using IOS.Shared.Models;

namespace IOS.Scheduler.Services;

/// <summary>
/// 任务调度服务接口
/// </summary>
public interface ITaskScheduleService
{
    /// <summary>
    /// 创建调度任务
    /// </summary>
    Task<string> CreateScheduleAsync(ScheduleTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新调度任务
    /// </summary>
    Task UpdateScheduleAsync(string taskId, ScheduleTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除调度任务
    /// </summary>
    Task DeleteScheduleAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 暂停调度任务
    /// </summary>
    Task PauseScheduleAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 恢复调度任务
    /// </summary>
    Task ResumeScheduleAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 立即执行任务
    /// </summary>
    Task TriggerTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取任务状态
    /// </summary>
    Task<TaskStatus> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有调度任务
    /// </summary>
    Task<IEnumerable<ScheduleTask>> GetAllSchedulesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 出库任务服务接口
/// </summary>
public interface IOutboundTaskService
{
    /// <summary>
    /// 创建出库任务
    /// </summary>
    Task<string> CreateOutboundTaskAsync(OutboundTaskRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行出库任务
    /// </summary>
    Task ExecuteOutboundTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消出库任务
    /// </summary>
    Task CancelOutboundTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取任务进度
    /// </summary>
    Task<OutboundTaskProgress> GetTaskProgressAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取待执行任务
    /// </summary>
    Task<IEnumerable<OutboundTask>> GetPendingTasksAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 调度任务
/// </summary>
public class ScheduleTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextRunTime { get; set; }
    public DateTime? LastRunTime { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Ready;
}

/// <summary>
/// 出库任务请求
/// </summary>
public class OutboundTaskRequest
{
    public string OrderId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string TargetLocation { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// 出库任务
/// </summary>
public class OutboundTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string TargetLocation { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public TaskStatus Status { get; set; } = TaskStatus.Ready;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// 出库任务进度
/// </summary>
public class OutboundTaskProgress
{
    public string TaskId { get; set; } = string.Empty;
    public TaskStatus Status { get; set; }
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 任务状态
/// </summary>
public enum TaskStatus
{
    Ready,      // 就绪
    Running,    // 执行中
    Completed,  // 已完成
    Failed,     // 失败
    Cancelled,  // 已取消
    Paused      // 已暂停
}

/// <summary>
/// 任务优先级
/// </summary>
public enum TaskPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
    Critical = 4
} 