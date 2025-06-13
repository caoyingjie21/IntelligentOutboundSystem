using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace IOS.Scheduler.Services;

/// <summary>
/// 线程安全的共享数据服务
/// </summary>
public class SharedDataService
{
    private readonly ConcurrentDictionary<string, object> _data;
    private readonly ILogger<SharedDataService> _logger;
    private readonly object _lockObject = new();

    public SharedDataService(ILogger<SharedDataService> logger)
    {
        _data = new ConcurrentDictionary<string, object>();
        _logger = logger;
    }

    /// <summary>
    /// 设置数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void SetData<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("键不能为空", nameof(key));
        }

        try
        {
            _data.AddOrUpdate(key, value!, (k, oldValue) => value!);
            _logger.LogDebug("设置共享数据: Key={Key}, Type={Type}", key, typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "设置共享数据失败: Key={Key}, Type={Type}", key, typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// 获取数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <returns>数据值</returns>
    public T? GetData<T>(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("键不能为空", nameof(key));
        }

        try
        {
            if (_data.TryGetValue(key, out var value) && value is T result)
            {
                _logger.LogDebug("获取共享数据成功: Key={Key}, Type={Type}", key, typeof(T).Name);
                return result;
            }

            _logger.LogDebug("获取共享数据失败: Key={Key}, Type={Type}", key, typeof(T).Name);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取共享数据异常: Key={Key}, Type={Type}", key, typeof(T).Name);
            return default;
        }
    }

    /// <summary>
    /// 尝试获取数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="value">输出值</param>
    /// <returns>是否成功获取</returns>
    public bool TryGetData<T>(string key, out T? value)
    {
        value = default;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        try
        {
            if (_data.TryGetValue(key, out var storedValue) && storedValue is T result)
            {
                value = result;
                _logger.LogDebug("尝试获取共享数据成功: Key={Key}, Type={Type}", key, typeof(T).Name);
                return true;
            }

            _logger.LogDebug("尝试获取共享数据失败: Key={Key}, Type={Type}", key, typeof(T).Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "尝试获取共享数据异常: Key={Key}, Type={Type}", key, typeof(T).Name);
            return false;
        }
    }

    /// <summary>
    /// 检查是否包含指定键
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>是否包含</returns>
    public bool ContainsKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        return _data.ContainsKey(key);
    }

    /// <summary>
    /// 移除数据
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>是否成功移除</returns>
    public bool RemoveData(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        try
        {
            var removed = _data.TryRemove(key, out _);
            if (removed)
            {
                _logger.LogDebug("移除共享数据成功: Key={Key}", key);
            }
            return removed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "移除共享数据异常: Key={Key}", key);
            return false;
        }
    }

    /// <summary>
    /// 清空所有数据
    /// </summary>
    public void ClearAll()
    {
        try
        {
            lock (_lockObject)
            {
                var count = _data.Count;
                _data.Clear();
                _logger.LogInformation("清空所有共享数据，数量: {Count}", count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清空共享数据异常");
            throw;
        }
    }

    /// <summary>
    /// 获取所有键
    /// </summary>
    /// <returns>所有键的集合</returns>
    public IEnumerable<string> GetAllKeys()
    {
        try
        {
            return _data.Keys.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有键异常");
            return Enumerable.Empty<string>();
        }
    }

    /// <summary>
    /// 获取数据数量
    /// </summary>
    /// <returns>数据数量</returns>
    public int Count => _data.Count;

    /// <summary>
    /// 获取数据数量（方法形式）
    /// </summary>
    /// <returns>数据数量</returns>
    public int GetDataCount()
    {
        return _data.Count;
    }

    /// <summary>
    /// 获取所有数据
    /// </summary>
    /// <returns>所有数据的字典</returns>
    public Dictionary<string, object> GetAllData()
    {
        try
        {
            return _data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取所有数据异常");
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 原子性更新数据
    /// </summary>
    /// <typeparam name="T">数据类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="updateFunc">更新函数</param>
    /// <returns>更新后的值</returns>
    public T? UpdateData<T>(string key, Func<T?, T> updateFunc)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("键不能为空", nameof(key));
        }

        if (updateFunc == null)
        {
            throw new ArgumentNullException(nameof(updateFunc));
        }

        try
        {
            var updatedValue = _data.AddOrUpdate(key,
                // 添加新值
                k => updateFunc(default),
                // 更新现有值
                (k, oldValue) => updateFunc(oldValue is T existingValue ? existingValue : default));

            _logger.LogDebug("原子性更新共享数据: Key={Key}, Type={Type}", key, typeof(T).Name);
            return updatedValue is T result ? result : default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "原子性更新共享数据异常: Key={Key}, Type={Type}", key, typeof(T).Name);
            throw;
        }
    }
} 