using IOS.CoderService.Models;

namespace IOS.CoderService.Services;

/// <summary>
/// 条码服务接口
/// </summary>
public interface ICoderService
{
    /// <summary>
    /// 是否正在运行
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// 连接的客户端数量
    /// </summary>
    int ConnectedClientsCount { get; }

    /// <summary>
    /// 启动Socket服务器
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 停止Socket服务器
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有连接的客户端信息
    /// </summary>
    Task<IReadOnlyList<ClientInfo>> GetConnectedClientsAsync();

    /// <summary>
    /// 获取服务状态
    /// </summary>
    Task<CoderServiceStatus> GetStatusAsync();

    /// <summary>
    /// 启动扫码任务
    /// </summary>
    /// <param name="direction">方向</param>
    /// <param name="stackHeight">堆叠高度</param>
    Task<CodeInfo> StartScanningAsync(string direction, double stackHeight);

    /// <summary>
    /// 收集所有客户端的条码数据
    /// </summary>
    Task<Dictionary<string, string>> CollectCodesAsync(CancellationToken cancellationToken = default, int timeoutMs = 5000);

    /// <summary>
    /// 断开指定客户端连接
    /// </summary>
    Task DisconnectClientAsync(string clientEndPoint);

    /// <summary>
    /// 清空消息队列
    /// </summary>
    Task ClearMessageQueueAsync();

    /// <summary>
    /// 发送消息到指定客户端
    /// </summary>
    Task SendMessageToClientAsync(string clientEndPoint, string message);

    /// <summary>
    /// 广播消息到所有客户端
    /// </summary>
    Task BroadcastMessageAsync(string message);
} 