using IOS.Shared.Messages;
using IOS.Shared.Configuration;

namespace IOS.Infrastructure.Messaging;

/// <summary>
/// MQTT服务接口
/// </summary>
public interface IMqttService : IDisposable
{
    /// <summary>
    /// 消息接收事件
    /// </summary>
    event Func<string, string, Task>? OnMessageReceived;

    /// <summary>
    /// 连接状态变更事件
    /// </summary>
    event Func<bool, Task>? OnConnectionChanged;

    /// <summary>
    /// 启动MQTT服务
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止MQTT服务
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布消息
    /// </summary>
    Task PublishAsync<T>(string topic, StandardMessage<T> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 发布原始消息
    /// </summary>
    Task PublishAsync(string topic, string payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// 订阅主题
    /// </summary>
    Task SubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// 取消订阅主题
    /// </summary>
    Task UnsubscribeAsync(string topic, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取连接状态
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 健康检查
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 增强的MQTT服务接口
/// </summary>
public interface IEnhancedMqttService : IMqttService
{
    /// <summary>
    /// 使用主题键发布消息
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    /// <param name="topicKey">主题键</param>
    /// <param name="data">消息数据</param>
    /// <param name="priority">消息优先级</param>
    /// <param name="correlationId">关联ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发布是否成功</returns>
    Task<bool> PublishAsync<T>(
        string topicKey, 
        T data, 
        MessagePriority priority = MessagePriority.Normal,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用主题键订阅消息
    /// </summary>
    /// <typeparam name="T">消息数据类型</typeparam>
    /// <param name="topicKey">主题键</param>
    /// <param name="handler">消息处理器</param>
    /// <param name="filterType">消息类型过滤</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SubscribeAsync<T>(
        string topicKey, 
        Func<StandardMessage<T>, Task> handler,
        MessageType? filterType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量发布消息
    /// </summary>
    /// <param name="messages">消息列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>发布结果</returns>
    Task<BatchPublishResult> PublishBatchAsync(
        IEnumerable<(string topic, string payload)> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取服务统计信息
    /// </summary>
    /// <returns>统计信息</returns>
    MqttServiceStatistics GetStatistics();
}

/// <summary>
/// MQTT配置选项
/// </summary>
public class MqttOptions
{
    public const string SectionName = "Mqtt";

    /// <summary>
    /// MQTT服务器地址
    /// </summary>
    public string Broker { get; set; } = "localhost";

    /// <summary>
    /// MQTT服务器端口
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// 客户端ID
    /// </summary>
    public string ClientId { get; set; } = Environment.MachineName;

    /// <summary>
    /// 用户名
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 保持连接间隔（秒）
    /// </summary>
    public int KeepAlivePeriod { get; set; } = 30;

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 10;

    /// <summary>
    /// 重连间隔（秒）
    /// </summary>
    public int ReconnectInterval { get; set; } = 5;

    /// <summary>
    /// 最大重连次数
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 5;

    /// <summary>
    /// 是否使用TLS
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// 清除会话
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// 主题配置
    /// </summary>
    public MqttTopicOptions Topics { get; set; } = new();
}

/// <summary>
/// 基础配置
/// </summary>
public class BaseOptions 
{
    public const string SectionName = "Sample";

    public double HeightInit { get; set; } = 0; // 起始高度

    public double TrayHeight { get; set; } = 0; // 托盘高度

    public double CameraHeight {  get; set; } = 0; // 相机高度

    public double CoderHeight { get; set; } = 0; // 读码器高度
}

/// <summary>
/// MQTT主题配置选项
/// </summary>
public class MqttTopicOptions
{
    /// <summary>
    /// 订阅主题列表
    /// </summary>
    public List<string> Subscribe { get; set; } = new();

    /// <summary>
    /// 发布主题配置
    /// </summary>
    public Dictionary<string, string> Publish { get; set; } = new();
}

/// <summary>
/// 批量发布结果
/// </summary>
public class BatchPublishResult
{
    /// <summary>
    /// 成功发布的消息数量
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败的消息数量
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// 失败的消息详情
    /// </summary>
    public List<(string topic, string error)> Failures { get; set; } = new();

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool IsAllSuccess => FailureCount == 0;
}

/// <summary>
/// MQTT服务统计信息
/// </summary>
public class MqttServiceStatistics
{
    /// <summary>
    /// 连接时间
    /// </summary>
    public DateTime? ConnectedAt { get; set; }

    /// <summary>
    /// 发布消息总数
    /// </summary>
    public long PublishedMessages { get; set; }

    /// <summary>
    /// 接收消息总数
    /// </summary>
    public long ReceivedMessages { get; set; }

    /// <summary>
    /// 订阅主题数量
    /// </summary>
    public int SubscribedTopics { get; set; }

    /// <summary>
    /// 重连次数
    /// </summary>
    public int ReconnectCount { get; set; }

    /// <summary>
    /// 最后一次消息时间
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// 连接状态
    /// </summary>
    public bool IsConnected { get; set; }
} 