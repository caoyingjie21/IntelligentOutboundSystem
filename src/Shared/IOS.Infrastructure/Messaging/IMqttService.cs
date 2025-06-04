using IOS.Shared.Messages;

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
} 