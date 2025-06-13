using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 传感器消息处理器
/// </summary>
public class SensorMessageHandler : BaseMessageHandler
{
    public SensorMessageHandler(
        ILogger<SensorMessageHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        switch (topic)
        {
            case "sensor/grating":
                await HandleGratingData(message);
                break;
            default:
                Logger.LogWarning("未知的传感器消息主题: {Topic}", topic);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "sensor/grating",
        };
    }

    private async Task HandleGratingData(string message)
    {
        var gratingData = DeserializeMessage<GratingData>(message);
        if (gratingData == null)
        {
            Logger.LogError("解析光栅传感器数据失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到光栅传感器数据: 方向={Direction}",
            gratingData.Direction);

        // 存储光栅发送过来的方向
        SharedDataService.SetData("sensor:grating", gratingData);

        // 触发相关任务处理
        await ProcessGratingEvent(gratingData);
    }

    private async Task ProcessGratingEvent(GratingData gratingData)
    {
        // 处理光栅传感器事件
        if (!string.IsNullOrEmpty(gratingData.Direction))
        {
            Logger.LogInformation("方向接收成功,执行高度检测");
            
            var notification = new
            {
                Direction = gratingData.Direction,
                Timestamp = DateTime.UtcNow
            };
            
            await MqttService.PublishAsync("vision/height", SerializeObject(notification));
        }
    }
}

#region Data Models

public class GratingData
{
    public string Direction { get; set; } = string.Empty;
}

#endregion 