using Microsoft.AspNetCore.Mvc;
using IOS.Scheduler.Services;

namespace IOS.Scheduler.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SchedulerController : ControllerBase
{
    private readonly ILogger<SchedulerController> _logger;
    private readonly SharedDataService _sharedDataService;
    private readonly ITaskScheduleService _taskScheduleService;

    public SchedulerController(
        ILogger<SchedulerController> logger,
        SharedDataService sharedDataService,
        ITaskScheduleService taskScheduleService)
    {
        _logger = logger;
        _sharedDataService = sharedDataService;
        _taskScheduleService = taskScheduleService;
    }

    /// <summary>
    /// 获取系统状态
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        try
        {
            var status = new
            {
                ServiceName = "IOS.Scheduler",
                Status = "Running",
                Timestamp = DateTime.Now,
                Version = "1.0.0",
                SharedDataCount = _sharedDataService.GetDataCount(),
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取系统状态失败");
            return StatusCode(500, new { error = "获取系统状态失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取共享数据
    /// </summary>
    [HttpGet("shared-data")]
    public IActionResult GetSharedData()
    {
        try
        {
            var data = _sharedDataService.GetAllData();
            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取共享数据失败");
            return StatusCode(500, new { error = "获取共享数据失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 获取任务列表
    /// </summary>
    [HttpGet("tasks")]
    public IActionResult GetTasks()
    {
        try
        {
            var tasks = 1;
            return Ok(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取任务列表失败");
            return StatusCode(500, new { error = "获取任务列表失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 创建新任务
    /// </summary>
    [HttpPost("tasks")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.TaskType))
            {
                return BadRequest(new { error = "任务类型不能为空" });
            }

            return Ok(new { message = "任务创建成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建任务失败");
            return StatusCode(500, new { error = "创建任务失败", message = ex.Message });
        }
    }

    /// <summary>
    /// 发送MQTT消息
    /// </summary>
    [HttpPost("mqtt/send")]
    public IActionResult SendMqttMessage([FromBody] MqttMessageRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Topic) || string.IsNullOrEmpty(request.Message))
            {
                return BadRequest(new { error = "主题和消息内容不能为空" });
            }

            // 这里需要通过SharedDataService或其他方式发送MQTT消息
            _sharedDataService.SetData($"mqtt_send_{DateTime.Now:yyyyMMddHHmmss}", new
            {
                Topic = request.Topic,
                Message = request.Message,
                Timestamp = DateTime.Now
            });

            return Ok(new { message = "MQTT消息发送成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送MQTT消息失败");
            return StatusCode(500, new { error = "发送MQTT消息失败", message = ex.Message });
        }
    }
}

public class CreateTaskRequest
{
    public string TaskType { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
}

public class MqttMessageRequest
{
    public string Topic { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
} 