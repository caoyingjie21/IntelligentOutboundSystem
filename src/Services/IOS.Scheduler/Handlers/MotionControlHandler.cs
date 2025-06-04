using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 运动控制消息处理器
/// </summary>
public class MotionControlHandler : BaseMessageHandler
{
    public MotionControlHandler(
        ILogger<MotionControlHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        switch (topic)
        {
            case "motion/moving/complete":
                await HandleMovingComplete(message);
                break;
            case "motion/position":
                await HandlePositionUpdate(message);
                break;
            default:
                Logger.LogWarning("未知的运动控制消息主题: {Topic}", topic);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "motion/moving/complete",
            "motion/position"
        };
    }

    private async Task HandleMovingComplete(string message)
    {
        var motionData = DeserializeMessage<MotionCompleteData>(message);
        if (motionData == null)
        {
            Logger.LogError("解析运动完成消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("运动控制完成: 任务ID={TaskId}, 位置={Position}", 
            motionData.TaskId, motionData.FinalPosition);

        // 更新任务状态
        SharedDataService.SetData($"task:{motionData.TaskId}:motion_status", "completed");
        SharedDataService.SetData($"task:{motionData.TaskId}:final_position", motionData.FinalPosition);

        // 触发下一步操作
        await TriggerNextStep(motionData.TaskId);
    }

    private async Task HandlePositionUpdate(string message)
    {
        var positionData = DeserializeMessage<PositionData>(message);
        if (positionData == null)
        {
            Logger.LogError("解析位置更新消息失败: {Message}", message);
            return;
        }

        Logger.LogDebug("位置更新: X={X}, Y={Y}, Z={Z}", 
            positionData.X, positionData.Y, positionData.Z);

        // 存储当前位置
        SharedDataService.SetData("motion:current_position", positionData);
        SharedDataService.SetData("motion:last_update", DateTime.UtcNow);

        await Task.CompletedTask;
    }

    private async Task TriggerNextStep(string taskId)
    {
        // 触发任务的下一步操作
        var nextStepMessage = new
        {
            TaskId = taskId,
            Step = "vision_detection",
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("outbound/task/next_step", SerializeObject(nextStepMessage));
        Logger.LogInformation("已触发任务下一步: {TaskId}", taskId);
    }
}

#region Data Models

public class MotionCompleteData
{
    public string TaskId { get; set; } = string.Empty;
    public string FinalPosition { get; set; } = string.Empty;
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PositionData
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion 