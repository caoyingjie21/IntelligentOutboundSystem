using System.Text.Json;
using System.Text.Json.Serialization;

namespace IOS.Shared.Utilities;

/// <summary>
/// JSON序列化帮助类，提供统一的序列化配置
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// 默认的JSON序列化选项（用于发送消息）
    /// </summary>
    public static readonly JsonSerializerOptions DefaultSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// 默认的JSON反序列化选项（用于接收消息）
    /// </summary>
    public static readonly JsonSerializerOptions DefaultDeserializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>
    /// 序列化对象为JSON字符串
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    /// <param name="obj">要序列化的对象</param>
    /// <param name="options">序列化选项，如果为null则使用默认选项</param>
    /// <returns>JSON字符串</returns>
    public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(obj, options ?? DefaultSerializerOptions);
    }

    /// <summary>
    /// 反序列化JSON字符串为对象
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON字符串</param>
    /// <param name="options">反序列化选项，如果为null则使用默认选项</param>
    /// <returns>反序列化后的对象，失败时返回null</returns>
    public static T? Deserialize<T>(string json, JsonSerializerOptions? options = null) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultDeserializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// 安全的反序列化方法，包含错误处理
    /// </summary>
    /// <typeparam name="T">目标对象类型</typeparam>
    /// <param name="json">JSON字符串</param>
    /// <param name="result">反序列化结果</param>
    /// <param name="error">错误信息</param>
    /// <param name="options">反序列化选项</param>
    /// <returns>是否成功反序列化</returns>
    public static bool TryDeserialize<T>(string json, out T? result, out string? error, JsonSerializerOptions? options = null) where T : class
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, options ?? DefaultDeserializerOptions);
            error = null;
            return result != null;
        }
        catch (JsonException ex)
        {
            result = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// 验证JSON字符串格式是否正确
    /// </summary>
    /// <param name="json">JSON字符串</param>
    /// <returns>是否为有效的JSON格式</returns>
    public static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// 格式化JSON字符串（美化输出）
    /// </summary>
    /// <param name="json">原始JSON字符串</param>
    /// <returns>格式化后的JSON字符串</returns>
    public static string FormatJson(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json; // 如果解析失败，返回原始字符串
        }
    }
} 