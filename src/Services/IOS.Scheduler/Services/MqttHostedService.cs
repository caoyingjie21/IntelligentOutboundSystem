using IOS.Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
    private readonly MqttOptions _mqttOptions;

    public MqttHostedService(
        IMqttService mqttService, 
        ILogger<MqttHostedService> logger,
        MessageHandlerFactory messageHandlerFactory,
        SharedDataService sharedDataService,
        IOptions<MqttOptions> mqttOptions)
    {
        _mqttService = mqttService;
        _logger = logger;
        _messageHandlerFactory = messageHandlerFactory;
        _sharedDataService = sharedDataService;
        _mqttOptions = mqttOptions.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("启动MQTT托管服务...");
        
        try
        {
            // 先调用基类StartAsync启动BackgroundService
            await base.StartAsync(cancellationToken);
            
            // 订阅连接状态变更事件
            _mqttService.OnConnectionChanged += OnConnectionChanged;
            
            // 订阅消息接收事件
            _mqttService.OnMessageReceived += OnMessageReceived;
            
            // 启动MQTT服务
            await _mqttService.StartAsync(cancellationToken);
            _logger.LogInformation("MQTT服务启动成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动MQTT服务失败");
            throw;
        }
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
        _logger.LogDebug("MQTT托管服务后台任务已启动");
        
        // 保持服务运行，等待取消信号
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // 正常取消，服务停止
            _logger.LogDebug("MQTT托管服务后台任务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT托管服务运行异常");
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
            // 检查主题是否在订阅列表中
            if (!IsSubscribedTopic(topic))
            {
                _logger.LogDebug("忽略未订阅的主题: {Topic}", topic);
                return;
            }

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
    /// 检查主题是否在订阅列表中（精确匹配）
    /// </summary>
    private bool IsSubscribedTopic(string topic)
    {
        var subscribedTopics = _mqttOptions.Topics.Subscribe;
        
        if (subscribedTopics == null || !subscribedTopics.Any())
        {
            return false;
        }

        // 只进行精确匹配，不支持通配符匹配
        return subscribedTopics.Contains(topic, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 订阅所有需要的主题
    /// </summary>
    private async Task SubscribeToTopics()
    {
        var topics = _mqttOptions.Topics.Subscribe;

        if (topics == null || !topics.Any())
        {
            _logger.LogWarning("配置中未找到需要订阅的主题");
            return;
        }

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
        
        _logger.LogInformation("完成主题订阅，共订阅 {Count} 个主题", topics.Count);
    }
} 