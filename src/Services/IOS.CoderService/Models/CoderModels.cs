namespace IOS.CoderService.Models;

/// <summary>
/// 条码信息
/// </summary>
public class CodeInfo
{
    /// <summary>
    /// 订单号
    /// </summary>
    public string Order { get; set; } = string.Empty;

    /// <summary>
    /// 条码数据
    /// </summary>
    public string Codes { get; set; } = string.Empty;

    /// <summary>
    /// 方向
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// 堆叠高度
    /// </summary>
    public double StackHeight { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 客户端连接信息
/// </summary>
public class ClientInfo
{
    /// <summary>
    /// 客户端终端点
    /// </summary>
    public string EndPoint { get; set; } = string.Empty;

    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后活动时间
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 接收的消息列表
    /// </summary>
    public List<string> Messages { get; set; } = new();
}

/// <summary>
/// 条码服务状态
/// </summary>
public class CoderServiceStatus
{
    /// <summary>
    /// 是否运行中
    /// </summary>
    public bool IsRunning { get; set; }

    /// <summary>
    /// 连接的客户端数量
    /// </summary>
    public int ConnectedClients { get; set; }

    /// <summary>
    /// 监听地址
    /// </summary>
    public string ListenAddress { get; set; } = string.Empty;

    /// <summary>
    /// 监听端口
    /// </summary>
    public int ListenPort { get; set; }

    /// <summary>
    /// MQTT连接状态
    /// </summary>
    public bool MqttConnected { get; set; }

    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 扫码启动请求
/// </summary>
public class ScanStartRequest
{
    /// <summary>
    /// 方向
    /// </summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>
    /// 堆叠高度
    /// </summary>
    public double StackHeight { get; set; }
} 