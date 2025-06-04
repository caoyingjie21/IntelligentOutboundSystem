using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 默认消息处理器 - 处理未知主题的消息
/// </summary>
public class DefaultMessageHandler : BaseMessageHandler
{
    public DefaultMessageHandler(
        ILogger<DefaultMessageHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        Logger.LogInformation("收到未知主题消息: 主题={Topic}, 消息长度={Length}", topic, message?.Length ?? 0);

        // 记录未知消息到共享数据中，用于调试和分析
        var unknownMessage = new
        {
            Topic = topic,
            Message = message,
            Timestamp = DateTime.UtcNow,
            MessageLength = message?.Length ?? 0
        };

        SharedDataService.SetData($"unknown_messages:{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}", unknownMessage);

        // 发送未知消息通知
        var notification = new
        {
            Event = "unknown_topic_received",
            Topic = topic,
            MessageLength = message?.Length ?? 0,
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("system/events/unknown_topic", SerializeObject(notification));

        // 尝试基于主题模式进行简单处理
        await TryBasicProcessing(topic, message);
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        // 默认处理器支持所有主题
        return new[] { "#" };
    }

    public override bool CanHandle(string topic)
    {
        // 默认处理器可以处理任何主题
        return true;
    }

    /// <summary>
    /// 尝试基于主题模式进行基本处理
    /// </summary>
    private async Task TryBasicProcessing(string topic, string message)
    {
        try
        {
            var topicParts = topic.Split('/');
            if (topicParts.Length < 2)
            {
                Logger.LogDebug("主题层级不足，无法进行基本处理: {Topic}", topic);
                return;
            }

            var category = topicParts[0];
            var subcategory = topicParts[1];

            Logger.LogDebug("尝试基本处理: 类别={Category}, 子类别={Subcategory}", category, subcategory);

            switch (category.ToLower())
            {
                case "test":
                    await HandleTestMessage(topic, message);
                    break;
                case "debug":
                    await HandleDebugMessage(topic, message);
                    break;
                case "log":
                    await HandleLogMessage(topic, message);
                    break;
                default:
                    Logger.LogDebug("未识别的消息类别: {Category}", category);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "基本处理失败: 主题={Topic}", topic);
        }
    }

    /// <summary>
    /// 处理测试消息
    /// </summary>
    private async Task HandleTestMessage(string topic, string message)
    {
        Logger.LogInformation("处理测试消息: 主题={Topic}", topic);

        var testResponse = new
        {
            Type = "test_response",
            OriginalTopic = topic,
            OriginalMessage = message,
            ProcessedBy = "DefaultMessageHandler",
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("test/response", SerializeObject(testResponse));
    }

    /// <summary>
    /// 处理调试消息
    /// </summary>
    private async Task HandleDebugMessage(string topic, string message)
    {
        Logger.LogDebug("处理调试消息: 主题={Topic}, 内容={Message}", topic, message);

        // 存储调试信息
        var debugInfo = new
        {
            Topic = topic,
            Message = message,
            Handler = "DefaultMessageHandler",
            Timestamp = DateTime.UtcNow
        };

        SharedDataService.SetData($"debug:{DateTime.UtcNow:yyyyMMdd_HHmmss}", debugInfo);

        // 如果是调试命令，尝试执行
        if (message?.StartsWith("cmd:") == true)
        {
            await ProcessDebugCommand(message[4..].Trim());
        }
    }

    /// <summary>
    /// 处理日志消息
    /// </summary>
    private async Task HandleLogMessage(string topic, string message)
    {
        Logger.LogInformation("收到外部日志消息: 主题={Topic}", topic);

        try
        {
            var logData = DeserializeMessage<ExternalLogData>(message);
            if (logData != null)
            {
                // 根据日志级别记录
                switch (logData.Level.ToLower())
                {
                    case "error":
                        Logger.LogError("外部日志 [{Source}]: {Message}", logData.Source, logData.Message);
                        break;
                    case "warning":
                        Logger.LogWarning("外部日志 [{Source}]: {Message}", logData.Source, logData.Message);
                        break;
                    case "info":
                        Logger.LogInformation("外部日志 [{Source}]: {Message}", logData.Source, logData.Message);
                        break;
                    default:
                        Logger.LogDebug("外部日志 [{Source}]: {Message}", logData.Source, logData.Message);
                        break;
                }
            }
            else
            {
                Logger.LogInformation("外部日志: {Message}", message);
            }
        }
        catch
        {
            Logger.LogInformation("外部日志: {Message}", message);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 处理调试命令
    /// </summary>
    private async Task ProcessDebugCommand(string command)
    {
        Logger.LogInformation("执行调试命令: {Command}", command);

        try
        {
            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var cmd = parts[0].ToLower();
            var args = parts.Skip(1).ToArray();

            switch (cmd)
            {
                case "status":
                    await ExecuteStatusCommand();
                    break;
                case "clear":
                    await ExecuteClearCommand(args);
                    break;
                case "info":
                    await ExecuteInfoCommand(args);
                    break;
                case "ping":
                    await ExecutePingCommand();
                    break;
                default:
                    Logger.LogWarning("未知的调试命令: {Command}", cmd);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "执行调试命令失败: {Command}", command);
        }
    }

    private async Task ExecuteStatusCommand()
    {
        var status = new
        {
            Service = "IOS.Scheduler",
            Handler = "DefaultMessageHandler",
            Status = "running",
            SharedDataCount = SharedDataService.Count,
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("debug/status/response", SerializeObject(status));
    }

    private async Task ExecuteClearCommand(string[] args)
    {
        if (args.Length > 0 && args[0] == "unknown")
        {
            // 清除未知消息记录
            var keys = SharedDataService.GetAllKeys()
                .Where(k => k.StartsWith("unknown_messages:"))
                .ToList();

            foreach (var key in keys)
            {
                SharedDataService.RemoveData(key);
            }

            Logger.LogInformation("已清除 {Count} 条未知消息记录", keys.Count);
        }

        await Task.CompletedTask;
    }

    private async Task ExecuteInfoCommand(string[] args)
    {
        var info = new
        {
            UnknownMessagesCount = SharedDataService.GetAllKeys().Count(k => k.StartsWith("unknown_messages:")),
            DebugMessagesCount = SharedDataService.GetAllKeys().Count(k => k.StartsWith("debug:")),
            TotalSharedDataCount = SharedDataService.Count,
            LastProcessedTime = DateTime.UtcNow
        };

        await MqttService.PublishAsync("debug/info/response", SerializeObject(info));
    }

    private async Task ExecutePingCommand()
    {
        var pong = new
        {
            Type = "pong",
            Source = "DefaultMessageHandler",
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("debug/pong", SerializeObject(pong));
    }
}

#region Data Models

public class ExternalLogData
{
    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

#endregion 