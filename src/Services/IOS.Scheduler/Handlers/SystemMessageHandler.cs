using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 系统消息处理器
/// </summary>
public class SystemMessageHandler : BaseMessageHandler
{
    public SystemMessageHandler(
        ILogger<SystemMessageHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        switch (topic)
        {
            case "system/heartbeat":
                await HandleHeartbeat(message);
                break;
            case "system/status":
                await HandleStatusRequest(message);
                break;
            case "system/config":
                await HandleConfigUpdate(message);
                break;
            default:
                Logger.LogWarning("未知的系统消息主题: {Topic}", topic);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "system/heartbeat",
            "system/status",
            "system/config"
        };
    }

    /// <summary>
    /// 处理心跳消息
    /// </summary>
    private async Task HandleHeartbeat(string message)
    {
        Console.WriteLine(message);
        var heartbeatInfo = DeserializeMessage<HeartbeatInfo>(message);
        if (heartbeatInfo == null)
        {
            Logger.LogError("解析心跳消息失败: {Message}", message);
            return;
        }

        Logger.LogDebug("收到心跳消息: 来源={Source}, 时间戳={Timestamp}", 
            heartbeatInfo.Source, heartbeatInfo.Timestamp);

        // 更新设备状态
        SharedDataService.SetData($"heartbeat:{heartbeatInfo.Source}", heartbeatInfo);
        SharedDataService.SetData($"heartbeat:{heartbeatInfo.Source}:last_seen", DateTime.UtcNow);

        // 发送心跳响应
        var response = new
        {
            Source = "IOS.Scheduler",
            Timestamp = DateTime.UtcNow,
            Status = "alive"
        };

        await MqttService.PublishAsync("system/heartbeat/response", SerializeObject(response));
    }

    /// <summary>
    /// 处理状态请求
    /// </summary>
    private async Task HandleStatusRequest(string message)
    {
        Logger.LogInformation("收到系统状态请求");

        try
        {
            var systemStatus = await GetSystemStatus();
            await MqttService.PublishAsync("system/status/response", SerializeObject(systemStatus));
            Logger.LogInformation("已发送系统状态响应");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取系统状态失败");
            var errorResponse = new
            {
                Status = "error",
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            };
            await MqttService.PublishAsync("system/status/error", SerializeObject(errorResponse));
        }
    }

    /// <summary>
    /// 处理配置更新
    /// </summary>
    private async Task HandleConfigUpdate(string message)
    {
        var configInfo = DeserializeMessage<ConfigUpdateInfo>(message);
        if (configInfo == null)
        {
            Logger.LogError("解析配置更新消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到配置更新: 配置项={ConfigKey}", configInfo.ConfigKey);

        try
        {
            // 更新配置
            SharedDataService.SetData($"config:{configInfo.ConfigKey}", configInfo.ConfigValue);
            SharedDataService.SetData($"config:{configInfo.ConfigKey}:updated", DateTime.UtcNow);

            // 应用配置更改
            await ApplyConfigChange(configInfo);

            // 发送确认消息
            var confirmMessage = new
            {
                ConfigKey = configInfo.ConfigKey,
                Status = "updated",
                Timestamp = DateTime.UtcNow
            };
            await MqttService.PublishAsync("system/config/confirm", SerializeObject(confirmMessage));

            Logger.LogInformation("配置更新成功: {ConfigKey}", configInfo.ConfigKey);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "配置更新失败: {ConfigKey}", configInfo.ConfigKey);
            
            var errorMessage = new
            {
                ConfigKey = configInfo.ConfigKey,
                Status = "error",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
            await MqttService.PublishAsync("system/config/error", SerializeObject(errorMessage));
        }
    }

    /// <summary>
    /// 获取系统状态
    /// </summary>
    private async Task<object> GetSystemStatus()
    {
        // 获取任务统计
        var taskKeys = SharedDataService.GetAllKeys()
            .Where(k => k.StartsWith("task:") && k.EndsWith(":status"))
            .ToList();

        var taskStats = new Dictionary<string, int>();
        foreach (var key in taskKeys)
        {
            var status = SharedDataService.GetData<string>(key);
            if (!string.IsNullOrEmpty(status))
            {
                taskStats[status] = taskStats.GetValueOrDefault(status) + 1;
            }
        }

        // 获取设备心跳状态
        var heartbeatKeys = SharedDataService.GetAllKeys()
            .Where(k => k.StartsWith("heartbeat:") && k.EndsWith(":last_seen"))
            .ToList();

        var deviceStatus = new List<object>();
        foreach (var key in heartbeatKeys)
        {
            var deviceName = key.Replace("heartbeat:", "").Replace(":last_seen", "");
            var lastSeen = SharedDataService.GetData<DateTime>(key);
            var isOnline = DateTime.UtcNow.Subtract(lastSeen).TotalMinutes < 5; // 5分钟内有心跳认为在线

            deviceStatus.Add(new
            {
                Device = deviceName,
                Status = isOnline ? "online" : "offline",
                LastSeen = lastSeen
            });
        }

        var systemStatus = new
        {
            Service = "IOS.Scheduler",
            Status = "running",
            Timestamp = DateTime.UtcNow,
            Uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss"),
            TaskStatistics = taskStats,
            DeviceStatus = deviceStatus,
            SharedDataCount = SharedDataService.Count,
            Memory = new
            {
                WorkingSet = GC.GetTotalMemory(false),
                Gen0Collections = GC.CollectionCount(0),
                Gen1Collections = GC.CollectionCount(1),
                Gen2Collections = GC.CollectionCount(2)
            }
        };

        return systemStatus;
    }

    /// <summary>
    /// 应用配置更改
    /// </summary>
    private async Task ApplyConfigChange(ConfigUpdateInfo configInfo)
    {
        switch (configInfo.ConfigKey.ToLower())
        {
            case "log_level":
                // 动态调整日志级别
                Logger.LogInformation("日志级别更新为: {LogLevel}", configInfo.ConfigValue);
                break;

            case "mqtt_reconnect_interval":
                // 更新MQTT重连间隔
                if (int.TryParse(configInfo.ConfigValue?.ToString(), out var interval))
                {
                    Logger.LogInformation("MQTT重连间隔更新为: {Interval}秒", interval);
                    // 这里可以通知MqttService更新重连间隔
                }
                break;

            case "task_timeout":
                // 更新任务超时时间
                if (int.TryParse(configInfo.ConfigValue?.ToString(), out var timeout))
                {
                    Logger.LogInformation("任务超时时间更新为: {Timeout}秒", timeout);
                }
                break;

            default:
                Logger.LogInformation("配置项 {ConfigKey} 已更新，值: {ConfigValue}", 
                    configInfo.ConfigKey, configInfo.ConfigValue);
                break;
        }

        await Task.CompletedTask;
    }
}

#region Data Models

public class HeartbeatInfo
{
    public string Source { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? AdditionalData { get; set; }
}

public class ConfigUpdateInfo
{
    public string ConfigKey { get; set; } = string.Empty;
    public object? ConfigValue { get; set; }
    public string? Description { get; set; }
}

#endregion 