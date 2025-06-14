using IOS.Infrastructure.Messaging;
using IOS.MotionControl.Configuration;
using IOS.Shared.Messages;
using IOS.Shared.Models;
using IOS.Shared.Utilities;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IOS.MotionControl.Services;

/// <summary>
/// 运动控制托管服务 - 处理MQTT消息和运动控制集成
/// </summary>
public class MotionControlHostedService : BackgroundService
{
    private readonly ILogger<MotionControlHostedService> _logger;
    private readonly IMqttService _mqttService;
    private readonly IMotionControlService _motionControlService;
    private readonly MotionControlOptions _options;

    public MotionControlHostedService(
        ILogger<MotionControlHostedService> logger,
        IMqttService mqttService,
        IMotionControlService motionControlService,
        IOptions<MotionControlOptions> options)
    {
        _logger = logger;
        _mqttService = mqttService;
        _motionControlService = motionControlService;
        _options = options.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("运动控制托管服务启动中...");

            // 启动MQTT服务
            await _mqttService.StartAsync();
            _logger.LogInformation("MQTT服务启动成功");

            // 订阅MQTT事件
            _mqttService.OnConnectionChanged += OnMqttConnectionChanged;
            _mqttService.OnMessageReceived += OnMqttMessageReceived;

            // 初始化运动控制系统
            if (!_motionControlService.IsInitialized)
            {
                _logger.LogInformation("初始化运动控制系统");
                await _motionControlService.InitializeAsync();
            }

            _logger.LogInformation("运动控制托管服务启动成功");
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "运动控制托管服务启动失败");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("运动控制托管服务停止中...");

            // 取消订阅MQTT事件
            _mqttService.OnConnectionChanged -= OnMqttConnectionChanged;
            _mqttService.OnMessageReceived -= OnMqttMessageReceived;

            // 停止MQTT服务
            await _mqttService.StopAsync();
            _logger.LogInformation("MQTT服务停止成功");

            // 关闭运动控制系统
            await _motionControlService.ShutdownAsync();

            _logger.LogInformation("运动控制托管服务停止成功");
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "运动控制托管服务停止失败");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("运动控制托管服务开始执行");

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
                _logger.LogError(ex, "运动控制托管服务执行异常");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("运动控制托管服务执行结束");
    }

    /// <summary>
    /// MQTT连接状态变更处理
    /// </summary>
    private async Task OnMqttConnectionChanged(bool isConnected)
    {
        _logger.LogInformation("MQTT连接状态变更: {IsConnected}", isConnected);

        if (isConnected)
        {
            _logger.LogInformation("MQTT连接成功，开始订阅运动控制主题...");
            await SubscribeToTopics();
        }
        else
        {
            _logger.LogWarning("MQTT连接断开");
        }
    }

    /// <summary>
    /// MQTT消息接收处理
    /// </summary>
    private async Task OnMqttMessageReceived(string topic, string message)
    {
        try
        {
            _logger.LogDebug("收到MQTT消息 - 主题: {Topic}, 消息: {Message}", topic, message);

            switch (topic)
            {
                case var t when t == _options.Topics.Receives.Moving:
                    await HandleMovingMessageAsync(message);
                    break;
                case var t when t == _options.Topics.Receives.Back:
                    await HandleBackMessageAsync(message);
                    break;
                case var t when t == _options.Topics.Receives.Config:
                    await HandleConfigMessageAsync(message);
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

    /// <summary>
    /// 处理运动命令消息
    /// </summary>
    private async Task HandleMovingMessageAsync(string message)
    {
        try
        {
            // 使用统一的JSON反序列化配置
            if (!JsonHelper.TryDeserialize<MotionSendData>(message, out var motionMessage, out var error))
            {
                _logger.LogError("无法解析运动数据: {Error}, 消息: {Message}", error, message);
                return;
            }
            
            _logger.LogInformation("收到运动数据: Position={Position}", motionMessage.Position);
            
            // 转换为脉冲数 (假设1mm = 1000脉冲)
            var Pulse = (int)(motionMessage.Position * 1000) * 100;

            _logger.LogInformation("执行运动命令, 脉冲: {Pulse}pulse", Pulse);

            await _motionControlService.MoveAbsoluteAsync(Pulse);

            // 发送完成消息
            await _mqttService.PublishAsync(_options.Topics.Sends.MovingComplete, "test");

            _logger.LogInformation("运动命令执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理运动命令失败: {Message}", message);
        }
    }

    /// <summary>
    /// 处理回零命令消息
    /// </summary>
    private async Task HandleBackMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("执行回零命令");
            await _motionControlService.HomeAsync();
            
            // 发送回零完成消息
            var response = new StandardMessage<object>
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "MotionControl",
                Type = MessageType.Response,
                Data = new { Status = "Success", Position = _motionControlService.CurrentPosition }
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.MovingComplete, JsonHelper.Serialize(response));
            _logger.LogInformation("回零命令执行完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行回零命令失败");
        }
    }

    /// <summary>
    /// 处理配置命令消息
    /// </summary>
    private async Task HandleConfigMessageAsync(string message)
    {
        try
        {
            _logger.LogInformation("收到配置命令: {Message}", message);
            
            // 发送当前配置信息
            var configResponse = new StandardMessage<object>
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "MotionControl",
                Type = MessageType.Response,
                Data = new
                {
                    Speed = _options.Speed,
                    MinPosition = _options.MinPosition,
                    MaxPosition = _options.MaxPosition,
                    CurrentPosition = _motionControlService.CurrentPosition,
                    IsEnabled = _motionControlService.IsEnabled
                }
            };

            await _mqttService.PublishAsync(_options.Topics.Sends.Status, JsonHelper.Serialize(configResponse));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理配置命令失败");
        }
    }

    /// <summary>
    /// 订阅MQTT主题
    /// </summary>
    private async Task SubscribeToTopics()
    {
        var topics = new[]
        {
            _options.Topics.Receives.Moving,
            _options.Topics.Receives.Back,
            _options.Topics.Receives.Config
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

        _logger.LogInformation("完成运动控制主题订阅，共订阅 {Count} 个主题", topics.Length);
    }

    /// <summary>
    /// 发布状态信息
    /// </summary>
    private async Task PublishStatusAsync()
    {
        try
        {
            var status = await _motionControlService.GetStatusAsync();
            
            var statusMessage = new StandardMessage<MotionStatus>
            {
                MessageId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Source = "MotionControl",
                Type = MessageType.Response,
                Data = status
            };
            await _mqttService.PublishAsync(_options.Topics.Sends.Status, JsonHelper.Serialize(statusMessage));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发布状态信息失败");
        }
    }
} 