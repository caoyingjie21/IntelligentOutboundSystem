# IOS.MotionControl - 运动控制服务

## 概述

IOS.MotionControl是智能出库系统中的运动控制服务，负责管理和控制EtherCAT运动轴的操作。该服务基于.NET 8构建，集成了MQTT通信、RESTful API和EtherCAT运动控制功能。

## 主要功能

### 🚀 核心功能
- **EtherCAT运动控制**：支持CiA402标准的运动控制
- **MQTT集成**：通过MQTT接收运动命令和发布状态信息
- **RESTful API**：提供HTTP API进行手动控制和状态查询
- **实时状态监控**：定期发布轴位置和状态信息
- **安全保护**：位置范围检查、错误处理和安全关闭

### 📡 MQTT主题

#### 接收主题（订阅）
- `motion/moving` - 运动命令
- `motion/back` - 回零命令  
- `motion/config` - 配置查询命令

#### 发送主题（发布）
- `motion/moving/complete` - 运动完成通知
- `motion/position` - 位置信息
- `motion/status` - 状态信息

### 🔧 API端点

- `GET /api/motion/status` - 获取运动状态
- `POST /api/motion/move-absolute` - 绝对运动
- `POST /api/motion/move-relative` - 相对运动
- `POST /api/motion/home` - 回零操作
- `POST /api/motion/stop` - 停止运动
- `POST /api/motion/initialize` - 初始化系统

## 配置说明

### appsettings.json

```json
{
  "MotionControl": {
    "EtherNet": "CNet",           // EtherCAT网络接口名称
    "Speed": 50000,               // 默认运动速度
    "MinPosition": 0,             // 最小位置限制
    "MaxPosition": 220000,        // 最大位置限制
    "SlaveId": 1,                 // 从站ID
    "Topics": {
      "Receives": {
        "Moving": "motion/moving",
        "Back": "motion/back",
        "Config": "motion/config"
      },
      "Sends": {
        "MovingComplete": "motion/moving/complete",
        "Position": "motion/position",
        "Status": "motion/status"
      }
    }
  }
}
```

## 消息格式

### 运动命令消息格式
```
{taskId};{distanceInMm}
```
示例：`TASK001;150.5`

### 运动完成响应格式
```
{taskId};done
```
示例：`TASK001;done`

### 状态消息格式
```json
{
  "messageId": "uuid",
  "timestamp": "2024-01-01T00:00:00Z",
  "source": "MotionControl",
  "messageType": "Status",
  "data": {
    "position": 12000,
    "isEnabled": true,
    "isMoving": false,
    "hasError": false,
    "errorMessage": null,
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

## 运行要求

### 系统要求
- Windows 10/11 或 Windows Server 2019+
- .NET 8 Runtime
- EtherCAT网络适配器
- x86平台（EtherCAT库要求）

### 硬件要求
- 支持EtherCAT的运动控制器
- 网络接口卡配置为EtherCAT网络

## 启动和部署

### 开发环境运行
```bash
cd intelligentoutboundsystem/src/Services/IOS.MotionControl
dotnet run
```

### 生产环境部署
```bash
# 发布应用
dotnet publish -c Release -o ./publish

# 安装为Windows服务
sc create "IOS.MotionControl" binPath="C:\path\to\publish\IOS.MotionControl.exe"
sc start "IOS.MotionControl"
```

## 日志

服务使用Serilog进行日志记录：
- 控制台输出：实时查看运行状态
- 文件日志：保存在 `logs/motion-control-{date}.log`

日志级别：
- Information：正常操作信息
- Warning：警告信息
- Error：错误信息
- Debug：调试信息（开发模式）

## 安全考虑

1. **位置限制**：严格检查运动位置在安全范围内
2. **错误处理**：运动错误时自动停止并记录
3. **安全关闭**：服务停止时自动回零并断电
4. **通信安全**：MQTT支持TLS加密（生产环境建议启用）

## 故障排除

### 常见问题

1. **EtherCAT连接失败**
   - 检查网络接口配置
   - 确认EtherCAT从站连接
   - 验证网络接口名称配置

2. **MQTT连接失败**
   - 检查MQTT服务器地址和端口
   - 验证网络连接
   - 检查认证信息

3. **运动异常**
   - 检查轴是否正确启用
   - 验证位置范围设置
   - 查看详细错误日志

## 扩展开发

### 添加新的运动控制功能
1. 在 `IMotionControlService` 接口中添加新方法
2. 在 `MotionControlService` 中实现具体逻辑
3. 在 `MotionController` 中添加对应的API端点
4. 在 `MotionControlHostedService` 中添加MQTT处理逻辑

### 支持多轴控制
- 扩展 `MotionControlOptions` 配置多个轴
- 修改 `MotionControlService` 支持轴选择
- 更新API和MQTT消息格式

## 版本历史

- **v1.0.0** - 初始版本，支持单轴EtherCAT控制
- 基于原始MotionControl项目迁移，适配IOS架构 