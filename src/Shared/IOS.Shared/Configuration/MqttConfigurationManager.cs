using Microsoft.Extensions.Configuration;
using IOS.Infrastructure.Messaging;

namespace IOS.Shared.Configuration;

/// <summary>
/// MQTT配置管理器
/// </summary>
public static class MqttConfigurationManager
{
    /// <summary>
    /// 加载标准化MQTT配置
    /// </summary>
    /// <param name="configuration">配置对象</param>
    /// <param name="serviceName">服务名称</param>
    /// <returns>标准化MQTT配置</returns>
    public static StandardMqttOptions LoadConfiguration(IConfiguration configuration, string serviceName)
    {
        var options = configuration.GetSection("Mqtt").Get<StandardMqttOptions>() ?? new();
        
        // 设置服务名称
        options.ServiceName = serviceName;
        
        // 解析主题模板
        ResolveTopicTemplates(options, serviceName);
        
        // 设置默认客户端ID
        if (string.IsNullOrEmpty(options.Connection.ClientId))
        {
            options.Connection.ClientId = $"IOS.{serviceName}";
        }
        
        return options;
    }

    /// <summary>
    /// 解析主题模板
    /// </summary>
    /// <param name="options">MQTT配置选项</param>
    /// <param name="serviceName">服务名称</param>
    private static void ResolveTopicTemplates(StandardMqttOptions options, string serviceName)
    {
        // 解析订阅主题
        foreach (var topic in options.Topics.Subscribe.ToList())
        {
            options.Topics.Subscribe[topic.Key] = ResolveTopicTemplate(topic.Value, serviceName);
        }
        
        // 解析发布主题
        foreach (var topic in options.Topics.Publish.ToList())
        {
            options.Topics.Publish[topic.Key] = ResolveTopicTemplate(topic.Value, serviceName);
        }
    }

    /// <summary>
    /// 解析主题模板
    /// </summary>
    /// <param name="template">主题模板</param>
    /// <param name="serviceName">服务名称</param>
    /// <returns>解析后的主题</returns>
    private static string ResolveTopicTemplate(string template, string serviceName)
    {
        return template
            .Replace("{serviceName}", serviceName.ToLower())
            .Replace("{version}", "v1")
            .Replace("{timestamp}", DateTime.UtcNow.ToString("yyyyMMdd"))
            .Replace("{environment}", GetEnvironment());
    }

    /// <summary>
    /// 获取当前环境
    /// </summary>
    /// <returns>环境名称</returns>
    private static string GetEnvironment()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    }

    /// <summary>
    /// 验证配置
    /// </summary>
    /// <param name="options">MQTT配置选项</param>
    /// <returns>验证结果</returns>
    public static ConfigurationValidationResult ValidateConfiguration(StandardMqttOptions options)
    {
        var result = new ConfigurationValidationResult();

        // 验证连接配置
        if (string.IsNullOrEmpty(options.Connection.Broker))
        {
            result.AddError("Connection.Broker", "MQTT代理地址不能为空");
        }

        if (options.Connection.Port <= 0 || options.Connection.Port > 65535)
        {
            result.AddError("Connection.Port", "MQTT端口必须在1-65535范围内");
        }

        if (string.IsNullOrEmpty(options.Connection.ClientId))
        {
            result.AddError("Connection.ClientId", "客户端ID不能为空");
        }

        // 验证主题配置
        if (options.Topics.Subscribe.Count == 0 && options.Topics.Publish.Count == 0)
        {
            result.AddWarning("Topics", "未配置任何订阅或发布主题");
        }

        return result;
    }
}

/// <summary>
/// 标准化MQTT配置选项
/// </summary>
public class StandardMqttOptions
{
    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; set; } = "";

    /// <summary>
    /// 连接配置
    /// </summary>
    public MqttConnectionOptions Connection { get; set; } = new();

    /// <summary>
    /// 主题配置
    /// </summary>
    public MqttTopicConfiguration Topics { get; set; } = new();

    /// <summary>
    /// 消息配置
    /// </summary>
    public MqttMessageOptions Messages { get; set; } = new();
}

/// <summary>
/// MQTT连接配置选项
/// </summary>
public class MqttConnectionOptions
{
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
    public string ClientId { get; set; } = "";

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
    public int KeepAlivePeriod { get; set; } = 60;

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

/// <summary>
/// MQTT主题配置
/// </summary>
public class MqttTopicConfiguration
{
    /// <summary>
    /// 订阅主题配置
    /// </summary>
    public Dictionary<string, string> Subscribe { get; set; } = new();

    /// <summary>
    /// 发布主题配置
    /// </summary>
    public Dictionary<string, string> Publish { get; set; } = new();
}

/// <summary>
/// MQTT消息配置选项
/// </summary>
public class MqttMessageOptions
{
    /// <summary>
    /// 消息版本
    /// </summary>
    public string Version { get; set; } = "v1";

    /// <summary>
    /// 是否启用消息验证
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// 最大重试次数
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 消息超时时间（秒）
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// 配置验证结果
/// </summary>
public class ConfigurationValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// 错误列表
    /// </summary>
    public IReadOnlyList<string> Errors => _errors.AsReadOnly();

    /// <summary>
    /// 警告列表
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

    /// <summary>
    /// 添加错误
    /// </summary>
    /// <param name="field">字段名</param>
    /// <param name="message">错误消息</param>
    public void AddError(string field, string message)
    {
        _errors.Add($"{field}: {message}");
    }

    /// <summary>
    /// 添加警告
    /// </summary>
    /// <param name="field">字段名</param>
    /// <param name="message">警告消息</param>
    public void AddWarning(string field, string message)
    {
        _warnings.Add($"{field}: {message}");
    }
} 