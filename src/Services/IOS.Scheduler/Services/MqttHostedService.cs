using IOS.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Services;

/// <summary>
/// MQTT托管服务 - 负责启动和停止MQTT服务
/// </summary>
public class MqttHostedService : BackgroundService
{
    private readonly IMqttService _mqttService;
    private readonly ILogger<MqttHostedService> _logger;
    private readonly MessageHandlerFactory _messageHandlerFactory;
    private readonly SharedDataService _sharedDataService;

    public MqttHostedService(
        IMqttService mqttService, 
        ILogger<MqttHostedService> logger,
        MessageHandlerFactory messageHandlerFactory,
        SharedDataService sharedDataService)
    {
        _mqttService = mqttService;
        _logger = logger;
        _messageHandlerFactory = messageHandlerFactory;
        _sharedDataService = sharedDataService;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("启动MQTT托管服务...");
        
        try
        {
            // 订阅连接状态变更事件
            _mqttService.OnConnectionChanged += OnConnectionChanged;
            
            // 订阅消息接收事件
            _mqttService.OnMessageReceived += OnMessageReceived;
            
            await _mqttService.StartAsync(cancellationToken);
            _logger.LogInformation("MQTT服务启动成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动MQTT服务失败");
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("停止MQTT托管服务...");
        
        try
        {
            // 取消订阅事件
            _mqttService.OnConnectionChanged -= OnConnectionChanged;
            _mqttService.OnMessageReceived -= OnMessageReceived;
            
            await _mqttService.StopAsync(cancellationToken);
            _logger.LogInformation("MQTT服务停止成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止MQTT服务失败");
        }

        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 保持服务运行
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 检查MQTT连接状态
                if (!_mqttService.IsConnected)
                {
                    _logger.LogWarning("MQTT连接断开，等待重新连接...");
                }

                // 每30秒检查一次
                await Task.Delay(30000, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，退出循环
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT托管服务运行异常");
                await Task.Delay(5000, stoppingToken); // 等待5秒后继续
            }
        }
    }

    /// <summary>
    /// 连接状态变更处理
    /// </summary>
    private async Task OnConnectionChanged(bool isConnected)
    {
        if (isConnected)
        {
            _logger.LogInformation("MQTT连接成功，开始订阅主题...");
            await SubscribeToTopics();
        }
        else
        {
            _logger.LogWarning("MQTT连接断开");
        }
    }

    /// <summary>
    /// 消息接收处理
    /// </summary>
    private async Task OnMessageReceived(string topic, string message)
    {
        try
        {
            _logger.LogDebug("收到MQTT消息，主题: {Topic}, 消息长度: {Length}", topic, message.Length);
            
            // 创建消息处理器并处理消息
            var handler = _messageHandlerFactory.CreateHandler(topic);
            await handler.HandleMessageAsync(topic, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理MQTT消息失败，主题: {Topic}", topic);
        }
    }

    /// <summary>
    /// 订阅所有需要的主题
    /// </summary>
    private async Task SubscribeToTopics()
    {
        var topics = new[]
        {
            // 系统消息
            "system/heartbeat",
            "system/status", 
            "system/config",
            
            //// 出库任务消息
            //"outbound/task/created",
            //"outbound/task/execute",
            //"outbound/task/progress", 
            //"outbound/task/completed",
            //"outbound/task/cancelled",
            
            //// 设备消息（使用通配符）
            //"device/+/status",
            //"device/+/command",
            //"device/+/response",
            
            // 传感器消息
            "sensor/grating",
            "sensor/data",
            
            // 运动控制消息
            "motion/moving/complete",
            //"motion/position",
            "motion/status",
            
            // 视觉系统消息
            "vision/detection",
            "vision/result",
            
            // 读码器消息
            "coder/result",
            "coder/complete"
        };

        foreach (var topic in topics)
        {
            try
            {
                await _mqttService.SubscribeAsync(topic);
                _logger.LogDebug("成功订阅主题: {Topic}", topic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "订阅主题失败: {Topic}", topic);
            }
        }
        
        _logger.LogInformation("完成主题订阅，共订阅 {Count} 个主题", topics.Length);
    }
} 