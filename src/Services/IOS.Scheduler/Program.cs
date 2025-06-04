using IOS.Infrastructure.Extensions;
using IOS.Scheduler.Services;
using IOS.Scheduler.Handlers;
using Quartz;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 添加基础设施服务
builder.Services.AddInfrastructure(builder.Configuration);

// 添加控制器
builder.Services.AddControllers();

// 添加API文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加Quartz调度器
builder.Services.AddQuartz(q =>
{
    // 添加示例任务
    var heartbeatJobKey = new JobKey("HeartbeatJob");
    q.AddJob<HeartbeatJob>(opts => opts.WithIdentity(heartbeatJobKey));

    q.AddTrigger(opts => opts
        .ForJob(heartbeatJobKey)
        .WithIdentity("HeartbeatJob-trigger")
        .WithCronSchedule("0/30 * * * * ?") // 每分钟执行一次
    );
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// 添加核心服务
builder.Services.AddSingleton<SharedDataService>();
builder.Services.AddSingleton<MessageHandlerFactory>();

// 添加MQTT托管服务
builder.Services.AddHostedService<MqttHostedService>();

// 注册所有消息处理器
builder.Services.AddTransient<SystemMessageHandler>();
builder.Services.AddTransient<OutboundTaskHandler>();
builder.Services.AddTransient<DeviceMessageHandler>();
builder.Services.AddTransient<SensorMessageHandler>();
builder.Services.AddTransient<MotionControlHandler>();
builder.Services.AddTransient<VisionMessageHandler>();
builder.Services.AddTransient<CoderMessageHandler>();
builder.Services.AddTransient<DefaultMessageHandler>();

// 添加业务服务
builder.Services.AddScoped<ITaskScheduleService, TaskScheduleService>();
builder.Services.AddScoped<IOutboundTaskService, OutboundTaskService>();

// 添加Windows服务支持
if (OperatingSystem.IsWindows())
{
    builder.Services.AddWindowsService();
}

var app = builder.Build();

// 配置HTTP管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

// 健康检查端点
app.MapHealthChecks("/health");

try
{
    Log.Information("IOS调度服务启动中...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IOS调度服务启动失败");
    throw;
}
finally
{
    Log.Information("IOS调度服务已停止");
    Log.CloseAndFlush();
}