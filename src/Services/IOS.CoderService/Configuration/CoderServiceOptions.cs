namespace IOS.CoderService.Configuration;

/// <summary>
/// 条码服务配置选项
/// </summary>
public class CoderServiceOptions
{
    public const string SectionName = "CoderService";

    /// <summary>
    /// Socket监听地址
    /// </summary>
    public string SocketAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Socket监听端口
    /// </summary>
    public int SocketPort { get; set; } = 5000;

    /// <summary>
    /// 最大客户端连接数
    /// </summary>
    public int MaxClients { get; set; } = 10;

    /// <summary>
    /// 接收缓冲区大小
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 1024;

    /// <summary>
    /// 客户端超时时间（毫秒）
    /// </summary>
    public int ClientTimeout { get; set; } = 30000;

    /// <summary>
    /// MQTT主题配置
    /// </summary>
    public TopicConfiguration Topics { get; set; } = new();
}

/// <summary>
/// 主题配置
/// </summary>
public class TopicConfiguration
{
    /// <summary>
    /// 接收主题
    /// </summary>
    public ReceiveTopics Receives { get; set; } = new();

    /// <summary>
    /// 发送主题
    /// </summary>
    public SendTopics Sends { get; set; } = new();
}

/// <summary>
/// 接收主题配置
/// </summary>
public class ReceiveTopics
{
    /// <summary>
    /// 启动扫码主题
    /// </summary>
    public string Start { get; set; } = "coder/start";

    /// <summary>
    /// 配置主题
    /// </summary>
    public string Config { get; set; } = "coder/config";

    /// <summary>
    /// 停止主题
    /// </summary>
    public string Stop { get; set; } = "coder/stop";

    /// <summary>
    /// 订单主题
    /// </summary>
    public string Order { get; set; } = "order";
}

/// <summary>
/// 发送主题配置
/// </summary>
public class SendTopics
{
    /// <summary>
    /// 条码数据主题
    /// </summary>
    public string Coder { get; set; } = "coder/odoo";

    /// <summary>
    /// 获取订单主题
    /// </summary>
    public string Order { get; set; } = "get_order";

    /// <summary>
    /// 状态主题
    /// </summary>
    public string Status { get; set; } = "coder/status";
} 