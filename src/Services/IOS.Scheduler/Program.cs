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

// 添加增强MQTT服务
builder.Services.AddEnhancedMqtt(builder.Configuration, "Scheduler");

// 添加控制器
builder.Services.AddControllers();

// 添加API文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 添加静态文件服务支持
builder.Services.AddDirectoryBrowser();

// 添加Quartz调度器
builder.Services.AddQuartz(q =>
{
    // 配置Quartz但不添加预定义任务
    // 任务将通过TaskScheduleService动态创建
});

// 注释掉QuartzHostedService以避免"Batch acquisition of 0 triggers"日志
// builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// 添加核心服务
builder.Services.AddSingleton<SharedDataService>();
builder.Services.AddSingleton<MessageHandlerFactory>();

// 添加MQTT托管服务
builder.Services.AddHostedService<MqttHostedService>();

// 注册所有消息处理器
builder.Services.AddTransient<SensorMessageHandler>();
builder.Services.AddTransient<MotionControlHandler>();
builder.Services.AddTransient<VisionMessageHandler>();
builder.Services.AddTransient<CoderMessageHandler>();
builder.Services.AddTransient<DefaultMessageHandler>();

// 添加业务服务
builder.Services.AddScoped<ITaskScheduleService, TaskScheduleService>();

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

// 配置默认文件（必须在UseStaticFiles之前）
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});

// 启用静态文件服务
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true
});

app.UseDirectoryBrowser();

app.UseRouting();

app.UseAuthorization();

// 根路径重定向到管理界面（在MapControllers之前）
app.MapGet("/", async context =>
{
    context.Response.Redirect("/index.html", permanent: false);
});

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