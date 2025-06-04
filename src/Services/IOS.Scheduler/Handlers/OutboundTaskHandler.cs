using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 出库任务消息处理器
/// </summary>
public class OutboundTaskHandler : BaseMessageHandler
{
    public OutboundTaskHandler(
        ILogger<OutboundTaskHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService) 
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        switch (topic)
        {
            case "outbound/task/created":
                await HandleTaskCreated(message);
                break;
            case "outbound/task/execute":
                await HandleTaskExecute(message);
                break;
            case "outbound/task/progress":
                await HandleTaskProgress(message);
                break;
            case "outbound/task/completed":
                await HandleTaskCompleted(message);
                break;
            case "outbound/task/cancelled":
                await HandleTaskCancelled(message);
                break;
            default:
                Logger.LogWarning("未知的出库任务消息主题: {Topic}", topic);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "outbound/task/created",
            "outbound/task/execute",
            "outbound/task/progress",
            "outbound/task/completed",
            "outbound/task/cancelled"
        };
    }

    /// <summary>
    /// 处理任务创建消息
    /// </summary>
    private async Task HandleTaskCreated(string message)
    {
        var taskInfo = DeserializeMessage<OutboundTaskInfo>(message);
        if (taskInfo == null)
        {
            Logger.LogError("解析任务创建消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到新的出库任务: 任务ID={TaskId}, 类型={TaskType}", taskInfo.TaskId, taskInfo.TaskType);

        // 存储任务信息到共享数据中
        SharedDataService.SetData($"task:{taskInfo.TaskId}", taskInfo);
        SharedDataService.SetData($"task:{taskInfo.TaskId}:status", "created");

        // 发送任务确认消息
        var confirmMessage = new
        {
            TaskId = taskInfo.TaskId,
            Status = "received",
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("outbound/task/confirm", SerializeObject(confirmMessage));
        Logger.LogInformation("已确认接收任务: {TaskId}", taskInfo.TaskId);
    }

    /// <summary>
    /// 处理任务执行消息
    /// </summary>
    private async Task HandleTaskExecute(string message)
    {
        var executeInfo = DeserializeMessage<TaskExecuteInfo>(message);
        if (executeInfo == null)
        {
            Logger.LogError("解析任务执行消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("开始执行任务: {TaskId}", executeInfo.TaskId);

        // 更新任务状态
        SharedDataService.SetData($"task:{executeInfo.TaskId}:status", "executing");
        SharedDataService.SetData($"task:{executeInfo.TaskId}:start_time", DateTime.UtcNow);

        // 开始执行任务逻辑
        await ExecuteOutboundTask(executeInfo);
    }

    /// <summary>
    /// 处理任务进度消息
    /// </summary>
    private async Task HandleTaskProgress(string message)
    {
        var progressInfo = DeserializeMessage<TaskProgressInfo>(message);
        if (progressInfo == null)
        {
            Logger.LogError("解析任务进度消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("任务进度更新: 任务ID={TaskId}, 进度={Progress}%", 
            progressInfo.TaskId, progressInfo.Progress);

        // 更新进度信息
        SharedDataService.SetData($"task:{progressInfo.TaskId}:progress", progressInfo.Progress);
        SharedDataService.SetData($"task:{progressInfo.TaskId}:last_update", DateTime.UtcNow);

        // 如果进度达到100%，准备完成任务
        if (progressInfo.Progress >= 100)
        {
            await PrepareTaskCompletion(progressInfo.TaskId);
        }
    }

    /// <summary>
    /// 处理任务完成消息
    /// </summary>
    private async Task HandleTaskCompleted(string message)
    {
        var completionInfo = DeserializeMessage<TaskCompletionInfo>(message);
        if (completionInfo == null)
        {
            Logger.LogError("解析任务完成消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("任务完成: 任务ID={TaskId}, 结果={Result}", 
            completionInfo.TaskId, completionInfo.Result);

        // 更新任务状态
        SharedDataService.SetData($"task:{completionInfo.TaskId}:status", "completed");
        SharedDataService.SetData($"task:{completionInfo.TaskId}:result", completionInfo.Result);
        SharedDataService.SetData($"task:{completionInfo.TaskId}:end_time", DateTime.UtcNow);

        // 清理任务相关的临时数据
        await CleanupTaskData(completionInfo.TaskId);
    }

    /// <summary>
    /// 处理任务取消消息
    /// </summary>
    private async Task HandleTaskCancelled(string message)
    {
        var cancellationInfo = DeserializeMessage<TaskCancellationInfo>(message);
        if (cancellationInfo == null)
        {
            Logger.LogError("解析任务取消消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("任务取消: 任务ID={TaskId}, 原因={Reason}", 
            cancellationInfo.TaskId, cancellationInfo.Reason);

        // 更新任务状态
        SharedDataService.SetData($"task:{cancellationInfo.TaskId}:status", "cancelled");
        SharedDataService.SetData($"task:{cancellationInfo.TaskId}:cancel_reason", cancellationInfo.Reason);
        SharedDataService.SetData($"task:{cancellationInfo.TaskId}:cancel_time", DateTime.UtcNow);

        // 停止任务执行并清理
        await StopTaskExecution(cancellationInfo.TaskId);
        await CleanupTaskData(cancellationInfo.TaskId);
    }

    /// <summary>
    /// 执行出库任务
    /// </summary>
    private async Task ExecuteOutboundTask(TaskExecuteInfo executeInfo)
    {
        try
        {
            // 获取任务详细信息
            var taskInfo = SharedDataService.GetData<OutboundTaskInfo>($"task:{executeInfo.TaskId}");
            if (taskInfo == null)
            {
                Logger.LogError("找不到任务信息: {TaskId}", executeInfo.TaskId);
                return;
            }

            // 发送设备控制指令
            await SendDeviceCommands(taskInfo);

            // 监控任务执行状态
            await MonitorTaskExecution(executeInfo.TaskId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "执行出库任务异常: {TaskId}", executeInfo.TaskId);
            await HandleTaskError(executeInfo.TaskId, ex.Message);
        }
    }

    /// <summary>
    /// 发送设备控制指令
    /// </summary>
    private async Task SendDeviceCommands(OutboundTaskInfo taskInfo)
    {
        // 发送运动控制指令
        var motionCommand = new
        {
            TaskId = taskInfo.TaskId,
            Action = "move_to_position",
            Position = taskInfo.TargetPosition,
            Speed = taskInfo.Speed ?? 100
        };
        await MqttService.PublishAsync("motion/command", SerializeObject(motionCommand));

        // 发送视觉检测指令
        var visionCommand = new
        {
            TaskId = taskInfo.TaskId,
            Action = "start_detection",
            Parameters = taskInfo.VisionParameters
        };
        await MqttService.PublishAsync("vision/command", SerializeObject(visionCommand));

        Logger.LogInformation("已发送设备控制指令: {TaskId}", taskInfo.TaskId);
    }

    /// <summary>
    /// 监控任务执行状态
    /// </summary>
    private async Task MonitorTaskExecution(string taskId)
    {
        // 这里可以实现任务执行监控逻辑
        // 例如定期检查设备状态、任务进度等
        Logger.LogInformation("开始监控任务执行: {TaskId}", taskId);
        
        // 示例：发送心跳消息
        var heartbeatMessage = new
        {
            TaskId = taskId,
            Status = "monitoring",
            Timestamp = DateTime.UtcNow
        };
        await MqttService.PublishAsync("outbound/task/heartbeat", SerializeObject(heartbeatMessage));
    }

    /// <summary>
    /// 准备任务完成
    /// </summary>
    private async Task PrepareTaskCompletion(string taskId)
    {
        Logger.LogInformation("准备完成任务: {TaskId}", taskId);
        
        // 发送任务即将完成的通知
        var notification = new
        {
            TaskId = taskId,
            Status = "completing",
            Timestamp = DateTime.UtcNow
        };
        await MqttService.PublishAsync("outbound/task/completing", SerializeObject(notification));
    }

    /// <summary>
    /// 处理任务错误
    /// </summary>
    private async Task HandleTaskError(string taskId, string errorMessage)
    {
        Logger.LogError("任务执行错误: 任务ID={TaskId}, 错误={Error}", taskId, errorMessage);

        // 更新任务状态
        SharedDataService.SetData($"task:{taskId}:status", "error");
        SharedDataService.SetData($"task:{taskId}:error", errorMessage);
        SharedDataService.SetData($"task:{taskId}:error_time", DateTime.UtcNow);

        // 发送错误通知
        var errorNotification = new
        {
            TaskId = taskId,
            Status = "error",
            Error = errorMessage,
            Timestamp = DateTime.UtcNow
        };
        await MqttService.PublishAsync("outbound/task/error", SerializeObject(errorNotification));
    }

    /// <summary>
    /// 停止任务执行
    /// </summary>
    private async Task StopTaskExecution(string taskId)
    {
        Logger.LogInformation("停止任务执行: {TaskId}", taskId);

        // 发送停止指令给相关设备
        var stopCommand = new
        {
            TaskId = taskId,
            Action = "stop"
        };

        await MqttService.PublishAsync("motion/stop", SerializeObject(stopCommand));
        await MqttService.PublishAsync("vision/stop", SerializeObject(stopCommand));
    }

    /// <summary>
    /// 清理任务数据
    /// </summary>
    private async Task CleanupTaskData(string taskId)
    {
        Logger.LogInformation("清理任务数据: {TaskId}", taskId);

        // 保留基本任务信息，清理临时数据
        var keys = SharedDataService.GetAllKeys()
            .Where(k => k.StartsWith($"task:{taskId}:") && 
                       (k.Contains("temp") || k.Contains("cache")))
            .ToList();

        foreach (var key in keys)
        {
            SharedDataService.RemoveData(key);
        }

        await Task.CompletedTask;
    }
}

#region Data Models

public class OutboundTaskInfo
{
    public string TaskId { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string TargetPosition { get; set; } = string.Empty;
    public int? Speed { get; set; }
    public Dictionary<string, object>? VisionParameters { get; set; }
    public DateTime CreatedTime { get; set; }
}

public class TaskExecuteInfo
{
    public string TaskId { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

public class TaskProgressInfo
{
    public string TaskId { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Description { get; set; }
}

public class TaskCompletionInfo
{
    public string TaskId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public Dictionary<string, object>? Data { get; set; }
}

public class TaskCancellationInfo
{
    public string TaskId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

#endregion 