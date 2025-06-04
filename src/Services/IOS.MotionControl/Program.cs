using Serilog;
using IOS.Infrastructure.Extensions;
using IOS.MotionControl.Configuration;
using IOS.MotionControl.Services;
using Core.Net.EtherCAT;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 添加基础设施服务
builder.Services.AddInfrastructure(builder.Configuration);

// 添加健康检查
builder.Services.AddHealthChecks();

// 配置运动控制选项
builder.Services.Configure<MotionControlOptions>(
    builder.Configuration.GetSection("MotionControl"));

// 注册EtherCAT主站
builder.Services.AddSingleton<EtherCATMaster>();

// 注册运动控制服务
builder.Services.AddSingleton<IMotionControlService, MotionControlService>();

// 添加托管服务
builder.Services.AddHostedService<MotionControlHostedService>();

// 添加控制器支持
builder.Services.AddControllers();

// 添加API文档
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 如果是Windows系统，添加Windows服务支持
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
    Log.Information("IOS运动控制服务启动中...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IOS运动控制服务启动失败");
    throw;
}
finally
{
    Log.Information("IOS运动控制服务已停止");
    Log.CloseAndFlush();
} 