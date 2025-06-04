using Microsoft.Extensions.Logging;
using IOS.Scheduler.Handlers;

namespace IOS.Scheduler.Services;

/// <summary>
/// 消息处理器工厂
/// </summary>
public class MessageHandlerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageHandlerFactory> _logger;
    private readonly Dictionary<string, Type> _handlerMappings;

    public MessageHandlerFactory(IServiceProvider serviceProvider, ILogger<MessageHandlerFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _handlerMappings = InitializeHandlerMappings();
    }

    /// <summary>
    /// 初始化处理器映射
    /// </summary>
    private Dictionary<string, Type> InitializeHandlerMappings()
    {
        return new Dictionary<string, Type>
        {
            // 系统消息
            { "system/heartbeat", typeof(SystemMessageHandler) },
            { "system/status", typeof(SystemMessageHandler) },
            { "system/config", typeof(SystemMessageHandler) },
            
            // 出库任务消息
            { "outbound/task/created", typeof(OutboundTaskHandler) },
            { "outbound/task/execute", typeof(OutboundTaskHandler) },
            { "outbound/task/progress", typeof(OutboundTaskHandler) },
            { "outbound/task/completed", typeof(OutboundTaskHandler) },
            { "outbound/task/cancelled", typeof(OutboundTaskHandler) },
            
            // 设备消息
            { "device/+/status", typeof(DeviceMessageHandler) },
            { "device/+/command", typeof(DeviceMessageHandler) },
            { "device/+/response", typeof(DeviceMessageHandler) },
            
            // 传感器消息
            { "sensor/grating", typeof(SensorMessageHandler) },
            { "sensor/data", typeof(SensorMessageHandler) },
            
            // 运动控制消息
            { "motion/moving/complete", typeof(MotionControlHandler) },
            { "motion/position", typeof(MotionControlHandler) },
            
            // 视觉系统消息
            { "vision/detection", typeof(VisionMessageHandler) },
            { "vision/result", typeof(VisionMessageHandler) },
            
            // 读码器消息
            { "coder/result", typeof(CoderMessageHandler) },
            { "coder/complete", typeof(CoderMessageHandler) }
        };
    }

    /// <summary>
    /// 创建消息处理器
    /// </summary>
    /// <param name="topic">MQTT主题</param>
    /// <returns>消息处理器实例</returns>
    public IMessageHandler CreateHandler(string topic)
    {
        try
        {
            // 精确匹配
            if (_handlerMappings.TryGetValue(topic, out var exactHandlerType))
            {
                return CreateHandlerInstance(exactHandlerType, topic);
            }

            // 通配符匹配
            foreach (var (pattern, handlerType) in _handlerMappings)
            {
                if (IsTopicMatch(topic, pattern))
                {
                    return CreateHandlerInstance(handlerType, topic);
                }
            }

            // 如果没有找到特定处理器，返回默认处理器
            _logger.LogWarning("没有找到主题 {Topic} 的处理器，使用默认处理器", topic);
            return CreateHandlerInstance(typeof(DefaultMessageHandler), topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建消息处理器失败，主题: {Topic}", topic);
            throw;
        }
    }

    /// <summary>
    /// 创建处理器实例
    /// </summary>
    private IMessageHandler CreateHandlerInstance(Type handlerType, string topic)
    {
        var handler = _serviceProvider.GetRequiredService(handlerType) as IMessageHandler;
        if (handler == null)
        {
            throw new InvalidOperationException($"无法创建处理器实例: {handlerType.Name}，主题: {topic}");
        }
        return handler;
    }

    /// <summary>
    /// 检查主题是否匹配模式（支持MQTT通配符）
    /// </summary>
    private static bool IsTopicMatch(string topic, string pattern)
    {
        if (pattern == topic) return true;
        if (!pattern.Contains('+') && !pattern.Contains('#')) return false;

        var topicParts = topic.Split('/');
        var patternParts = pattern.Split('/');

        return IsTopicMatchRecursive(topicParts, patternParts, 0, 0);
    }

    /// <summary>
    /// 递归匹配主题模式
    /// </summary>
    private static bool IsTopicMatchRecursive(string[] topicParts, string[] patternParts, int topicIndex, int patternIndex)
    {
        if (patternIndex >= patternParts.Length)
            return topicIndex >= topicParts.Length;

        if (topicIndex >= topicParts.Length)
            return patternParts[patternIndex] == "#";

        var patternPart = patternParts[patternIndex];

        if (patternPart == "#")
            return true;

        if (patternPart == "+")
            return IsTopicMatchRecursive(topicParts, patternParts, topicIndex + 1, patternIndex + 1);

        if (patternPart == topicParts[topicIndex])
            return IsTopicMatchRecursive(topicParts, patternParts, topicIndex + 1, patternIndex + 1);

        return false;
    }
} 