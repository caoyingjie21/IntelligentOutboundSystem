using Serilog;
using IOS.Infrastructure.Extensions;
using IOS.CoderService.Configuration;
using IOS.CoderService.Services;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// 添加基础设施服务
builder.Services.AddInfrastructure(builder.Configuration);

// 添加增强MQTT服务
builder.Services.AddEnhancedMqtt(builder.Configuration, "CoderService");

// 添加健康检查
builder.Services.AddHealthChecks();

// 配置条码服务选项
builder.Services.Configure<CoderServiceOptions>(
    builder.Configuration.GetSection("CoderService"));

// 注册条码服务
builder.Services.AddSingleton<ICoderService, CoderService>();

// 添加托管服务
builder.Services.AddHostedService<CoderHostedService>();

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
    Log.Information("IOS条码服务启动中...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IOS条码服务启动失败");
    throw;
}
finally
{
    Log.Information("IOS条码服务已停止");
    Log.CloseAndFlush();
} 