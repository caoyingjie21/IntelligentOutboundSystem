using IOS.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace IOS.Scheduler.Services;

/// <summary>
/// 任务调度服务实现
/// </summary>
public class TaskScheduleService : ITaskScheduleService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IMqttService _mqttService;
    private readonly ILogger<TaskScheduleService> _logger;
    private readonly Dictionary<string, ScheduleTask> _scheduleTasks = new();

    public TaskScheduleService(
        ISchedulerFactory schedulerFactory,
        IMqttService mqttService,
        ILogger<TaskScheduleService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _mqttService = mqttService;
        _logger = logger;
    }

    public async Task<string> CreateScheduleAsync(ScheduleTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);

            var jobKey = new JobKey(task.Id, "default");
            var triggerKey = new TriggerKey($"{task.Id}_trigger", "default");

            // 创建Job
            var jobDetail = JobBuilder.Create<GenericJob>()
                .WithIdentity(jobKey)
                .UsingJobData("TaskId", task.Id)
                .UsingJobData("JobType", task.JobType)
                .Build();

            // 创建Trigger
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(task.CronExpression)
                .Build();

            await scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

            // 保存任务信息
            _scheduleTasks[task.Id] = task;

            _logger.LogInformation("创建调度任务成功: {TaskId}, 名称: {TaskName}", task.Id, task.Name);
            return task.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建调度任务失败: {TaskName}", task.Name);
            throw;
        }
    }

    public async Task UpdateScheduleAsync(string taskId, ScheduleTask task, CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var triggerKey = new TriggerKey($"{taskId}_trigger", "default");

            // 删除旧的Trigger
            await scheduler.UnscheduleJob(triggerKey, cancellationToken);

            // 创建新的Trigger
            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(task.CronExpression)
                .Build();

            await scheduler.ScheduleJob(newTrigger, cancellationToken);

            // 更新任务信息
            _scheduleTasks[taskId] = task;

            _logger.LogInformation("更新调度任务成功: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新调度任务失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task DeleteScheduleAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobKey = new JobKey(taskId, "default");

            await scheduler.DeleteJob(jobKey, cancellationToken);
            _scheduleTasks.Remove(taskId);

            _logger.LogInformation("删除调度任务成功: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除调度任务失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task PauseScheduleAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobKey = new JobKey(taskId, "default");

            await scheduler.PauseJob(jobKey, cancellationToken);

            if (_scheduleTasks.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Paused;
            }

            _logger.LogInformation("暂停调度任务成功: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "暂停调度任务失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task ResumeScheduleAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobKey = new JobKey(taskId, "default");

            await scheduler.ResumeJob(jobKey, cancellationToken);

            if (_scheduleTasks.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Ready;
            }

            _logger.LogInformation("恢复调度任务成功: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "恢复调度任务失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task TriggerTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
            var jobKey = new JobKey(taskId, "default");

            await scheduler.TriggerJob(jobKey, cancellationToken);

            _logger.LogInformation("触发调度任务成功: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "触发调度任务失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<TaskStatus> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_scheduleTasks.TryGetValue(taskId, out var task))
            {
                return task.Status;
            }

            return TaskStatus.Ready;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务状态失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<IEnumerable<ScheduleTask>> GetAllSchedulesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return _scheduleTasks.Values.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有调度任务失败");
            throw;
        }
    }
}

/// <summary>
/// 通用任务Job
/// </summary>
public class GenericJob : IJob
{
    private readonly ILogger<GenericJob> _logger;
    private readonly IMqttService _mqttService;

    public GenericJob(ILogger<GenericJob> logger, IMqttService mqttService)
    {
        _logger = logger;
        _mqttService = mqttService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var taskId = context.JobDetail.JobDataMap.GetString("TaskId");
        var jobType = context.JobDetail.JobDataMap.GetString("JobType");

        _logger.LogInformation("开始执行任务: {TaskId}, 类型: {JobType}", taskId, jobType);

        try
        {
            // 根据任务类型执行不同的逻辑
            switch (jobType)
            {
                case "HeartbeatJob":
                    await ExecuteHeartbeatJob(taskId);
                    break;
                case "OutboundJob":
                    await ExecuteOutboundJob(taskId);
                    break;
                default:
                    _logger.LogWarning("未知的任务类型: {JobType}", jobType);
                    break;
            }

            _logger.LogInformation("任务执行完成: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "任务执行失败: {TaskId}", taskId);
            throw;
        }
    }

    private async Task ExecuteHeartbeatJob(string taskId)
    {
        // 发送心跳消息
        var heartbeatData = new
        {
            TaskId = taskId,
            Timestamp = DateTime.UtcNow,
            Status = "Running"
        };
        
        await _mqttService.PublishAsync("system/heartbeat", JsonSerializer.Serialize(heartbeatData));
    }

    private async Task ExecuteOutboundJob(string taskId)
    {
        // 执行出库任务逻辑
        var outboundData = new
        {
            TaskId = taskId,
            Timestamp = DateTime.UtcNow,
            Action = "Execute"
        };
        
        await _mqttService.PublishAsync("outbound/execute", JsonSerializer.Serialize(outboundData));
    }
}

/// <summary>
/// 心跳任务
/// </summary>
public class HeartbeatJob : IJob
{
    private readonly ILogger<HeartbeatJob> _logger;
    private readonly IMqttService _mqttService;

    public HeartbeatJob(ILogger<HeartbeatJob> logger, IMqttService mqttService)
    {
        _logger = logger;
        _mqttService = mqttService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogDebug("执行心跳任务");

            // 检查MQTT连接状态
            if (!_mqttService.IsConnected)
            {
                _logger.LogWarning("MQTT服务未连接，跳过心跳消息发送");
                return;
            }

            // 发送心跳消息
            var heartbeatData = new
            {
                Source = "Scheduler",
                Timestamp = DateTime.UtcNow,
                Status = "Running"
            };
            
            await _mqttService.PublishAsync("system/heartbeat", JsonSerializer.Serialize(heartbeatData));
            _logger.LogDebug("心跳消息发送成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "心跳任务执行失败");
            // 不重新抛出异常，避免任务调度器停止
        }
    }
} 