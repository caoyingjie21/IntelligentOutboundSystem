using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 视觉系统消息处理器
/// </summary>
public class VisionMessageHandler : BaseMessageHandler
{
    public VisionMessageHandler(
        ILogger<VisionMessageHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        switch (topic)
        {
            case "vision/detection":
                await HandleVisionDetection(message);
                break;
            case "vision/result":
                await HandleVisionResult(message);
                break;
            case "vision/height/result":
                await HandleVisionHeightResult(message);
                break;
            default:
                Logger.LogWarning("未知的视觉系统消息主题: {Topic}", topic);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "vision/detection",
            "vision/result"
        };
    }

    private async Task HandleVisionDetection(string message)
    {
        var detectionData = DeserializeMessage<VisionDetectionData>(message);
        if (detectionData == null)
        {
            Logger.LogError("解析视觉检测消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到视觉检测数据: 任务ID={TaskId}, 检测到={ObjectCount}个对象", 
            detectionData.TaskId, detectionData.DetectedObjects?.Count ?? 0);

        // 存储检测结果
        SharedDataService.SetData($"vision:{detectionData.TaskId}:detection", detectionData);
        SharedDataService.SetData($"vision:{detectionData.TaskId}:last_update", DateTime.UtcNow);

        // 处理检测结果
        await ProcessDetectionResult(detectionData);
    }

    private async Task HandleVisionHeightResult(string message)
    {
        var heightData = DeserializeMessage<VisionHeightResultData>(message);
        if (heightData == null)
        {
            Logger.LogError("解析视觉结果消息失败: {Message}", message);
            return;
        }
        Logger.LogInformation("收到视觉高度结果: 高度={Height}", heightData.min_height);

        SharedDataService.SetData("min_height", heightData.min_height);
        await Task.CompletedTask;
    }
    
    private async Task HandleVisionResult(string message)
    {
        var resultData = DeserializeMessage<VisionResultData>(message);
        if (resultData == null)
        {
            Logger.LogError("解析视觉结果消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到视觉处理结果: 任务ID={TaskId}, 结果={Result}", 
            resultData.TaskId, resultData.Result);

        // 存储处理结果
        SharedDataService.SetData($"vision:{resultData.TaskId}:result", resultData);

        await Task.CompletedTask;
    }

    private async Task ProcessDetectionResult(VisionDetectionData detectionData)
    {
        if (detectionData.DetectedObjects == null || !detectionData.DetectedObjects.Any())
        {
            Logger.LogWarning("未检测到任何对象: 任务ID={TaskId}", detectionData.TaskId);
            
            var noObjectMessage = new
            {
                TaskId = detectionData.TaskId,
                Event = "no_object_detected",
                Timestamp = DateTime.UtcNow
            };
            
            await MqttService.PublishAsync("vision/events/no_object", SerializeObject(noObjectMessage));
            return;
        }

        // 分析检测到的对象
        foreach (var obj in detectionData.DetectedObjects)
        {
            Logger.LogInformation("检测到对象: 类型={Type}, 置信度={Confidence}, 位置=({X},{Y})", 
                obj.ObjectType, obj.Confidence, obj.X, obj.Y);

            // 根据对象类型进行不同处理
            await ProcessDetectedObject(detectionData.TaskId, obj);
        }

        // 发送检测完成通知
        var completionMessage = new
        {
            TaskId = detectionData.TaskId,
            Event = "detection_completed",
            ObjectCount = detectionData.DetectedObjects.Count,
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("vision/events/detection_completed", SerializeObject(completionMessage));
    }

    private async Task ProcessDetectedObject(string taskId, DetectedObject obj)
    {
        // 根据对象类型执行不同的处理逻辑
        switch (obj.ObjectType.ToLower())
        {
            case "package":
                await HandlePackageDetection(taskId, obj);
                break;
            case "qrcode":
                await HandleQRCodeDetection(taskId, obj);
                break;
            case "barcode":
                await HandleBarcodeDetection(taskId, obj);
                break;
            default:
                Logger.LogInformation("未知对象类型: {ObjectType}", obj.ObjectType);
                break;
        }
    }

    private async Task HandlePackageDetection(string taskId, DetectedObject package)
    {
        Logger.LogInformation("检测到包裹: 任务ID={TaskId}, 位置=({X},{Y})", taskId, package.X, package.Y);

        // 记录包裹信息
        var packageInfo = new
        {
            TaskId = taskId,
            ObjectType = "package",
            Position = new { X = package.X, Y = package.Y },
            Confidence = package.Confidence,
            Timestamp = DateTime.UtcNow
        };

        SharedDataService.SetData($"task:{taskId}:package_info", packageInfo);

        // 通知下一步处理
        await MqttService.PublishAsync("outbound/package/detected", SerializeObject(packageInfo));
    }

    private async Task HandleQRCodeDetection(string taskId, DetectedObject qrCode)
    {
        Logger.LogInformation("检测到二维码: 任务ID={TaskId}, 内容={Content}", taskId, qrCode.Content);

        // 解析二维码内容
        var qrCodeInfo = new
        {
            TaskId = taskId,
            ObjectType = "qrcode",
            Content = qrCode.Content,
            Position = new { X = qrCode.X, Y = qrCode.Y },
            Timestamp = DateTime.UtcNow
        };

        SharedDataService.SetData($"task:{taskId}:qrcode_info", qrCodeInfo);

        // 发送二维码识别结果
        await MqttService.PublishAsync("coder/qrcode/result", SerializeObject(qrCodeInfo));
    }

    private async Task HandleBarcodeDetection(string taskId, DetectedObject barcode)
    {
        Logger.LogInformation("检测到条形码: 任务ID={TaskId}, 内容={Content}", taskId, barcode.Content);

        // 解析条形码内容
        var barcodeInfo = new
        {
            TaskId = taskId,
            ObjectType = "barcode",
            Content = barcode.Content,
            Position = new { X = barcode.X, Y = barcode.Y },
            Timestamp = DateTime.UtcNow
        };

        SharedDataService.SetData($"task:{taskId}:barcode_info", barcodeInfo);

        // 发送条形码识别结果
        await MqttService.PublishAsync("coder/barcode/result", SerializeObject(barcodeInfo));
    }
}

#region Data Models

public class VisionDetectionData
{
    public string TaskId { get; set; } = string.Empty;
    public List<DetectedObject>? DetectedObjects { get; set; }
    public DateTime Timestamp { get; set; }
}

public class VisionResultData
{
    public string TaskId { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public Dictionary<string, object>? AdditionalData { get; set; }
    public DateTime Timestamp { get; set; }
}

public class VisionHeightResultData
{
    public double min_height { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class DetectedObject
{
    public string ObjectType { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Confidence { get; set; }
    public string? Content { get; set; }
}

#endregion 