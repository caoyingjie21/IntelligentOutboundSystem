using IOS.Infrastructure.Messaging;
using IOS.Shared.Messages;
using IOS.CoderService.Models;
using IOS.CoderService.Configuration;
using IOS.CoderService.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IOS.CoderService.Services;

/// <summary>
/// 条码服务托管服务
/// </summary>
public class CoderHostedService : BackgroundService
{
    private readonly ILogger<CoderHostedService> _logger;
    private readonly IMqttService _mqttService;
    private readonly ICoderService _coderService;
    private readonly CoderServiceOptions _options;
    private CodeInfo _currentCodeInfo = new();

    public CoderHostedService(
        ILogger<CoderHostedService> logger,
        IMqttService mqttService,
        ICoderService coderService,
        IOptions<CoderServiceOptions> options)
    {
        _logger = logger;
        _mqttService = mqttService;
        _coderService = coderService;
        _options = options.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("条码服务托管服务启动中...");

            // 启动MQTT服务
            await _mqttService.StartAsync();
            _logger.LogInformation("MQTT服务启动成功");

            // 订阅MQTT事件
            _mqttService.OnConnectionChanged += OnMqttConnectionChanged;
            _mqttService.OnMessageReceived += OnMqttMessageReceived;

            // 启动条码服务（Socket服务器）
            _logger.LogInformation("启动Socket服务器...");
            await _coderService.StartAsync(cancellationToken);
            
            // 确认Socket服务器状态
            if (_coderService.IsRunning)
            {
                _logger.LogInformation("Socket服务器启动成功，监听端口: {Port}", _options.SocketPort);
            }
            else
            {
                _logger.LogError("Socket服务器启动失败！");
                throw new InvalidOperationException("Socket服务器启动失败");
            }

            // 获取并记录当前状态
            var status = await _coderService.GetStatusAsync();
            _logger.LogInformation("条码服务状态 - 运行中: {IsRunning}, 监听地址: {Address}:{Port}, 连接客户端: {Clients}", 
                status.IsRunning, status.ListenAddress, status.ListenPort, status.ConnectedClients);

            _logger.LogInformation("条码服务托管服务启动成功");
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "条码服务托管服务启动失败");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("条码服务托管服务停止中...");

            // 停止条码服务
            await _coderService.StopAsync();
            _logger.LogInformation("条码服务停止成功");

            // 取消订阅MQTT事件
            _mqttService.OnConnectionChanged -= OnMqttConnectionChanged;
            _mqttService.OnMessageReceived -= OnMqttMessageReceived;

            // 停止MQTT服务
            await _mqttService.StopAsync();
            _logger.LogInformation("MQTT服务停止成功");

            _logger.LogInformation("条码服务托管服务停止成功");
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "条码服务托管服务停止失败");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("条码服务托管服务开始执行");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 定期发布状态信息
                await PublishStatusAsync();
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，不记录错误
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "条码服务托管服务执行异常");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("条码服务托管服务执行结束");
    }

    private async Task OnMqttConnectionChanged(bool isConnected)
    {
        _logger.LogInformation("MQTT连接状态变更: {IsConnected}", isConnected);

        if (isConnected)
        {
            await SubscribeToTopics();
        }
    }

    private async Task OnMqttMessageReceived(string topic, string message)
    {
        try
        {
            _logger.LogDebug("收到MQTT消息 - 主题: {Topic}, 消息: {Message}", topic, message);

            switch (topic)
            {
                case var t when t == _options.Topics.Receives.Start:
                    await HandleStartScanningMessageAsync(message);
                    break;
                case var t when t == _options.Topics.Receives.Order:
                    await HandleOrderMessageAsync(message);
                    break;
                case var t when t == _options.Topics.Receives.Config:
                    await HandleConfigMessageAsync(message);
                    break;
                case var t when t == _options.Topics.Receives.Stop:
                    await HandleStopMessageAsync(message);
                    break;
                default:
                    _logger.LogWarning("未处理的MQTT主题: {Topic}", topic);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理MQTT消息失败 - 主题: {Topic}, 消息: {Message}", topic, message);
        }
    }

    private async Task HandleStartScanningMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("收到启动扫码消息: {Message}", message);

            // 解析消息格式: {direction};{stackHeight}
            var parts = message.Split(';');
            if (parts.Length >= 2)
            {
                _currentCodeInfo.Direction = parts[0];
                if (double.TryParse(parts[1], out var height))
                {
                    _currentCodeInfo.StackHeight = height;
                }
            }

            // 清空之前的消息队列
            await _coderService.ClearMessageQueueAsync();
            _logger.LogInformation("已清空消息队列");

            // 延迟500ms等待客户端准备
            await Task.Delay(500);

            // 开始收集条码数据（等待5秒）
            _logger.LogInformation("开始收集条码数据，等待5秒...");
            await Task.Delay(5000);

            // 收集所有客户端的条码数据
            var codes = await _coderService.CollectCodesAsync();
            _currentCodeInfo.Codes = string.Join(";", codes);
            _currentCodeInfo.Timestamp = DateTime.Now;

            _logger.LogInformation("收集到条码数据: {Codes}", _currentCodeInfo.Codes);

            // 请求订单信息
            await _mqttService.PublishAsync(_options.Topics.Sends.Order, "请求订单信息");
            _logger.LogInformation("已发送订单请求");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理启动扫码消息失败");
        }
    }

    private async Task HandleOrderMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("收到订单消息: {Message}", message);

            _currentCodeInfo.Order = message;

            // 发布完整的条码信息到业务系统
            var coderMessage = new StandardMessage<CodeInfo>
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "CoderService",
                Type = MessageType.Response,
                Data = _currentCodeInfo
            };

            var jsonMessage = JsonSerializer.Serialize(coderMessage);
            await _mqttService.PublishAsync(_options.Topics.Sends.Coder, jsonMessage);

            _logger.LogInformation("已发布条码信息到业务系统: {Order}, 条码: {Codes}", 
                _currentCodeInfo.Order, _currentCodeInfo.Codes);

            // 重置当前条码信息
            _currentCodeInfo = new CodeInfo();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理订单消息失败");
        }
    }

    private async Task HandleConfigMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("收到配置查询消息: {Message}", message);

            var configResponse = new StandardMessage<object>
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "CoderService",
                Type = MessageType.Response,
                Data = new
                {
                    ServiceName = "CoderService",
                    Version = "1.0.0",
                    Configuration = new
                    {
                        _options.SocketAddress,
                        _options.SocketPort,
                        _options.MaxClients,
                        _options.ReceiveBufferSize,
                        _options.ClientTimeout
                    },
                    Status = await _coderService.GetStatusAsync(),
                    ConnectedClients = (await _coderService.GetConnectedClientsAsync()).Count
                }
            };

            var jsonResponse = JsonSerializer.Serialize(configResponse);
            await _mqttService.PublishAsync(_options.Topics.Sends.Status, jsonResponse);

            _logger.LogInformation("已发送配置信息响应");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理配置查询消息失败");
        }
    }

    private async Task HandleStopMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("收到停止消息: {Message}", message);

            // 清空消息队列
            await _coderService.ClearMessageQueueAsync();

            // 重置当前条码信息
            _currentCodeInfo = new CodeInfo();

            _logger.LogInformation("已处理停止消息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理停止消息失败");
        }
    }

    private async Task SubscribeToTopics()
    {
        try
        {
            await _mqttService.SubscribeAsync(_options.Topics.Receives.Start);
            await _mqttService.SubscribeAsync(_options.Topics.Receives.Order);
            await _mqttService.SubscribeAsync(_options.Topics.Receives.Config);
            await _mqttService.SubscribeAsync(_options.Topics.Receives.Stop);

            _logger.LogInformation("已订阅所有MQTT主题");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订阅MQTT主题失败");
        }
    }

    private async Task PublishStatusAsync()
    {
        try
        {
            var status = await _coderService.GetStatusAsync();
            var clients = await _coderService.GetConnectedClientsAsync();

            var statusMessage = new StandardMessage<object>
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "CoderService",
                Type = MessageType.Heartbeat,
                Data = new
                {
                    ServiceStatus = status,
                    ConnectedClients = clients.Count,
                    ClientDetails = clients.Select(c => new 
                    {
                        c.EndPoint,
                        c.ConnectedAt,
                        c.LastActivity,
                        MessageCount = c.Messages.Count
                    }).ToList(),
                    SocketServerRunning = _coderService.IsRunning,
                    ListeningPort = _options.SocketPort
                }
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.Status, statusMessage);
            
            // 添加详细的状态日志
            _logger.LogDebug("发布状态信息 - Socket运行: {SocketRunning}, 连接客户端: {ClientCount}, MQTT连接: {MqttConnected}", 
                _coderService.IsRunning, clients.Count, status.MqttConnected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布状态信息失败");
        }
    }
}