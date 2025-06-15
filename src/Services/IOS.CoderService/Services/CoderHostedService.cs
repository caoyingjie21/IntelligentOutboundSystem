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
    private readonly IEnhancedMqttService _mqttService;
    private readonly ICoderService _coderService;
    private readonly CoderServiceOptions _options;
    private CodeInfo _currentCodeInfo = new();

    public CoderHostedService(
        ILogger<CoderHostedService> logger,
        IEnhancedMqttService mqttService,
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

            // MQTT服务已通过托管服务自动启动
            _logger.LogInformation("等待MQTT服务连接...");

            // 订阅MQTT事件
            _mqttService.OnConnectionChanged += OnMqttConnectionChanged;

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

            // 订阅MQTT主题
            await SubscribeToTopicsAsync();

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

            // MQTT服务将通过托管服务自动停止

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
            await SubscribeToTopicsAsync();
        }
    }

    private async Task SubscribeToTopicsAsync()
    {
        try
        {
            _logger.LogInformation("订阅MQTT主题...");

            // 订阅启动扫码消息
            await _mqttService.SubscribeAsync<CoderStartCommand>(
                _options.Topics.Receives.Start,
                HandleStartScanningMessageAsync);

            // 订阅订单消息
            await _mqttService.SubscribeAsync<OrderInfo>(
                _options.Topics.Receives.Order,
                HandleOrderMessageAsync);

            // 订阅配置消息
            await _mqttService.SubscribeAsync<CoderConfig>(
                _options.Topics.Receives.Config,
                HandleConfigMessageAsync);

            // 订阅停止消息
            await _mqttService.SubscribeAsync<object>(
                _options.Topics.Receives.Stop,
                HandleStopMessageAsync);

            _logger.LogInformation("MQTT主题订阅完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "订阅MQTT主题失败");
        }
    }

    private async Task HandleStartScanningMessageAsync(StandardMessage<CoderStartCommand> message)
    {
        try
        {
            _logger.LogInformation("收到启动扫码消息: {@Message}", message);

            var command = message.Data;
            _currentCodeInfo.Direction = command.Direction;
            _currentCodeInfo.StackHeight = command.StackHeight;

            // 清空之前的消息队列
            await _coderService.ClearMessageQueueAsync();
            _logger.LogInformation("已清空消息队列");

            // 延迟500ms等待客户端准备
            await Task.Delay(500);

            // 开始收集条码数据（等待5秒）
            _logger.LogInformation("开始收集条码数据，等待5秒...");
            await Task.Delay(5000);

            // 获取收集到的条码数据
            var messages = await _coderService.GetMessagesAsync();
            _logger.LogInformation("收集到 {Count} 条条码数据", messages.Count);

            // 处理条码数据
            var codeInfos = new List<CodeInfo>();
            foreach (var msg in messages)
            {
                var codeInfo = new CodeInfo
                {
                    Direction = _currentCodeInfo.Direction,
                    StackHeight = _currentCodeInfo.StackHeight,
                    Code = msg,
                    Timestamp = DateTime.Now
                };
                codeInfos.Add(codeInfo);
                _logger.LogInformation("处理条码: {Code}", msg);
            }

            // 发布条码完成消息
            var completeData = new CoderCompleteData
            {
                Direction = _currentCodeInfo.Direction,
                StackHeight = _currentCodeInfo.StackHeight,
                Codes = codeInfos.Select(c => c.Code).ToList(),
                Timestamp = DateTime.Now,
                Success = true
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.Complete, completeData, MessagePriority.High);
            _logger.LogInformation("已发布条码完成消息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理启动扫码消息失败");
            
            // 发布错误消息
            var errorData = new CoderCompleteData
            {
                Direction = _currentCodeInfo.Direction,
                StackHeight = _currentCodeInfo.StackHeight,
                Codes = new List<string>(),
                Timestamp = DateTime.Now,
                Success = false,
                ErrorMessage = ex.Message
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.Complete, errorData, MessagePriority.High);
        }
    }

    private async Task HandleOrderMessageAsync(StandardMessage<OrderInfo> message)
    {
        try
        {
            _logger.LogInformation("收到订单消息: {@Message}", message);

            var orderInfo = message.Data;
            _logger.LogInformation("处理订单: {OrderId}", orderInfo.OrderId);

            // 这里可以添加订单处理逻辑
            // 例如：保存订单信息、更新状态等

            // 发布订单请求消息（如果需要获取更多订单信息）
            var orderRequest = new OrderRequest
            {
                OrderId = orderInfo.OrderId,
                RequestType = "GetDetails",
                Timestamp = DateTime.Now
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.OrderRequest, orderRequest);
            _logger.LogInformation("已发布订单请求消息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理订单消息失败");
        }
    }

    private async Task HandleConfigMessageAsync(StandardMessage<CoderConfig> message)
    {
        try
        {
            _logger.LogInformation("收到配置消息: {@Message}", message);

            var config = message.Data;
            _logger.LogInformation("更新配置: {@Config}", config);

            // 这里可以添加配置更新逻辑
            // 例如：更新扫码参数、超时设置等

            _logger.LogInformation("配置更新完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理配置消息失败");
        }
    }

    private async Task HandleStopMessageAsync(StandardMessage<object> message)
    {
        try
        {
            _logger.LogInformation("收到停止消息: {@Message}", message);

            // 停止当前扫码操作
            await _coderService.ClearMessageQueueAsync();
            _logger.LogInformation("已停止扫码操作");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理停止消息失败");
        }
    }

    private async Task PublishStatusAsync()
    {
        try
        {
            var status = await _coderService.GetStatusAsync();
            var statusData = new CoderStatusData
            {
                IsRunning = status.IsRunning,
                ListenAddress = status.ListenAddress,
                ListenPort = status.ListenPort,
                ConnectedClients = status.ConnectedClients,
                Timestamp = DateTime.Now,
                ServiceVersion = "v1.0.0"
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.Status, statusData);
            _logger.LogDebug("已发布状态消息");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布状态消息失败");
        }
    }
}

// 消息数据模型
public class CoderStartCommand
{
    public string Direction { get; set; } = string.Empty;
    public double StackHeight { get; set; }
}

public class CoderCompleteData
{
    public string Direction { get; set; } = string.Empty;
    public double StackHeight { get; set; }
    public List<string> Codes { get; set; } = new();
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CoderStatusData
{
    public bool IsRunning { get; set; }
    public string ListenAddress { get; set; } = string.Empty;
    public int ListenPort { get; set; }
    public int ConnectedClients { get; set; }
    public DateTime Timestamp { get; set; }
    public string ServiceVersion { get; set; } = string.Empty;
}

public class OrderRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CoderConfig
{
    public int ScanTimeout { get; set; } = 5000;
    public int MaxRetries { get; set; } = 3;
    public bool EnableValidation { get; set; } = true;
}