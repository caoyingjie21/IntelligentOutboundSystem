using System.Text.Json.Serialization;

namespace IOS.Shared.Messages;

/// <summary>
/// 标准化消息封装
/// </summary>
/// <typeparam name="T">消息数据类型</typeparam>
public class StandardMessage<T>
{
    /// <summary>
    /// 消息唯一标识
    /// </summary>
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 消息版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "v1";

    /// <summary>
    /// 消息时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 消息来源服务信息
    /// </summary>
    [JsonPropertyName("source")]
    public ServiceInfo Source { get; set; } = new();

    /// <summary>
    /// 消息目标服务信息
    /// </summary>
    [JsonPropertyName("target")]
    public ServiceInfo? Target { get; set; }

    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    /// <summary>
    /// 消息优先级
    /// </summary>
    [JsonPropertyName("priority")]
    public MessagePriority Priority { get; set; } = MessagePriority.Normal;

    /// <summary>
    /// 关联ID，用于追踪相关消息
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 消息数据
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// 消息元数据
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// 消息头部信息（保持向后兼容）
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// 消息过期时间
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 重试次数
    /// </summary>
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    [JsonPropertyName("maxRetries")]
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// 服务信息
/// </summary>
public class ServiceInfo
{
    /// <summary>
    /// 服务名称
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// 服务实例标识
    /// </summary>
    [JsonPropertyName("instance")]
    public string Instance { get; set; } = Environment.MachineName;

    /// <summary>
    /// 服务版本
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 服务环境
    /// </summary>
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "Production";
}

/// <summary>
/// 消息类型枚举
/// </summary>
public enum MessageType
{
    /// <summary>
    /// 命令消息
    /// </summary>
    Command,
    
    /// <summary>
    /// 事件消息
    /// </summary>
    Event,
    
    /// <summary>
    /// 请求消息
    /// </summary>
    Request,
    
    /// <summary>
    /// 响应消息
    /// </summary>
    Response,
    
    /// <summary>
    /// 查询消息（保持向后兼容）
    /// </summary>
    Query,
    
    /// <summary>
    /// 通知消息
    /// </summary>
    Notification,
    
    /// <summary>
    /// 心跳消息
    /// </summary>
    Heartbeat
}

/// <summary>
/// 消息优先级枚举
/// </summary>
public enum MessagePriority
{
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 0,
    
    /// <summary>
    /// 普通优先级
    /// </summary>
    Normal = 1,
    
    /// <summary>
    /// 高优先级
    /// </summary>
    High = 2,
    
    /// <summary>
    /// 关键优先级
    /// </summary>
    Critical = 3
}

/// <summary>
/// Priority枚举（保持向后兼容）
/// </summary>
public enum Priority
{
    /// <summary>
    /// 低优先级
    /// </summary>
    Low = 3,
    
    /// <summary>
    /// 普通优先级
    /// </summary>
    Normal = 2,
    
    /// <summary>
    /// 高优先级
    /// </summary>
    High = 1,
    
    /// <summary>
    /// 关键优先级
    /// </summary>
    Critical = 0
} 