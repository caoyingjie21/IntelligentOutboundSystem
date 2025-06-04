namespace IOS.MotionControl.Configuration;

/// <summary>
/// 运动控制配置选项
/// </summary>
public class MotionControlOptions
{
    public const string SectionName = "MotionControl";

    /// <summary>
    /// EtherCAT网络接口名称
    /// </summary>
    public string EtherNet { get; set; } = "CNet";

    /// <summary>
    /// 运动速度
    /// </summary>
    public uint Speed { get; set; } = 50000;

    /// <summary>
    /// 最小位置
    /// </summary>
    public int MinPosition { get; set; } = 0;

    /// <summary>
    /// 最大位置
    /// </summary>
    public int MaxPosition { get; set; } = 220000;

    /// <summary>
    /// 从站ID
    /// </summary>
    public int SlaveId { get; set; } = 1;

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
    /// 运动命令主题
    /// </summary>
    public string Moving { get; set; } = "motion/moving";

    /// <summary>
    /// 回零命令主题
    /// </summary>
    public string Back { get; set; } = "motion/back";

    /// <summary>
    /// 配置命令主题
    /// </summary>
    public string Config { get; set; } = "motion/config";
}

/// <summary>
/// 发送主题配置
/// </summary>
public class SendTopics
{
    /// <summary>
    /// 运动完成主题
    /// </summary>
    public string MovingComplete { get; set; } = "motion/moving/complete";

    /// <summary>
    /// 位置信息主题
    /// </summary>
    public string Position { get; set; } = "motion/position";

    /// <summary>
    /// 状态信息主题
    /// </summary>
    public string Status { get; set; } = "motion/status";
} 