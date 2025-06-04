# IntelligentOutboundSystem (智能出库系统)

基于现代.NET架构的智能出库自动化系统框架，专为工业自动化环境设计。

## 项目概述

智能出库系统是一个模块化的工业自动化解决方案，采用微服务架构，支持：
- 任务调度与管理
- MQTT消息通信
- 机器视觉检测
- 运动控制
- 二维码读取
- 实时监控与健康检查

## 技术栈

- **.NET 8.0** - 现代化的开发平台
- **ASP.NET Core** - Web API 框架
- **MQTT** - 设备间通信协议
- **Quartz.NET** - 任务调度框架
- **Serilog** - 结构化日志记录
- **Redis** - 分布式缓存（可选）
- **Swagger** - API 文档

## 项目结构

```
intelligentoutboundsystem/
├── src/
│   ├── Shared/                     # 共享库
│   │   ├── IOS.Shared/            # 核心共享模型
│   │   └── IOS.Infrastructure/    # 基础设施组件
│   ├── Services/                   # 微服务
│   │   ├── IOS.Gateway/           # API网关
│   │   ├── IOS.Scheduler/         # 调度服务
│   │   ├── IOS.CoderService/      # 读码服务
│   │   ├── IOS.MotionControl/     # 运动控制服务
│   │   ├── IOS.DataMiddleware/    # 数据中间件
│   │   └── IOS.VisionService/     # 视觉服务
│   ├── UI/                        # 用户界面
│   │   └── IOS.UI.Admin/          # 管理界面
│   └── Tests/                     # 测试项目
│       ├── IOS.Tests.Unit/        # 单元测试
│       └── IOS.Tests.Integration/ # 集成测试
├── appsettings.json               # 配置文件
├── IntelligentOutboundSystem.sln  # 解决方案文件
└── README.md                      # 项目说明
```

## 核心功能

### 1. 标准化消息通信
- 统一的消息格式 (`StandardMessage<T>`)
- 支持多种消息类型：命令、事件、查询、响应等
- 消息优先级管理
- 关联ID跟踪

### 2. 改进的MQTT服务
- 连接池管理
- 自动重连机制
- 健康检查支持
- 结构化日志记录

### 3. 任务调度系统
- 基于Quartz.NET的调度引擎
- 支持Cron表达式
- 任务优先级管理
- 实时进度跟踪

### 4. 数据访问层
- 仓储模式实现
- 缓存策略支持
- 工作单元模式

### 5. 健康检查
- MQTT连接监控
- Redis连接检查
- 自定义健康检查支持

## 快速开始

### 1. 环境要求

- .NET 8.0 SDK
- Visual Studio 2022 或 VS Code
- MQTT Broker (如 EMQX 或 Mosquitto)
- Redis (可选，用于分布式缓存)

### 2. 配置

编辑 `appsettings.json` 文件，配置：

```json
{
  "Mqtt": {
    "Broker": "localhost",
    "Port": 1883,
    "ClientId": "IOS-Service"
  },
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### 3. 运行服务

```bash
# 进入调度服务目录
cd src/Services/IOS.Scheduler

# 运行服务
dotnet run
```

### 4. 访问API文档

服务启动后，访问 Swagger 文档：
- 开发环境: `https://localhost:5001/swagger`

## 配置说明

### MQTT配置
```json
{
  "Mqtt": {
    "Broker": "localhost",           // MQTT服务器地址
    "Port": 1883,                   // 端口
    "ClientId": "IOS-Service",      // 客户端ID
    "Username": "",                 // 用户名（可选）
    "Password": "",                 // 密码（可选）
    "KeepAlivePeriod": 30,          // 心跳间隔（秒）
    "ConnectionTimeout": 10,        // 连接超时（秒）
    "ReconnectInterval": 5,         // 重连间隔（秒）
    "MaxReconnectAttempts": 5,      // 最大重连次数
    "UseTls": false,               // 是否使用TLS
    "CleanSession": true           // 清除会话
  }
}
```

### 系统配置
```json
{
  "SystemConfig": {
    "ServiceName": "IntelligentOutboundSystem",
    "Version": "1.0.0",
    "Environment": "Development",
    "HeartbeatInterval": 60,        // 心跳间隔（秒）
    "TaskTimeout": 300,             // 任务超时（秒）
    "MaxConcurrentTasks": 10,       // 最大并发任务数
    "EnablePerformanceMonitoring": true,
    "EnableHealthChecks": true
  }
}
```

## MQTT主题设计

### 系统主题
- `system/heartbeat` - 系统心跳
- `system/status` - 系统状态
- `system/config` - 配置更新

### 任务主题
- `outbound/task/created` - 任务创建
- `outbound/task/execute` - 任务执行
- `outbound/task/progress` - 任务进度
- `outbound/task/completed` - 任务完成
- `outbound/task/cancelled` - 任务取消

### 设备主题
- `device/{deviceId}/status` - 设备状态
- `device/{deviceId}/command` - 设备命令
- `device/{deviceId}/response` - 设备响应

## 开发指南

### 添加新的微服务

1. 创建新的项目
2. 引用共享库
3. 实现业务逻辑
4. 配置依赖注入
5. 添加健康检查

### 自定义消息处理

```csharp
public class CustomMessageHandler
{
    private readonly IMqttService _mqttService;

    public CustomMessageHandler(IMqttService mqttService)
    {
        _mqttService = mqttService;
        _mqttService.OnMessageReceived += HandleMessage;
    }

    private async Task HandleMessage(string topic, string payload)
    {
        // 处理自定义消息逻辑
    }
}
```

### 添加调度任务

```csharp
var task = new ScheduleTask
{
    Name = "CustomTask",
    CronExpression = "0 */5 * * * ?", // 每5分钟执行
    JobType = "CustomJob",
    Parameters = new Dictionary<string, object>
    {
        ["param1"] = "value1"
    }
};

var taskId = await _scheduleService.CreateScheduleAsync(task);
```

## 监控与运维

### 健康检查
访问 `/health` 端点获取系统健康状态

### 日志
- 控制台日志：实时查看
- 文件日志：`logs/` 目录下
- 日志级别：通过配置调整

### 性能监控
- 启用性能监控功能
- 记录关键指标
- 支持自定义指标

## 部署

### Windows服务部署
```bash
# 发布应用
dotnet publish -c Release

# 安装为Windows服务
sc create "IOSScheduler" binPath="path\to\IOS.Scheduler.exe"
```

### 手动部署
```bash
# 发布应用
dotnet publish -c Release -o publish

# 运行服务
cd publish
dotnet IOS.Scheduler.dll
```

## 贡献指南

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 许可证

本项目采用 MIT 许可证。详情请参阅 LICENSE 文件。

## 联系方式

如有问题或建议，请联系项目维护者。

---

## 版本历史

### v1.0.0 (当前版本)
- 初始版本发布
- 基础微服务架构
- MQTT通信支持
- 任务调度功能
- 健康检查机制 