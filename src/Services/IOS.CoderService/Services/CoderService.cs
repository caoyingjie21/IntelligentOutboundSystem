using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IOS.CoderService.Configuration;
using IOS.CoderService.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IOS.CoderService.Services;

/// <summary>
/// 条码服务实现
/// </summary>
public class CoderService : ICoderService, IDisposable
{
    private readonly ILogger<CoderService> _logger;
    private readonly CoderServiceOptions _options;
    private Socket? _serverSocket;
    private IPEndPoint? _serverEndPoint;
    private readonly ConcurrentDictionary<string, Socket> _clients = new();
    private readonly ConcurrentDictionary<string, ClientInfo> _clientInfos = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// 是否正在运行
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// 连接的客户端数量
    /// </summary>
    public int ConnectedClientsCount => _clients.Count;

    public CoderService(
        ILogger<CoderService> logger,
        IOptions<CoderServiceOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// 启动Socket服务器
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("条码服务已经运行中");
            return;
        }

        try
        {
            _logger.LogInformation("启动条码服务...");

            // 解析IP地址
            if (!IPAddress.TryParse(_options.SocketAddress, out var parsedIpAddress))
            {
                throw new ArgumentException($"无效的IP地址格式: {_options.SocketAddress}");
            }

            // 创建服务器Socket
            _serverEndPoint = new IPEndPoint(parsedIpAddress, _options.SocketPort);
            _serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _serverSocket.Bind(_serverEndPoint);
            _serverSocket.Listen(_options.MaxClients);

            _logger.LogInformation("服务器已启动，监听地址: {Address}:{Port}", _options.SocketAddress, _options.SocketPort);

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // 启动接受连接的任务
            _ = Task.Run(async () => await AcceptClientsAsync(token), token);

            _isRunning = true;
            _logger.LogInformation("条码服务启动成功");

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "启动条码服务失败");
            throw;
        }
    }

    /// <summary>
    /// 停止Socket服务器
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return;
        }

        try
        {
            _logger.LogInformation("停止条码服务...");

            _cancellationTokenSource?.Cancel();

            // 关闭服务器Socket
            if (_serverSocket != null)
            {
                try
                {
                    _serverSocket.Shutdown(SocketShutdown.Both);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "关闭服务器Socket时发生警告");
                }
                finally
                {
                    _serverSocket.Close();
                    _serverSocket = null;
                }
            }

            // 断开所有客户端连接
            await DisconnectAllClientsAsync();

            _isRunning = false;
            _logger.LogInformation("条码服务已停止");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "停止条码服务时发生错误");
        }
    }

    /// <summary>
    /// 获取所有连接的客户端信息
    /// </summary>
    public async Task<IReadOnlyList<ClientInfo>> GetConnectedClientsAsync()
    {
        await Task.CompletedTask;
        return _clientInfos.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取服务状态
    /// </summary>
    public async Task<CoderServiceStatus> GetStatusAsync()
    {
        await Task.CompletedTask;

        return new CoderServiceStatus
        {
            IsRunning = _isRunning,
            ConnectedClients = ConnectedClientsCount,
            ListenAddress = _options.SocketAddress,
            ListenPort = _options.SocketPort,
            MqttConnected = true, // 这里需要根据实际MQTT连接状态设置
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 启动扫码任务
    /// </summary>
    public async Task<CodeInfo> StartScanningAsync(string direction, double stackHeight)
    {
        _logger.LogInformation("启动扫码任务，方向: {Direction}, 堆叠高度: {StackHeight}", direction, stackHeight);

        // 清空之前的消息队列
        await ClearMessageQueueAsync();

        // 收集条码数据
        var codes = await CollectCodesAsync();
        var combinedCodes = string.Join(";", codes.Values);

        var codeInfo = new CodeInfo
        {
            Direction = direction,
            StackHeight = stackHeight,
            Codes = combinedCodes,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("扫码任务完成，收集到 {Count} 个客户端的数据", codes.Count);
        return codeInfo;
    }

    /// <summary>
    /// 收集所有客户端的条码数据
    /// </summary>
    public async Task<Dictionary<string, string>> CollectCodesAsync(CancellationToken cancellationToken = default, int timeoutMs = 5000)
    {
        var responses = new Dictionary<string, string>();

        // 等待一段时间让所有客户端发送数据
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await Task.Delay(timeoutMs, combinedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 超时或取消，继续处理
        }

        // 收集所有客户端的消息
        foreach (var clientInfo in _clientInfos.Values)
        {
            if (clientInfo.Messages.Count > 0)
            {
                var combinedMessages = string.Join(";", clientInfo.Messages);
                responses[clientInfo.EndPoint] = combinedMessages;
                _logger.LogDebug("收到响应: {Messages} 来自 {EndPoint}", combinedMessages, clientInfo.EndPoint);
            }
        }

        _logger.LogInformation("所有客户端响应已接收完毕，共收集 {Count} 个响应", responses.Count);
        return responses;
    }

    /// <summary>
    /// 断开指定客户端连接
    /// </summary>
    public async Task DisconnectClientAsync(string clientEndPoint)
    {
        if (_clients.TryRemove(clientEndPoint, out var clientSocket))
        {
            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
                _logger.LogInformation("客户端 {EndPoint} 已断开连接", clientEndPoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "断开客户端 {EndPoint} 连接时发生警告", clientEndPoint);
            }
        }

        _clientInfos.TryRemove(clientEndPoint, out _);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 清空消息队列
    /// </summary>
    public async Task ClearMessageQueueAsync()
    {
        foreach (var clientInfo in _clientInfos.Values)
        {
            clientInfo.Messages.Clear();
        }

        _logger.LogDebug("消息队列已清空");
        await Task.CompletedTask;
    }

    /// <summary>
    /// 发送消息到指定客户端
    /// </summary>
    public async Task SendMessageToClientAsync(string clientEndPoint, string message)
    {
        if (_clients.TryGetValue(clientEndPoint, out var clientSocket))
        {
            try
            {
                var buffer = Encoding.UTF8.GetBytes(message);
                await clientSocket.SendAsync(buffer, SocketFlags.None);
                _logger.LogDebug("发送消息到 {EndPoint}: {Message}", clientEndPoint, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送消息到客户端 {EndPoint} 失败", clientEndPoint);
                await DisconnectClientAsync(clientEndPoint);
            }
        }
        else
        {
            _logger.LogWarning("客户端 {EndPoint} 未找到", clientEndPoint);
        }
    }

    /// <summary>
    /// 广播消息到所有客户端
    /// </summary>
    public async Task BroadcastMessageAsync(string message)
    {
        var tasks = _clients.Keys.Select(clientEndPoint => 
            SendMessageToClientAsync(clientEndPoint, message)).ToArray();

        await Task.WhenAll(tasks);
        _logger.LogInformation("广播消息到 {Count} 个客户端: {Message}", _clients.Count, message);
    }

    /// <summary>
    /// 接受客户端连接
    /// </summary>
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _serverSocket != null)
        {
            try
            {
                var clientSocket = await _serverSocket.AcceptAsync();
                var clientEndPoint = clientSocket.RemoteEndPoint?.ToString() ?? "Unknown";

                _logger.LogInformation("客户端已连接: {EndPoint}", clientEndPoint);

                _clients.TryAdd(clientEndPoint, clientSocket);
                _clientInfos.TryAdd(clientEndPoint, new ClientInfo
                {
                    EndPoint = clientEndPoint,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow
                });

                // 启动处理客户端的任务
                _ = Task.Run(async () => await HandleClientAsync(clientSocket, clientEndPoint, cancellationToken), 
                    cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Socket已被释放，正常退出
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "接受客户端连接时发生错误");
                }
            }
        }
    }

    /// <summary>
    /// 处理客户端通信
    /// </summary>
    private async Task HandleClientAsync(Socket clientSocket, string clientEndPoint, CancellationToken cancellationToken)
    {
        var buffer = new byte[_options.ReceiveBufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await clientSocket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);
                
                if (bytesRead == 0)
                {
                    _logger.LogInformation("客户端已断开连接: {EndPoint}", clientEndPoint);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                _logger.LogDebug("收到消息: {Message} 来自 {EndPoint}", message, clientEndPoint);

                // 更新客户端信息
                if (_clientInfos.TryGetValue(clientEndPoint, out var clientInfo))
                {
                    clientInfo.LastActivity = DateTime.UtcNow;
                    clientInfo.Messages.Add(message);
                }
            }
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "处理客户端 {EndPoint} 通信时发生错误", clientEndPoint);
            }
        }
        finally
        {
            await DisconnectClientAsync(clientEndPoint);
        }
    }

    /// <summary>
    /// 断开所有客户端连接
    /// </summary>
    private async Task DisconnectAllClientsAsync()
    {
        var disconnectTasks = _clients.Keys.ToList()
            .Select(clientEndPoint => DisconnectClientAsync(clientEndPoint));

        await Task.WhenAll(disconnectTasks);
        _clients.Clear();
        _clientInfos.Clear();
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            var stopTask = StopAsync();
            if (!stopTask.Wait(TimeSpan.FromSeconds(10)))
            {
                _logger.LogWarning("停止条码服务超时");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "释放条码服务资源时发生错误");
        }

        _cancellationTokenSource?.Dispose();
        _disposed = true;
    }
} 