using Microsoft.AspNetCore.Mvc;
using IOS.MotionControl.Services;
using Microsoft.Extensions.Logging;

namespace IOS.MotionControl.Controllers;

/// <summary>
/// 运动控制API控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MotionController : ControllerBase
{
    private readonly IMotionControlService _motionControlService;
    private readonly ILogger<MotionController> _logger;

    public MotionController(IMotionControlService motionControlService, ILogger<MotionController> logger)
    {
        _motionControlService = motionControlService;
        _logger = logger;
    }

    /// <summary>
    /// 获取运动状态
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<MotionStatus>> GetStatus()
    {
        try
        {
            var status = await _motionControlService.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取运动状态失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 绝对运动
    /// </summary>
    /// <param name="request">运动请求</param>
    [HttpPost("move-absolute")]
    public async Task<ActionResult> MoveAbsolute([FromBody] MoveAbsoluteRequest request)
    {
        try
        {
            await _motionControlService.MoveAbsoluteAsync(request.Position, request.Speed);
            return Ok(new { Message = "运动命令已执行", Position = request.Position });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行绝对运动失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 相对运动
    /// </summary>
    /// <param name="request">运动请求</param>
    [HttpPost("move-relative")]
    public async Task<ActionResult> MoveRelative([FromBody] MoveRelativeRequest request)
    {
        try
        {
            await _motionControlService.MoveRelativeAsync(request.Distance, request.Speed);
            return Ok(new { Message = "相对运动命令已执行", Distance = request.Distance });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行相对运动失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 回零
    /// </summary>
    /// <param name="request">回零请求</param>
    [HttpPost("home")]
    public async Task<ActionResult> Home([FromBody] HomeRequest? request = null)
    {
        try
        {
            await _motionControlService.HomeAsync(request?.Speed);
            return Ok(new { Message = "回零命令已执行" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行回零失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 停止运动
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult> Stop()
    {
        try
        {
            await _motionControlService.StopAsync();
            return Ok(new { Message = "停止命令已执行" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行停止失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 初始化系统
    /// </summary>
    [HttpPost("initialize")]
    public async Task<ActionResult> Initialize()
    {
        try
        {
            if (_motionControlService.IsInitialized)
            {
                return BadRequest(new { Error = "系统已经初始化" });
            }

            await _motionControlService.InitializeAsync();
            return Ok(new { Message = "系统初始化成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化系统失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

/// <summary>
/// 绝对运动请求
/// </summary>
public class MoveAbsoluteRequest
{
    /// <summary>
    /// 目标位置
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// 运动速度（可选）
    /// </summary>
    public uint? Speed { get; set; }
}

/// <summary>
/// 相对运动请求
/// </summary>
public class MoveRelativeRequest
{
    /// <summary>
    /// 运动距离
    /// </summary>
    public int Distance { get; set; }

    /// <summary>
    /// 运动速度（可选）
    /// </summary>
    public uint? Speed { get; set; }
}

/// <summary>
/// 回零请求
/// </summary>
public class HomeRequest
{
    /// <summary>
    /// 运动速度（可选）
    /// </summary>
    public uint? Speed { get; set; }
} 