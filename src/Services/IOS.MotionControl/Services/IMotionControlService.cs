namespace IOS.MotionControl.Services;

/// <summary>
/// 运动控制服务接口
/// </summary>
public interface IMotionControlService
{
    /// <summary>
    /// 是否已初始化
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// 当前位置
    /// </summary>
    int CurrentPosition { get; }

    /// <summary>
    /// 是否已启用
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// 初始化运动控制系统
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 绝对运动
    /// </summary>
    /// <param name="position">目标位置</param>
    /// <param name="speed">运动速度（可选）</param>
    Task MoveAbsoluteAsync(int position, uint? speed = null);

    /// <summary>
    /// 相对运动
    /// </summary>
    /// <param name="distance">移动距离</param>
    /// <param name="speed">运动速度（可选）</param>
    Task MoveRelativeAsync(int distance, uint? speed = null);

    /// <summary>
    /// 回零操作
    /// </summary>
    /// <param name="speed">运动速度（可选）</param>
    Task HomeAsync(uint? speed = null);

    /// <summary>
    /// 停止运动
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 获取运动状态
    /// </summary>
    Task<MotionStatus> GetStatusAsync();

    /// <summary>
    /// 关闭运动控制系统
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// 运动状态
/// </summary>
public class MotionStatus
{
    /// <summary>
    /// 当前位置
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// 是否已启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 是否在运动
    /// </summary>
    public bool IsMoving { get; set; }

    /// <summary>
    /// 是否有错误
    /// </summary>
    public bool HasError { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
} 