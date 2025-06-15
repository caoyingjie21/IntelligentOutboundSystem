using IOS.Shared.Messages;

namespace IOS.Shared.Services;

/// <summary>
/// 主题注册服务
/// </summary>
public class TopicRegistry
{
    private readonly Dictionary<string, TopicDefinition> _topics = new();
    private readonly object _lock = new();

    /// <summary>
    /// 注册主题
    /// </summary>
    /// <param name="key">主题键</param>
    /// <param name="pattern">主题模式</param>
    /// <param name="messageType">消息类型</param>
    /// <param name="dataType">数据类型</param>
    public void RegisterTopic(string key, string pattern, MessageType messageType, Type? dataType = null)
    {
        lock (_lock)
        {
            _topics[key] = new TopicDefinition
            {
                Key = key,
                Pattern = pattern,
                MessageType = messageType,
                DataType = dataType,
                RegisteredAt = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 获取主题
    /// </summary>
    /// <param name="key">主题键</param>
    /// <param name="version">版本</param>
    /// <param name="parameters">参数</param>
    /// <returns>解析后的主题</returns>
    public string GetTopic(string key, string version = "v1", params object[] parameters)
    {
        lock (_lock)
        {
            if (!_topics.TryGetValue(key, out var definition))
                throw new InvalidOperationException($"Topic key '{key}' not registered");

            var topic = definition.Pattern.Replace("{version}", version);
            
            if (parameters.Length > 0)
            {
                topic = string.Format(topic, parameters);
            }

            return topic;
        }
    }

    /// <summary>
    /// 获取所有注册的主题
    /// </summary>
    /// <returns>主题定义列表</returns>
    public IReadOnlyList<TopicDefinition> GetAllTopics()
    {
        lock (_lock)
        {
            return _topics.Values.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// 检查主题是否已注册
    /// </summary>
    /// <param name="key">主题键</param>
    /// <returns>是否已注册</returns>
    public bool IsRegistered(string key)
    {
        lock (_lock)
        {
            return _topics.ContainsKey(key);
        }
    }

    /// <summary>
    /// 移除主题注册
    /// </summary>
    /// <param name="key">主题键</param>
    /// <returns>是否成功移除</returns>
    public bool UnregisterTopic(string key)
    {
        lock (_lock)
        {
            return _topics.Remove(key);
        }
    }

    /// <summary>
    /// 清空所有注册的主题
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _topics.Clear();
        }
    }
}

/// <summary>
/// 主题定义
/// </summary>
public class TopicDefinition
{
    /// <summary>
    /// 主题键
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// 主题模式
    /// </summary>
    public string Pattern { get; set; } = "";

    /// <summary>
    /// 消息类型
    /// </summary>
    public MessageType MessageType { get; set; }

    /// <summary>
    /// 数据类型
    /// </summary>
    public Type? DataType { get; set; }

    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTime RegisteredAt { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = "";
} 