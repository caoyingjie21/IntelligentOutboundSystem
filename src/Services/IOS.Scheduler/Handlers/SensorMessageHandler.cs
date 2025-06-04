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
            case "sensor/data":
                await HandleSensorData(message);
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
            "sensor/data"
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

        Logger.LogInformation("收到光栅传感器数据: 状态={Status}, 位置={Position}", 
            gratingData.Status, gratingData.Position);

        // 存储传感器数据
        SharedDataService.SetData("sensor:grating:current", gratingData);
        SharedDataService.SetData("sensor:grating:last_update", DateTime.UtcNow);

        // 触发相关任务处理
        await ProcessGratingEvent(gratingData);
    }

    private async Task HandleSensorData(string message)
    {
        var sensorData = DeserializeMessage<SensorData>(message);
        if (sensorData == null)
        {
            Logger.LogError("解析传感器数据失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到传感器数据: 传感器ID={SensorId}, 值={Value}", 
            sensorData.SensorId, sensorData.Value);

        // 存储传感器数据
        SharedDataService.SetData($"sensor:{sensorData.SensorId}:current", sensorData);
        SharedDataService.SetData($"sensor:{sensorData.SensorId}:last_update", DateTime.UtcNow);

        await Task.CompletedTask;
    }

    private async Task ProcessGratingEvent(GratingData gratingData)
    {
        // 处理光栅传感器事件
        if (gratingData.Status == "blocked")
        {
            Logger.LogInformation("检测到物体阻挡光栅，触发处理流程");
            
            var notification = new
            {
                Event = "grating_blocked",
                Position = gratingData.Position,
                Timestamp = DateTime.UtcNow
            };
            
            await MqttService.PublishAsync("system/events/grating_blocked", SerializeObject(notification));
        }
        else if (gratingData.Status == "clear")
        {
            Logger.LogInformation("光栅传感器恢复正常");
            
            var notification = new
            {
                Event = "grating_clear",
                Position = gratingData.Position,
                Timestamp = DateTime.UtcNow
            };
            
            await MqttService.PublishAsync("system/events/grating_clear", SerializeObject(notification));
        }
    }
}

#region Data Models

public class GratingData
{
    public string Status { get; set; } = string.Empty;
    public double Position { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SensorData
{
    public string SensorId { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

#endregion 