using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using IOS.Shared.Messages;
using IOS.Shared.Services;
using IOS.Shared.Configuration;

namespace IOS.Infrastructure.Messaging;

/// <summary>
/// 增强的MQTT服务实现
/// </summary>
public class EnhancedMqttService : IEnhancedMqttService
{
    private readonly ILogger<EnhancedMqttService> _logger;
    private readonly StandardMqttOptions _options;
    private readonly TopicRegistry _topicRegistry;
    private readonly MqttServiceStatistics _statistics;
    
    private IManagedMqttClient? _client;
    private readonly Dictionary<string, List<Func<string, string, Task>>> _messageHandlers = new();
    private readonly object _lock = new();
    private bool _disposed = false;

    public EnhancedMqttService(
        ILogger<EnhancedMqttService> logger,
        IOptions<StandardMqttOptions> options,
        TopicRegistry topicRegistry)
    {
        _logger = logger;
        _options = options.Value;
        _topicRegistry = topicRegistry;
        _statistics = new MqttServiceStatistics();
        
        InitializeTopicRegistry();
    }

    /// <summary>
    /// 消息接收事件
    /// </summary>
    public event Func<string, string, Task>? OnMessageReceived;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    public event Func<bool, Task>? OnConnectionChanged;

    /// <summary>
    /// 获取连接状态
    /// </summary>
    public bool IsConnected => _client?.IsConnected ?? false;

    /// <summary>
    /// 启动MQTT服务
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("正在启动增强MQTT服务...");

            // 验证配置
            var validationResult = MqttConfigurationManager.ValidateConfiguration(_options);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors);
                throw new InvalidOperationException($"MQTT配置验证失败: {errors}");
            }

            // 创建客户端
            var factory = new MqttFactory();
            _client = factory.CreateManagedMqttClient();

            // 设置事件处理器
            _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
            _client.ConnectedAsync += OnConnectedAsync;
            _client.DisconnectedAsync += OnDisconnectedAsync;
            _client.ConnectingFailedAsync += OnConnectingFailedAsync;

            // 创建连接选项
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Connection.Broker, _options.Connection.Port)
                .WithClientId(_options.Connection.ClientId)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.Connection.KeepAlivePeriod))
                .WithCleanSession(_options.Connection.CleanSession);

            if (!string.IsNullOrEmpty(_options.Connection.Username) && !string.IsNullOrEmpty(_options.Connection.Password))
            {
                clientOptions.WithCredentials(_options.Connection.Username, _options.Connection.Password);
            }

            if (_options.Connection.UseTls)
            {
                clientOptions.WithTls();
            }

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions.Build())
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(_options.Connection.ReconnectInterval))
                .WithMaxPendingMessages(_options.Messages.MaxRetries * 10)
                .Build();

            // 启动客户端
            await _client.StartAsync(managedOptions);

            _logger.LogInformation("增强MQTT服务启动成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动增强MQTT服务失败");
            throw;
        }
    }

    /// <summary>
    /// 停止MQTT服务
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("正在停止增强MQTT服务...");

            if (_client != null)
            {
                await _client.StopAsync();
                _client.Dispose();
                _client = null;
            }

            _logger.LogInformation("增强MQTT服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止增强MQTT服务失败");
            throw;
        }
    }

    /// <summary>
    /// 发布标准消息
    /// </summary>
    public async Task PublishAsync<T>(string topic, StandardMessage<T> message, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            await PublishAsync(topic, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布标准消息失败: Topic={Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// 发布原始消息
    /// </summary>
    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        if (_client == null || !IsConnected)
        {
            throw new InvalidOperationException("MQTT客户端未连接");
        }

        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.EnqueueAsync(message);
            
            _statistics.PublishedMessages++;
            _statistics.LastMessageAt = DateTime.UtcNow;
            
            _logger.LogDebug("消息已发布: Topic={Topic}, PayloadLength={Length}", topic, payload.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布消息失败: Topic={Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// 使用主题键发布消息
    /// </summary>
    public async Task<bool> PublishAsync<T>(
        string topicKey, 
        T data, 
        MessagePriority priority = MessagePriority.Normal,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var topic = _topicRegistry.GetTopic(topicKey);
            var message = new StandardMessage<T>
            {
                Type = MessageType.Command,
                Priority = priority,
                CorrelationId = correlationId,
                Data = data,
                Source = new ServiceInfo
                {
                    Name = _options.ServiceName,
                    Version = _options.Messages.Version
                }
            };

            await PublishAsync(topic, message, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "使用主题键发布消息失败: TopicKey={TopicKey}", topicKey);
            return false;
        }
    }

    /// <summary>
    /// 订阅主题
    /// </summary>
    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("MQTT客户端未初始化");
        }

        try
        {
            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client.SubscribeAsync(new[] { topicFilter });
            
            _statistics.SubscribedTopics++;
            
            _logger.LogInformation("已订阅主题: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订阅主题失败: Topic={Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// 使用主题键订阅消息
    /// </summary>
    public async Task SubscribeAsync<T>(
        string topicKey, 
        Func<StandardMessage<T>, Task> handler,
        MessageType? filterType = null,
        CancellationToken cancellationToken = default)
    {
        var topic = _topicRegistry.GetTopic(topicKey);
        
        // 注册消息处理器
        lock (_lock)
        {
            if (!_messageHandlers.ContainsKey(topic))
            {
                _messageHandlers[topic] = new List<Func<string, string, Task>>();
            }

            _messageHandlers[topic].Add(async (receivedTopic, payload) =>
            {
                try
                {
                    var message = JsonSerializer.Deserialize<StandardMessage<T>>(payload);
                    if (message == null) return;

                    // 类型过滤
                    if (filterType.HasValue && message.Type != filterType.Value)
                        return;

                    await handler(message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理消息失败: Topic={Topic}", receivedTopic);
                }
            });
        }

        await SubscribeAsync(topic, cancellationToken);
    }

    /// <summary>
    /// 取消订阅主题
    /// </summary>
    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            throw new InvalidOperationException("MQTT客户端未初始化");
        }

        try
        {
            await _client.UnsubscribeAsync(topic);
            
            lock (_lock)
            {
                _messageHandlers.Remove(topic);
            }
            
            _statistics.SubscribedTopics = Math.Max(0, _statistics.SubscribedTopics - 1);
            
            _logger.LogInformation("已取消订阅主题: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消订阅主题失败: Topic={Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// 批量发布消息
    /// </summary>
    public async Task<BatchPublishResult> PublishBatchAsync(
        IEnumerable<(string topic, string payload)> messages,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchPublishResult();
        
        foreach (var (topic, payload) in messages)
        {
            try
            {
                await PublishAsync(topic, payload, cancellationToken);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Failures.Add((topic, ex.Message));
                _logger.LogError(ex, "批量发布消息失败: Topic={Topic}", topic);
            }
        }

        return result;
    }

    /// <summary>
    /// 健康检查
    /// </summary>
    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_client == null || !IsConnected)
            {
                return false;
            }

            // 发送心跳消息
            var heartbeat = new StandardMessage<object>
            {
                Type = MessageType.Heartbeat,
                Source = new ServiceInfo
                {
                    Name = _options.ServiceName,
                    Version = _options.Messages.Version
                },
                Data = new { Status = "Healthy", Timestamp = DateTime.UtcNow }
            };

            await PublishAsync("ios/v1/status/heartbeat", heartbeat, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "健康检查失败");
            return false;
        }
    }

    /// <summary>
    /// 获取服务统计信息
    /// </summary>
    public MqttServiceStatistics GetStatistics()
    {
        _statistics.IsConnected = IsConnected;
        return _statistics;
    }

    /// <summary>
    /// 初始化主题注册
    /// </summary>
    private void InitializeTopicRegistry()
    {
        // 注册标准主题
        _topicRegistry.RegisterTopic("sensor.trigger", "ios/{version}/sensor/grating/trigger", MessageType.Event);
        _topicRegistry.RegisterTopic("order.new", "ios/{version}/order/system/new", MessageType.Command);
        _topicRegistry.RegisterTopic("vision.start", "ios/{version}/vision/camera/start", MessageType.Command);
        _topicRegistry.RegisterTopic("motion.move", "ios/{version}/motion/control/move", MessageType.Command);
        _topicRegistry.RegisterTopic("motion.complete", "ios/{version}/motion/control/complete", MessageType.Event);
        _topicRegistry.RegisterTopic("coder.start", "ios/{version}/coder/service/start", MessageType.Command);
        _topicRegistry.RegisterTopic("coder.complete", "ios/{version}/coder/service/complete", MessageType.Event);
        _topicRegistry.RegisterTopic("status.heartbeat", "ios/{version}/status/{0}/heartbeat", MessageType.Heartbeat);
    }

    /// <summary>
    /// 连接成功事件处理
    /// </summary>
    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _statistics.ConnectedAt = DateTime.UtcNow;
        _statistics.ReconnectCount++;
        
        _logger.LogInformation("MQTT客户端已连接");
        
        if (OnConnectionChanged != null)
        {
            await OnConnectionChanged(true);
        }
    }

    /// <summary>
    /// 连接断开事件处理
    /// </summary>
    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("MQTT客户端连接断开: {Reason}", e.Reason);
        
        if (OnConnectionChanged != null)
        {
            await OnConnectionChanged(false);
        }
    }

    /// <summary>
    /// 连接失败事件处理
    /// </summary>
    private Task OnConnectingFailedAsync(ConnectingFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "MQTT客户端连接失败");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 消息接收事件处理
    /// </summary>
    private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _statistics.ReceivedMessages++;
            _statistics.LastMessageAt = DateTime.UtcNow;

            _logger.LogDebug("收到消息: Topic={Topic}, PayloadLength={Length}", topic, payload.Length);

            // 触发通用消息接收事件
            if (OnMessageReceived != null)
            {
                await OnMessageReceived(topic, payload);
            }

            // 触发特定主题的处理器
            lock (_lock)
            {
                if (_messageHandlers.TryGetValue(topic, out var handlers))
                {
                    foreach (var handler in handlers)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await handler(topic, payload);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "消息处理器执行失败: Topic={Topic}", topic);
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理接收消息时出错");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _disposed = true;
        }
    }
} 