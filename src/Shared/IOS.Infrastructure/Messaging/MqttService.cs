using IOS.Shared.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using System.Text;
using System.Text.Json;

namespace IOS.Infrastructure.Messaging;

/// <summary>
/// MQTT服务实现
/// </summary>
public class MqttService : IMqttService
{
    private readonly ILogger<MqttService> _logger;
    private readonly MqttOptions _options;
    private readonly IManagedMqttClient _client;
    private bool _disposed = false;

    public event Func<string, string, Task>? OnMessageReceived;
    public event Func<bool, Task>? OnConnectionChanged;

    public bool IsConnected => _client?.IsConnected ?? false;

    public MqttService(IOptions<MqttOptions> options, ILogger<MqttService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var factory = new MqttFactory();
        _client = factory.CreateManagedMqttClient();

        _client.ApplicationMessageReceivedAsync += OnApplicationMessageReceivedAsync;
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ConnectingFailedAsync += OnConnectingFailedAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_options.Broker, _options.Port)
                .WithClientId(_options.ClientId)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_options.KeepAlivePeriod))
                .WithTimeout(TimeSpan.FromSeconds(_options.ConnectionTimeout))
                .WithCleanSession(_options.CleanSession);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                clientOptions.WithCredentials(_options.Username, _options.Password);
            }

            if (_options.UseTls)
            {
                clientOptions.WithTlsOptions(o => o.UseTls());
            }

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions.Build())
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(_options.ReconnectInterval))
                .Build();

            await _client.StartAsync(managedOptions);
            _logger.LogInformation("MQTT服务已启动, Broker: {Broker}:{Port}", _options.Broker, _options.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动MQTT服务失败");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.StopAsync();
            _logger.LogInformation("MQTT服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止MQTT服务失败");
            throw;
        }
    }

    public async Task PublishAsync<T>(string topic, StandardMessage<T> message, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            await PublishAsync(topic, json, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布消息失败, Topic: {Topic}", topic);
            throw;
        }
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(Encoding.UTF8.GetBytes(payload))
                .WithRetainFlag(false)
                .Build();

            await _client.EnqueueAsync(message);
            _logger.LogDebug("消息已发布, Topic: {Topic}, Payload长度: {Length}", topic, payload.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布消息失败, Topic: {Topic}", topic);
            throw;
        }
    }

    public async Task SubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        try
        {
            var topicFilter = new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .Build();

            await _client.SubscribeAsync(new[] { topicFilter });
            _logger.LogInformation("已订阅主题: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订阅主题失败, Topic: {Topic}", topic);
            throw;
        }
    }

    public async Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.UnsubscribeAsync(topic);
            _logger.LogInformation("已取消订阅主题: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消订阅主题失败, Topic: {Topic}", topic);
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT健康检查失败");
            return false;
        }
    }

    private async Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

            _logger.LogDebug("收到消息, Topic: {Topic}, Payload长度: {Length}", topic, payload.Length);

            if (OnMessageReceived != null)
            {
                await OnMessageReceived(topic, payload);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理接收消息失败");
        }
    }

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs e)
    {
        _logger.LogInformation("MQTT客户端已连接");
        if (OnConnectionChanged != null)
        {
            await OnConnectionChanged(true);
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning("MQTT客户端已断开连接, 原因: {Reason}", e.Reason);
        if (OnConnectionChanged != null)
        {
            await OnConnectionChanged(false);
        }
    }

    private Task OnConnectingFailedAsync(ConnectingFailedEventArgs e)
    {
        _logger.LogError(e.Exception, "MQTT连接失败");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            try
            {
                _client?.StopAsync().Wait(TimeSpan.FromSeconds(5));
                _client?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "释放MQTT服务资源失败");
            }
            finally
            {
                _disposed = true;
            }
        }
    }
} 