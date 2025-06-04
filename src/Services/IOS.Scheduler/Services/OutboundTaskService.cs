using IOS.Infrastructure.Messaging;
using IOS.Shared.Messages;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IOS.Scheduler.Services;

/// <summary>
/// 出库任务服务实现
/// </summary>
public class OutboundTaskService : IOutboundTaskService
{
    private readonly IMqttService _mqttService;
    private readonly ILogger<OutboundTaskService> _logger;
    private readonly ConcurrentDictionary<string, OutboundTask> _tasks = new();
    private readonly ConcurrentDictionary<string, OutboundTaskProgress> _taskProgress = new();

    public OutboundTaskService(IMqttService mqttService, ILogger<OutboundTaskService> logger)
    {
        _mqttService = mqttService;
        _logger = logger;
    }

    public async Task<string> CreateOutboundTaskAsync(OutboundTaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var task = new OutboundTask
            {
                Id = Guid.NewGuid().ToString(),
                OrderId = request.OrderId,
                ProductCode = request.ProductCode,
                Quantity = request.Quantity,
                TargetLocation = request.TargetLocation,
                Priority = request.Priority,
                Status = TaskStatus.Ready,
                Parameters = request.Parameters,
                CreatedAt = DateTime.UtcNow
            };

            _tasks[task.Id] = task;

            // 初始化进度
            _taskProgress[task.Id] = new OutboundTaskProgress
            {
                TaskId = task.Id,
                Status = TaskStatus.Ready,
                ProgressPercentage = 0,
                CurrentStep = "任务已创建",
                LastUpdated = DateTime.UtcNow
            };

            // 发送任务创建消息
            var message = new StandardMessage<OutboundTask>
            {
                Source = "Scheduler",
                Target = "DataMiddleware",
                Type = MessageType.Command,
                Priority = Priority.Normal,
                Data = task
            };

            await _mqttService.PublishAsync("outbound/task/created", message, cancellationToken);

            _logger.LogInformation("出库任务创建成功: {TaskId}, 订单: {OrderId}", task.Id, request.OrderId);
            return task.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建出库任务失败: {OrderId}", request.OrderId);
            throw;
        }
    }

    public async Task ExecuteOutboundTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                throw new ArgumentException($"任务不存在: {taskId}");
            }

            if (task.Status != TaskStatus.Ready)
            {
                throw new InvalidOperationException($"任务状态不正确: {task.Status}");
            }

            // 更新任务状态
            task.Status = TaskStatus.Running;
            task.StartedAt = DateTime.UtcNow;

            // 更新进度
            UpdateTaskProgress(taskId, TaskStatus.Running, 10, "开始执行出库任务");

            // 发送任务执行消息
            var message = new StandardMessage<OutboundTask>
            {
                Source = "Scheduler",
                Target = "All",
                Type = MessageType.Command,
                Priority = ConvertPriority(task.Priority),
                Data = task
            };

            await _mqttService.PublishAsync("outbound/task/execute", message, cancellationToken);

            // 模拟任务执行步骤
            await SimulateTaskExecution(taskId, cancellationToken);

            _logger.LogInformation("出库任务执行完成: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行出库任务失败: {TaskId}", taskId);

            // 更新任务状态为失败
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.Status = TaskStatus.Failed;
                task.ErrorMessage = ex.Message;
                UpdateTaskProgress(taskId, TaskStatus.Failed, 0, $"任务执行失败: {ex.Message}");
            }

            throw;
        }
    }

    public async Task CancelOutboundTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                throw new ArgumentException($"任务不存在: {taskId}");
            }

            if (task.Status == TaskStatus.Completed)
            {
                throw new InvalidOperationException("已完成的任务无法取消");
            }

            task.Status = TaskStatus.Cancelled;
            UpdateTaskProgress(taskId, TaskStatus.Cancelled, 0, "任务已取消");

            // 发送取消消息
            var message = new StandardMessage<OutboundTask>
            {
                Source = "Scheduler",
                Target = "All",
                Type = MessageType.Command,
                Priority = Priority.High,
                Data = task
            };

            await _mqttService.PublishAsync("outbound/task/cancelled", message, cancellationToken);

            _logger.LogInformation("出库任务已取消: {TaskId}", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消出库任务失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<OutboundTaskProgress> GetTaskProgressAsync(string taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_taskProgress.TryGetValue(taskId, out var progress))
            {
                return progress;
            }

            return new OutboundTaskProgress
            {
                TaskId = taskId,
                Status = TaskStatus.Ready,
                ProgressPercentage = 0,
                CurrentStep = "任务不存在",
                LastUpdated = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务进度失败: {TaskId}", taskId);
            throw;
        }
    }

    public async Task<IEnumerable<OutboundTask>> GetPendingTasksAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return _tasks.Values
                .Where(t => t.Status == TaskStatus.Ready)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.CreatedAt)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取待执行任务失败");
            throw;
        }
    }

    private void UpdateTaskProgress(string taskId, TaskStatus status, int progressPercentage, string currentStep, string? message = null)
    {
        var progress = _taskProgress.GetOrAdd(taskId, new OutboundTaskProgress { TaskId = taskId });
        progress.Status = status;
        progress.ProgressPercentage = progressPercentage;
        progress.CurrentStep = currentStep;
        progress.Message = message ?? currentStep;
        progress.LastUpdated = DateTime.UtcNow;
    }

    private async Task SimulateTaskExecution(string taskId, CancellationToken cancellationToken)
    {
        var steps = new[]
        {
            ("库存检查", 20),
            ("读取二维码", 40),
            ("运动控制", 60),
            ("视觉检测", 80),
            ("完成出库", 100)
        };

        foreach (var (stepName, progress) in steps)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            UpdateTaskProgress(taskId, TaskStatus.Running, progress, stepName);

            // 发送进度更新消息
            var progressMessage = new StandardMessage<OutboundTaskProgress>
            {
                Source = "Scheduler",
                Target = "UI",
                Type = MessageType.Event,
                Priority = Priority.Normal,
                Data = _taskProgress[taskId]
            };

            await _mqttService.PublishAsync("outbound/task/progress", progressMessage, cancellationToken);

            // 模拟步骤执行时间
            await Task.Delay(1000, cancellationToken);
        }

        // 任务完成
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = TaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            UpdateTaskProgress(taskId, TaskStatus.Completed, 100, "任务已完成");

            // 发送完成消息
            var completedMessage = new StandardMessage<OutboundTask>
            {
                Source = "Scheduler",
                Target = "All",
                Type = MessageType.Event,
                Priority = Priority.Normal,
                Data = task
            };

            await _mqttService.PublishAsync("outbound/task/completed", completedMessage, cancellationToken);
        }
    }

    private Priority ConvertPriority(TaskPriority taskPriority)
    {
        return taskPriority switch
        {
            TaskPriority.Low => Priority.Low,
            TaskPriority.Normal => Priority.Normal,
            TaskPriority.High => Priority.High,
            TaskPriority.Critical => Priority.Critical,
            _ => Priority.Normal
        };
    }
} 