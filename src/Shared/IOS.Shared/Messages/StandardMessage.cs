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
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// 消息时间戳
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 消息来源
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 消息目标
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// 消息类型
    /// </summary>
    [JsonPropertyName("type")]
    public MessageType Type { get; set; }

    /// <summary>
    /// 消息数据
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    /// <summary>
    /// 消息头部信息
    /// </summary>
    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// 关联ID，用于追踪相关消息
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// 消息优先级
    /// </summary>
    [JsonPropertyName("priority")]
    public Priority Priority { get; set; } = Priority.Normal;
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
    /// 查询消息
    /// </summary>
    Query,
    
    /// <summary>
    /// 响应消息
    /// </summary>
    Response,
    
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