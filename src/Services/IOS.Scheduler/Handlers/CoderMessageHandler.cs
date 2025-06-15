using IOS.Infrastructure.Messaging;
using IOS.Scheduler.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IOS.Scheduler.Handlers;

/// <summary>
/// 读码器消息处理器
/// </summary>
public class CoderMessageHandler : BaseMessageHandler
{
    public CoderMessageHandler(
        ILogger<CoderMessageHandler> logger,
        SharedDataService sharedDataService,
        IMqttService mqttService)
        : base(logger, sharedDataService, mqttService)
    {
    }

    protected override async Task ProcessMessageAsync(string topic, string message)
    {
        switch (topic)
        {
            case "coder/result":
                await HandleCoderResult(message);
                break;
            case "coder/complete":
                await HandleCoderComplete(message);
                break;
            default:
                Logger.LogWarning("未知的读码器消息主题: {Topic}", topic);
                break;
        }
    }

    protected override IEnumerable<string> GetSupportedTopics()
    {
        return new[]
        {
            "coder/result",
            "coder/complete"
        };
    }

    private async Task HandleCoderResult(string message)
    {
        var coderResult = DeserializeMessage<CoderResultData>(message);
        if (coderResult == null)
        {
            Logger.LogError("解析读码器结果消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("收到读码器结果: 任务ID={TaskId}, 码值={Code}, 类型={CodeType}", 
            coderResult.TaskId, coderResult.Code, coderResult.CodeType);

        // 存储读码结果
        SharedDataService.SetData($"coder:{coderResult.TaskId}:result", coderResult);
        SharedDataService.SetData($"task:{coderResult.TaskId}:code_info", coderResult);

        // 验证码值
        await ValidateCode(coderResult);
    }

    private async Task HandleCoderComplete(string message)
    {
        var completionData = DeserializeMessage<CoderCompletionData>(message);
        if (completionData == null)
        {
            Logger.LogError("解析读码器完成消息失败: {Message}", message);
            return;
        }

        Logger.LogInformation("读码器处理完成: 任务ID={TaskId}, 状态={Status}", 
            completionData.TaskId, completionData.Status);

        // 更新任务状态
        SharedDataService.SetData($"task:{completionData.TaskId}:coder_status", "completed");
        SharedDataService.SetData($"task:{completionData.TaskId}:coder_result", completionData.Status);

        // 根据结果继续后续流程
        await ProcessCoderCompletion(completionData);
    }

    private async Task ValidateCode(CoderResultData coderResult)
    {
        try
        {
            // 验证码值格式和有效性
            var isValid = await ValidateCodeFormat(coderResult.Code, coderResult.CodeType);
            
            var validationResult = new
            {
                TaskId = coderResult.TaskId,
                Code = coderResult.Code,
                CodeType = coderResult.CodeType,
                IsValid = isValid,
                Timestamp = DateTime.UtcNow
            };

            // 存储验证结果
            SharedDataService.SetData($"task:{coderResult.TaskId}:code_validation", validationResult);

            if (isValid)
            {
                Logger.LogInformation("码值验证通过: 任务ID={TaskId}, 码值={Code}", 
                    coderResult.TaskId, coderResult.Code);
                
                // 发送验证成功通知
                await MqttService.PublishAsync("coder/validation/success", SerializeObject(validationResult));
                
                // 触发下一步流程
                await TriggerNextProcess(coderResult.TaskId);
            }
            else
            {
                Logger.LogWarning("码值验证失败: 任务ID={TaskId}, 码值={Code}", 
                    coderResult.TaskId, coderResult.Code);
                
                // 发送验证失败通知
                await MqttService.PublishAsync("coder/validation/failed", SerializeObject(validationResult));
                
                // 处理验证失败
                await HandleValidationFailure(coderResult.TaskId, coderResult.Code);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "码值验证异常: 任务ID={TaskId}, 码值={Code}", 
                coderResult.TaskId, coderResult.Code);
            
            var errorResult = new
            {
                TaskId = coderResult.TaskId,
                Code = coderResult.Code,
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            };
            
            await MqttService.PublishAsync("coder/validation/error", SerializeObject(errorResult));
        }
    }

    private async Task<bool> ValidateCodeFormat(string code, string codeType)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        // 根据码值类型进行不同的验证
        switch (codeType.ToLower())
        {
            case "qrcode":
                return await ValidateQRCode(code);
            case "barcode":
                return await ValidateBarcode(code);
            case "datamatrix":
                return await ValidateDataMatrix(code);
            default:
                Logger.LogWarning("未知的码值类型: {CodeType}", codeType);
                return false;
        }
    }

    private async Task<bool> ValidateQRCode(string code)
    {
        // 二维码验证逻辑
        // 这里可以实现具体的二维码格式验证
        await Task.CompletedTask;
        
        // 简单的长度和字符验证
        return code.Length >= 3 && code.Length <= 1000;
    }

    private async Task<bool> ValidateBarcode(string code)
    {
        // 条形码验证逻辑
        await Task.CompletedTask;
        
        // 简单的数字验证
        return code.All(char.IsDigit) && code.Length >= 8 && code.Length <= 20;
    }

    private async Task<bool> ValidateDataMatrix(string code)
    {
        // 数据矩阵码验证逻辑
        await Task.CompletedTask;
        
        return !string.IsNullOrWhiteSpace(code) && code.Length >= 3;
    }

    private async Task ProcessCoderCompletion(CoderCompletionData completionData)
    {
        switch (completionData.Status.ToLower())
        {
            case "success":
                await HandleCoderSuccess(completionData.TaskId);
                break;
            case "failed":
                await HandleCoderFailure(completionData.TaskId, completionData.ErrorMessage);
                break;
            case "timeout":
                await HandleCoderTimeout(completionData.TaskId);
                break;
            default:
                Logger.LogWarning("未知的读码器状态: {Status}", completionData.Status);
                break;
        }
    }

    private async Task TriggerNextProcess(string taskId)
    {
        // 触发任务的下一个处理步骤
        var nextStepMessage = new
        {
            TaskId = taskId,
            Step = "data_processing",
            Timestamp = DateTime.UtcNow
        };

        await MqttService.PublishAsync("outbound/task/next_step", SerializeObject(nextStepMessage));
        Logger.LogInformation("已触发任务下一步处理: {TaskId}", taskId);
    }

    private async Task HandleValidationFailure(string taskId, string code)
    {
        Logger.LogWarning("处理码值验证失败: 任务ID={TaskId}, 码值={Code}", taskId, code);
        
        // 记录失败信息
        SharedDataService.SetData($"task:{taskId}:validation_failed", true);
        SharedDataService.SetData($"task:{taskId}:failed_code", code);
        
        // 可能需要重新读码或报警
        var retryMessage = new
        {
            TaskId = taskId,
            Action = "retry_reading",
            Reason = "validation_failed",
            Timestamp = DateTime.UtcNow
        };
        
        await MqttService.PublishAsync("coder/retry", SerializeObject(retryMessage));
    }

    private async Task HandleCoderSuccess(string taskId)
    {
        Logger.LogInformation("读码器处理成功: 任务ID={TaskId}", taskId);
        
        // 发送成功通知
        var successMessage = new
        {
            TaskId = taskId,
            Status = "coder_success",
            Timestamp = DateTime.UtcNow
        };
        
        await MqttService.PublishAsync("outbound/task/coder_success", SerializeObject(successMessage));
    }

    private async Task HandleCoderFailure(string taskId, string? errorMessage)
    {
        Logger.LogError("读码器处理失败: 任务ID={TaskId}, 错误={Error}", taskId, errorMessage);
        
        // 记录错误信息
        SharedDataService.SetData($"task:{taskId}:coder_error", errorMessage);
        
        // 发送错误通知
        var errorNotification = new
        {
            TaskId = taskId,
            Status = "coder_failed",
            Error = errorMessage,
            Timestamp = DateTime.UtcNow
        };
        
        await MqttService.PublishAsync("outbound/task/coder_error", SerializeObject(errorNotification));
    }

    private async Task HandleCoderTimeout(string taskId)
    {
        Logger.LogWarning("读码器处理超时: 任务ID={TaskId}", taskId);
        
        // 记录超时信息
        SharedDataService.SetData($"task:{taskId}:coder_timeout", DateTime.UtcNow);
        
        // 发送超时通知
        var timeoutMessage = new
        {
            TaskId = taskId,
            Status = "coder_timeout",
            Timestamp = DateTime.UtcNow
        };
        
        await MqttService.PublishAsync("outbound/task/coder_timeout", SerializeObject(timeoutMessage));
    }
}

#region Data Models

public class CoderResultData
{
    public string TaskId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string CodeType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CoderCompletionData
{
    public string TaskId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion 