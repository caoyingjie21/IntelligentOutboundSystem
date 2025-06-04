using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 设备消息处理器
/// </summary>
public class DeviceMessageHandler : BaseMessageHandler
{
    public DeviceMessageHandler(
        ILogger<DeviceMessageHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        var topicParts = topic.Split('/');
        if (topicParts.Length < 3)
        {
            Logger.LogWarning("设备消息主题格式错误: {Topic}", topic);
            return;
        }

        var deviceId = topicParts[1];
        var messageType = topicParts[2];

        switch (messageType)
        {
            case "status":
                await HandleDeviceStatus(deviceId, message);
                break;
            case "command":
                await HandleDeviceCommand(deviceId, message);
                break;
            case "response":
                await HandleDeviceResponse(deviceId, message);
                break;
            default:
                Logger.LogWarning("未知的设备消息类型: {MessageType}, 设备: {DeviceId}", messageType, deviceId);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "device/+/status",
            "device/+/command",
            "device/+/response"
        };
    }

    /// <summary>
    /// 处理设备状态消息
    /// </summary>
    private async Task HandleDeviceStatus(string deviceId, string message)
    {
        var statusInfo = DeserializeMessage<DeviceStatusInfo>(message);
        if (statusInfo == null)
        {
            Logger.LogError("解析设备状态消息失败: 设备={DeviceId}, 消息={Message}", deviceId, message);
            return;
        }

        Logger.LogInformation("设备状态更新: 设备={DeviceId}, 状态={Status}", deviceId, statusInfo.Status);

        // 存储设备状态
        SharedDataService.SetData($"device:{deviceId}:status", statusInfo);
        SharedDataService.SetData($"device:{deviceId}:last_update", DateTime.UtcNow);

        // 检查设备状态变化
        var previousStatus = SharedDataService.GetData<DeviceStatusInfo>($"device:{deviceId}:previous_status");
        if (previousStatus != null && previousStatus.Status != statusInfo.Status)
        {
            await HandleDeviceStatusChange(deviceId, previousStatus.Status, statusInfo.Status);
        }

        // 保存前一个状态
        SharedDataService.SetData($"device:{deviceId}:previous_status", statusInfo);

        await Task.CompletedTask;
    }

    /// <summary>
    /// 处理设备命令消息
    /// </summary>
    private async Task HandleDeviceCommand(string deviceId, string message)
    {
        var commandInfo = DeserializeMessage<DeviceCommandInfo>(message);
        if (commandInfo == null)
        {
            Logger.LogError("解析设备命令消息失败: 设备={DeviceId}, 消息={Message}", deviceId, message);
            return;
        }

        Logger.LogInformation("收到设备命令: 设备={DeviceId}, 命令={Command}", deviceId, commandInfo.Command);

        try
        {
            // 记录命令历史
            var commandHistory = SharedDataService.GetData<List<DeviceCommandInfo>>($"device:{deviceId}:command_history") 
                                ?? new List<DeviceCommandInfo>();
            commandHistory.Add(commandInfo);
            
            // 只保留最近50条命令记录
            if (commandHistory.Count > 50)
            {
                commandHistory = commandHistory.Skip(commandHistory.Count - 50).ToList();
            }
            
            SharedDataService.SetData($"device:{deviceId}:command_history", commandHistory);

            // 执行设备命令
            await ExecuteDeviceCommand(deviceId, commandInfo);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "执行设备命令失败: 设备={DeviceId}, 命令={Command}", deviceId, commandInfo.Command);
            
            // 发送错误响应
            var errorResponse = new
            {
                DeviceId = deviceId,
                CommandId = commandInfo.CommandId,
                Status = "error",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
            await MqttService.PublishAsync($"device/{deviceId}/error", SerializeObject(errorResponse));
        }
    }

    /// <summary>
    /// 处理设备响应消息
    /// </summary>
    private async Task HandleDeviceResponse(string deviceId, string message)
    {
        var responseInfo = DeserializeMessage<DeviceResponseInfo>(message);
        if (responseInfo == null)
        {
            Logger.LogError("解析设备响应消息失败: 设备={DeviceId}, 消息={Message}", deviceId, message);
            return;
        }

        Logger.LogInformation("收到设备响应: 设备={DeviceId}, 命令ID={CommandId}, 状态={Status}", 
            deviceId, responseInfo.CommandId, responseInfo.Status);

        // 存储设备响应
        SharedDataService.SetData($"device:{deviceId}:response:{responseInfo.CommandId}", responseInfo);
        SharedDataService.SetData($"device:{deviceId}:last_response", responseInfo);

        // 处理响应结果
        await ProcessDeviceResponse(deviceId, responseInfo);
    }

    /// <summary>
    /// 处理设备状态变化
    /// </summary>
    private async Task HandleDeviceStatusChange(string deviceId, string previousStatus, string currentStatus)
    {
        Logger.LogInformation("设备状态变化: 设备={DeviceId}, 从 {PreviousStatus} 变为 {CurrentStatus}", 
            deviceId, previousStatus, currentStatus);

        // 发送状态变化通知
        var statusChangeNotification = new
        {
            DeviceId = deviceId,
            PreviousStatus = previousStatus,
            CurrentStatus = currentStatus,
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("system/device/status_changed", SerializeObject(statusChangeNotification));

        // 根据状态变化执行相应操作
        switch (currentStatus.ToLower())
        {
            case "error":
            case "fault":
                await HandleDeviceError(deviceId, currentStatus);
                break;
            case "offline":
                await HandleDeviceOffline(deviceId);
                break;
            case "online":
                await HandleDeviceOnline(deviceId);
                break;
        }
    }

    /// <summary>
    /// 执行设备命令
    /// </summary>
    private async Task ExecuteDeviceCommand(string deviceId, DeviceCommandInfo commandInfo)
    {
        Logger.LogInformation("执行设备命令: 设备={DeviceId}, 命令={Command}", deviceId, commandInfo.Command);

        // 根据命令类型执行不同操作
        switch (commandInfo.Command.ToLower())
        {
            case "start":
                await HandleStartCommand(deviceId, commandInfo);
                break;
            case "stop":
                await HandleStopCommand(deviceId, commandInfo);
                break;
            case "reset":
                await HandleResetCommand(deviceId, commandInfo);
                break;
            case "configure":
                await HandleConfigureCommand(deviceId, commandInfo);
                break;
            default:
                Logger.LogWarning("未知的设备命令: {Command}, 设备: {DeviceId}", commandInfo.Command, deviceId);
                break;
        }
    }

    /// <summary>
    /// 处理设备响应
    /// </summary>
    private async Task ProcessDeviceResponse(string deviceId, DeviceResponseInfo responseInfo)
    {
        switch (responseInfo.Status.ToLower())
        {
            case "success":
                Logger.LogInformation("设备命令执行成功: 设备={DeviceId}, 命令ID={CommandId}", 
                    deviceId, responseInfo.CommandId);
                break;
            case "error":
                Logger.LogError("设备命令执行失败: 设备={DeviceId}, 命令ID={CommandId}, 错误={Error}", 
                    deviceId, responseInfo.CommandId, responseInfo.ErrorMessage);
                await HandleCommandError(deviceId, responseInfo);
                break;
            case "timeout":
                Logger.LogWarning("设备命令超时: 设备={DeviceId}, 命令ID={CommandId}", 
                    deviceId, responseInfo.CommandId);
                break;
        }

        await Task.CompletedTask;
    }

    private async Task HandleStartCommand(string deviceId, DeviceCommandInfo commandInfo)
    {
        Logger.LogInformation("处理启动命令: 设备={DeviceId}", deviceId);
        // 实现启动逻辑
        await Task.CompletedTask;
    }

    private async Task HandleStopCommand(string deviceId, DeviceCommandInfo commandInfo)
    {
        Logger.LogInformation("处理停止命令: 设备={DeviceId}", deviceId);
        // 实现停止逻辑
        await Task.CompletedTask;
    }

    private async Task HandleResetCommand(string deviceId, DeviceCommandInfo commandInfo)
    {
        Logger.LogInformation("处理重置命令: 设备={DeviceId}", deviceId);
        // 实现重置逻辑
        await Task.CompletedTask;
    }

    private async Task HandleConfigureCommand(string deviceId, DeviceCommandInfo commandInfo)
    {
        Logger.LogInformation("处理配置命令: 设备={DeviceId}", deviceId);
        // 实现配置逻辑
        await Task.CompletedTask;
    }

    private async Task HandleDeviceError(string deviceId, string errorStatus)
    {
        Logger.LogWarning("设备错误: 设备={DeviceId}, 状态={ErrorStatus}", deviceId, errorStatus);
        // 实现错误处理逻辑
        await Task.CompletedTask;
    }

    private async Task HandleDeviceOffline(string deviceId)
    {
        Logger.LogWarning("设备离线: 设备={DeviceId}", deviceId);
        // 实现离线处理逻辑
        await Task.CompletedTask;
    }

    private async Task HandleDeviceOnline(string deviceId)
    {
        Logger.LogInformation("设备上线: 设备={DeviceId}", deviceId);
        // 实现上线处理逻辑
        await Task.CompletedTask;
    }

    private async Task HandleCommandError(string deviceId, DeviceResponseInfo responseInfo)
    {
        Logger.LogError("设备命令错误: 设备={DeviceId}, 命令ID={CommandId}", deviceId, responseInfo.CommandId);
        // 实现命令错误处理逻辑
        await Task.CompletedTask;
    }
}

#region Data Models

public class DeviceStatusInfo
{
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, object>? Properties { get; set; }
}

public class DeviceCommandInfo
{
    public string CommandId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DeviceResponseInfo
{
    public string CommandId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion 