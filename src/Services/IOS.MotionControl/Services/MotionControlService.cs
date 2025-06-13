using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IOS.MotionControl.Configuration;
using Core.Net.EtherCAT;
using Core.Net.EtherCAT.SeedWork;

namespace IOS.MotionControl.Services;

/// <summary>
/// 运动控制服务实现
/// </summary>
public class MotionControlService : IMotionControlService, IDisposable
{
    private readonly ILogger<MotionControlService> _logger;
    private readonly MotionControlOptions _options;
    private readonly EtherCATMaster _etherCATMaster;
    private IEtherCATSlave_CiA402? _axis;
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    /// 是否已启用
    /// </summary>
    public bool IsEnabled => _isInitialized && _axis != null;

    /// <summary>
    /// 当前位置
    /// </summary>
    public int CurrentPosition => _axis?.PositionActualValue ?? 0;

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized => _isInitialized;

    public MotionControlService(
        ILogger<MotionControlService> logger,
        IOptions<MotionControlOptions> options,
        EtherCATMaster etherCATMaster)
    {
        _logger = logger;
        _options = options.Value;
        _etherCATMaster = etherCATMaster;
    }

    /// <summary>
    /// 初始化运动控制系统
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("运动控制系统已经初始化");
            return;
        }

        try
        {
            //_logger.LogInformation("开始初始化EtherCAT运动控制系统");

            //// 检查网络接口配置
            //if (string.IsNullOrEmpty(_options.EtherNet))
            //{
            //    throw new InvalidOperationException("请配置EtherNet地址");
            //}

            //// 创建轴实例
            //_axis = new EtherCATSlave_CiA402_1(_etherCATMaster, _options.SlaveId);

            //// 启动EtherCAT主站
            //_etherCATMaster.StartActivity(_options.EtherNet);
            //await Task.Delay(500);

            //// 配置轴参数
            //_etherCATMaster.WriteSDO<uint>(1, 0x6091, 0x01, 1);
            //_etherCATMaster.WriteSDO<uint>(1, 0x6091, 0x02, 1);
            //_etherCATMaster.WriteSDO<uint>(1, 0x6092, 0x01, 1000);
            //_etherCATMaster.WriteSDO<uint>(1, 0x6092, 0x02, 1);

            //// 复位并上电
            //_axis.Reset();
            //_axis.PowerOn();

            _isInitialized = true;
            _logger.LogInformation("EtherCAT运动控制系统初始化完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "初始化EtherCAT运动控制系统失败");
            throw;
        }
    }

    /// <summary>
    /// 绝对运动
    /// </summary>
    public async Task MoveAbsoluteAsync(int position, uint? speed = null)
    {
        ThrowIfNotInitialized();

        if (position < _options.MinPosition || position > _options.MaxPosition)
        {
            throw new ArgumentOutOfRangeException(nameof(position), 
                $"位置必须在 {_options.MinPosition} 到 {_options.MaxPosition} 之间");
        }

        var moveSpeed = speed ?? _options.Speed;
        var acceleration = moveSpeed * 10;
        var deceleration = moveSpeed * 10;
        var startPosition = CurrentPosition;

        _logger.LogInformation("开始绝对运动: 目标位置={Position}, 速度={Speed}", position, moveSpeed);

        try
        {
            _axis!.MoveAbsolute(position, moveSpeed, acceleration, deceleration);
            
            // 估算运动时间并等待
            var estimatedTime = Math.Abs(startPosition - position) / (moveSpeed / 1000.0);
            await Task.Delay((int)estimatedTime);

            _logger.LogInformation("绝对运动完成: 当前位置={Position}", CurrentPosition);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "绝对运动失败");
            throw;
        }
    }

    /// <summary>
    /// 相对运动
    /// </summary>
    public async Task MoveRelativeAsync(int distance, uint? speed = null)
    {
        ThrowIfNotInitialized();

        var targetPosition = CurrentPosition + distance;
        await MoveAbsoluteAsync(targetPosition, speed);
    }

    /// <summary>
    /// 回零
    /// </summary>
    public async Task HomeAsync(uint? speed = null)
    {
        ThrowIfNotInitialized();

        _logger.LogInformation("开始回零操作");
        await MoveAbsoluteAsync(0, speed);
        _logger.LogInformation("回零操作完成");
    }

    /// <summary>
    /// 停止运动
    /// </summary>
    public async Task StopAsync()
    {
        ThrowIfNotInitialized();

        try
        {
            _logger.LogInformation("停止轴运动");
            _axis!.Stop(_options.Speed * 10);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止运动失败");
            throw;
        }
    }

    /// <summary>
    /// 获取运动状态
    /// </summary>
    public async Task<MotionStatus> GetStatusAsync()
    {
        await Task.CompletedTask;

 //|| _axis == null
        if (!_isInitialized)
        {
            return new MotionStatus
            {
                Position = 0,
                IsEnabled = false,
                IsMoving = false,
                HasError = true,
                ErrorMessage = "系统未初始化",
                Timestamp = DateTime.UtcNow
            };
        }

        return new MotionStatus
        {
            Position = CurrentPosition,
            IsEnabled = IsEnabled,
            IsMoving = false,
            HasError = false,
            ErrorMessage = null,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 关闭运动控制系统
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (!_isInitialized || _axis == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("开始关闭运动控制系统");

            // 如果不在零位，先回零
            if (CurrentPosition != 0)
            {
                _logger.LogInformation("轴不在零位，执行回零操作");
                await HomeAsync();
            }

            // 断电
            await Task.Delay(500);
            _axis.PowerOff();
            await Task.Delay(500);

            _isInitialized = false;
            _logger.LogInformation("运动控制系统已关闭");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "关闭运动控制系统时发生错误");
        }
    }

    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    private void ThrowIfNotInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("运动控制系统尚未初始化，请先调用 InitializeAsync()");
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // 使用超时机制确保关闭操作不会无限等待
            var shutdownTask = ShutdownAsync();
            if (!shutdownTask.Wait(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("关闭运动控制系统超时");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放运动控制服务资源时发生错误");
        }

        _disposed = true;
    }
} 