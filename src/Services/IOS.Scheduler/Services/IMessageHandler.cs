namespace IOS.Scheduler.Services;

/// <summary>
/// 消息处理器接口
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// 处理消息
    /// </summary>
    /// <param name="topic">MQTT主题</param>
    /// <param name="message">消息内容</param>
    /// <returns></returns>
    Task HandleMessageAsync(string topic, string message);

    /// <summary>
    /// 处理器是否支持指定主题
    /// </summary>
    /// <param name="topic">MQTT主题</param>
    /// <returns></returns>
    bool CanHandle(string topic);
} 