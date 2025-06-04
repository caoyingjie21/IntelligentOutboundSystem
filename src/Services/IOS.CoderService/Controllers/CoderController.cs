using Microsoft.AspNetCore.Mvc;
using IOS.CoderService.Services;
using IOS.CoderService.Models;
using Microsoft.Extensions.Logging;

namespace IOS.CoderService.Controllers;

/// <summary>
/// 条码服务API控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CoderController : ControllerBase
{
    private readonly ICoderService _coderService;
    private readonly ILogger<CoderController> _logger;

    public CoderController(ICoderService coderService, ILogger<CoderController> logger)
    {
        _coderService = coderService;
        _logger = logger;
    }

    /// <summary>
    /// 获取服务状态
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<object>> GetStatus()
    {
        try
        {
            var status = await _coderService.GetStatusAsync();
            var clients = await _coderService.GetConnectedClientsAsync();
            
            var detailedStatus = new
            {
                ServiceStatus = status,
                SocketServerRunning = _coderService.IsRunning,
                ConnectedClientsCount = _coderService.ConnectedClientsCount,
                ClientDetails = clients.Select(c => new 
                {
                    c.EndPoint,
                    c.ConnectedAt,
                    c.LastActivity,
                    MessageCount = c.Messages.Count
                }).ToList(),
                Timestamp = DateTime.UtcNow
            };
            
            _logger.LogDebug("状态查询 - Socket运行: {IsRunning}, 客户端数: {ClientCount}", 
                _coderService.IsRunning, _coderService.ConnectedClientsCount);
                
            return Ok(detailedStatus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取服务状态失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 获取连接的客户端列表
    /// </summary>
    [HttpGet("clients")]
    public async Task<ActionResult<IReadOnlyList<ClientInfo>>> GetClients()
    {
        try
        {
            var clients = await _coderService.GetConnectedClientsAsync();
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取客户端列表失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 启动扫码任务
    /// </summary>
    /// <param name="request">扫码请求</param>
    [HttpPost("start-scanning")]
    public async Task<ActionResult<CodeInfo>> StartScanning([FromBody] ScanStartRequest request)
    {
        try
        {
            var codeInfo = await _coderService.StartScanningAsync(request.Direction, request.StackHeight);
            return Ok(codeInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动扫码任务失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 收集条码数据
    /// </summary>
    /// <param name="timeoutMs">超时时间（毫秒），默认5000ms</param>
    [HttpPost("collect-codes")]
    public async Task<ActionResult<Dictionary<string, string>>> CollectCodes([FromQuery] int timeoutMs = 5000)
    {
        try
        {
            var codes = await _coderService.CollectCodesAsync(timeoutMs: timeoutMs);
            return Ok(codes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "收集条码数据失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 断开指定客户端连接
    /// </summary>
    /// <param name="clientEndPoint">客户端终端点</param>
    [HttpDelete("clients/{clientEndPoint}")]
    public async Task<ActionResult> DisconnectClient(string clientEndPoint)
    {
        try
        {
            await _coderService.DisconnectClientAsync(clientEndPoint);
            return Ok(new { Message = $"客户端 {clientEndPoint} 已断开连接" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "断开客户端连接失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 清空消息队列
    /// </summary>
    [HttpPost("clear-queue")]
    public async Task<ActionResult> ClearMessageQueue()
    {
        try
        {
            await _coderService.ClearMessageQueueAsync();
            return Ok(new { Message = "消息队列已清空" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空消息队列失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 发送消息到指定客户端
    /// </summary>
    /// <param name="clientEndPoint">客户端终端点</param>
    /// <param name="request">消息请求</param>
    [HttpPost("clients/{clientEndPoint}/send")]
    public async Task<ActionResult> SendMessageToClient(string clientEndPoint, [FromBody] SendMessageRequest request)
    {
        try
        {
            await _coderService.SendMessageToClientAsync(clientEndPoint, request.Message);
            return Ok(new { Message = $"消息已发送到客户端 {clientEndPoint}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送消息到客户端失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 广播消息到所有客户端
    /// </summary>
    /// <param name="request">消息请求</param>
    [HttpPost("broadcast")]
    public async Task<ActionResult> BroadcastMessage([FromBody] SendMessageRequest request)
    {
        try
        {
            await _coderService.BroadcastMessageAsync(request.Message);
            return Ok(new { Message = $"消息已广播到 {_coderService.ConnectedClientsCount} 个客户端" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "广播消息失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 启动服务
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult> StartService()
    {
        try
        {
            if (_coderService.IsRunning)
            {
                return BadRequest(new { Error = "服务已经运行中" });
            }

            await _coderService.StartAsync();
            return Ok(new { Message = "服务启动成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动服务失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    /// <summary>
    /// 停止服务
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult> StopService()
    {
        try
        {
            if (!_coderService.IsRunning)
            {
                return BadRequest(new { Error = "服务未运行" });
            }

            await _coderService.StopAsync();
            return Ok(new { Message = "服务停止成功" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止服务失败");
            return StatusCode(500, new { Error = ex.Message });
        }
    }
}

/// <summary>
/// 发送消息请求
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// 消息内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
} 