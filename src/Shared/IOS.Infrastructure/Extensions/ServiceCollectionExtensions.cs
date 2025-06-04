using IOS.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Serilog;

namespace IOS.Infrastructure.Extensions;

/// <summary>
/// 依赖注入扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加基础设施服务
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // MQTT服务
        services.Configure<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));
        services.AddSingleton<IMqttService, MqttService>();

        // 内存缓存
        services.AddMemoryCache();

        // 分布式缓存（如果配置了Redis）
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
            });
        }

        // 健康检查
        services.AddHealthChecks()
            .AddCheck<MqttHealthCheck>("mqtt", HealthStatus.Degraded, new[] { "mqtt" });

        // 添加Serilog日志
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        return services;
    }

    /// <summary>
    /// 添加MQTT服务
    /// </summary>
    public static IServiceCollection AddMqtt(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));
        services.AddSingleton<IMqttService, MqttService>();
        return services;
    }

    /// <summary>
    /// 添加分布式缓存
    /// </summary>
    public static IServiceCollection AddDistributedCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    /// <summary>
    /// 添加健康检查
    /// </summary>
    public static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks();

        // MQTT健康检查
        healthChecks.AddCheck<MqttHealthCheck>("mqtt", HealthStatus.Degraded, new[] { "mqtt" });

        // Redis健康检查
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnection))
        {
            healthChecks.AddRedis(redisConnection, "redis", HealthStatus.Degraded, new[] { "redis" });
        }

        return services;
    }

    /// <summary>
    /// 配置Serilog
    /// </summary>
    public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        return services;
    }
}

/// <summary>
/// MQTT健康检查
/// </summary>
public class MqttHealthCheck : IHealthCheck
{
    private readonly IMqttService _mqttService;

    public MqttHealthCheck(IMqttService mqttService)
    {
        _mqttService = mqttService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _mqttService.HealthCheckAsync(cancellationToken);
            return isHealthy
                ? HealthCheckResult.Healthy("MQTT服务正常")
                : HealthCheckResult.Unhealthy("MQTT服务连接失败");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MQTT健康检查异常", ex);
        }
    }
} 