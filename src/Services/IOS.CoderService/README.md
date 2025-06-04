# IOS.CoderService - 条码扫描服务

## 概述

IOS.CoderService是智能出库系统中的条码扫描服务，负责管理和协调多个条码扫描客户端，通过Socket通信收集条码数据，并通过MQTT消息总线与其他系统组件进行集成。该服务基于.NET 8构建，集成了Socket服务器、MQTT通信和RESTful API功能。

## 主要功能

### 🚀 核心功能
- **Socket服务器**：监听TCP连接，接收多个客户端的条码扫描数据
- **MQTT集成**：通过MQTT接收扫码命令和发布条码数据
- **RESTful API**：提供HTTP API进行手动控制和状态查询
- **客户端管理**：管理多个扫码设备连接，实时监控连接状态
- **数据聚合**：收集并整合来自不同客户端的条码数据
- **消息队列**：缓存和管理客户端消息

### 📡 MQTT主题

#### 接收主题（订阅）
- `coder/start` - 启动扫码任务
- `coder/config` - 配置查询
- `coder/stop` - 停止扫码任务
- `order` - 订单信息

#### 发送主题（发布）
- `coder/odoo` - 条码数据（发送给业务系统）
- `get_order` - 获取订单请求
- `coder/status` - 状态信息

### 🔧 API端点

- `GET /api/coder/status` - 获取服务状态
- `GET /api/coder/clients` - 获取连接的客户端列表
- `POST /api/coder/start-scanning` - 启动扫码任务
- `POST /api/coder/collect-codes` - 收集条码数据
- `DELETE /api/coder/clients/{endPoint}` - 断开指定客户端
- `POST /api/coder/clear-queue` - 清空消息队列
- `POST /api/coder/clients/{endPoint}/send` - 发送消息到客户端
- `POST /api/coder/broadcast` - 广播消息到所有客户端
- `POST /api/coder/start` - 启动服务
- `POST /api/coder/stop` - 停止服务

## 配置说明

### appsettings.json

```json
{
  "CoderService": {
    "SocketAddress": "0.0.0.0",     // Socket监听地址
    "SocketPort": 5000,             // Socket监听端口
    "MaxClients": 10,               // 最大客户端连接数
    "ReceiveBufferSize": 1024,      // 接收缓冲区大小
    "ClientTimeout": 30000,         // 客户端超时时间（毫秒）
    "Topics": {
      "Receives": {
        "Start": "coder/start",
        "Config": "coder/config",
        "Stop": "coder/stop",
        "Order": "order"
      },
      "Sends": {
        "Coder": "coder/odoo",
        "Order": "get_order",
        "Status": "coder/status"
      }
    }
  }
}
```

## 工作流程

### 扫码任务流程
1. **接收启动命令**：通过MQTT接收`coder/start`消息，包含方向和堆叠高度信息
2. **准备扫码**：清空之前的消息队列，延迟500ms等待客户端准备
3. **收集数据**：等待5秒收集所有连接客户端的条码数据
4. **请求订单**：发布`get_order`消息请求订单信息
5. **接收订单**：通过`order`主题接收订单号
6. **发布结果**：将完整的条码信息发布到`coder/odoo`主题

### 消息格式

#### 启动扫码消息格式
```
{direction};{stackHeight}
```
示例：`入库;150.5`

#### 条码数据格式
```json
{
  "messageId": "uuid",
  "timestamp": "2024-01-01T00:00:00Z",
  "source": "CoderService",
  "messageType": "Response",
  "data": {
    "order": "ORDER123",
    "codes": "CODE1;CODE2;CODE3",
    "direction": "入库",
    "stackHeight": 150.5,
    "timestamp": "2024-01-01T00:00:00Z"
  }
}
```

#### 客户端状态格式
```json
{
  "isRunning": true,
  "connectedClients": 3,
  "listenAddress": "0.0.0.0",
  "listenPort": 5000,
  "mqttConnected": true,
  "timestamp": "2024-01-01T00:00:00Z"
}
```

## 客户端集成

### Socket客户端要求
- **协议**：TCP
- **编码**：UTF-8
- **连接方式**：主动连接到服务器
- **数据格式**：纯文本，每个条码一条消息

### 客户端连接示例（C#）
```csharp
using var client = new TcpClient();
await client.ConnectAsync("192.168.1.100", 5000);
using var stream = client.GetStream();

// 发送条码数据
var message = "12345678901234";
var buffer = Encoding.UTF8.GetBytes(message);
await stream.WriteAsync(buffer);
```

### 客户端连接示例（Python）
```python
import socket

client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client.connect(('192.168.1.100', 5000))

# 发送条码数据
message = "12345678901234"
client.send(message.encode('utf-8'))
client.close()
```

## 运行要求

### 系统要求
- Windows 10/11 或 Linux
- .NET 8 Runtime
- 网络连接（用于MQTT和Socket通信）

### 网络要求
- Socket端口（默认5000）可访问
- MQTT服务器连接正常
- 与条码扫描设备的网络连通性

## 启动和部署

### 开发环境运行
```bash
cd intelligentoutboundsystem/src/Services/IOS.CoderService
dotnet run
```

### 生产环境部署
```bash
# 发布应用
dotnet publish -c Release -o ./publish

# Windows服务安装
sc create "IOS.CoderService" binPath="C:\path\to\publish\IOS.CoderService.exe"
sc start "IOS.CoderService"

# Linux systemd服务
sudo systemctl enable ios-coderservice.service
sudo systemctl start ios-coderservice.service
```

## 日志和监控

### 日志配置
- **控制台输出**：开发环境实时查看
- **文件日志**：生产环境保存在`logs/coder-service-{date}.log`

### 监控指标
- 连接的客户端数量
- 消息处理速度
- 错误率和响应时间
- MQTT连接状态

## 故障排除

### 常见问题

1. **Socket连接失败**
   - 检查端口是否被占用
   - 验证防火墙设置
   - 确认客户端网络连接

2. **MQTT连接失败**
   - 检查MQTT服务器地址和端口
   - 验证认证信息
   - 检查网络连接状态

3. **客户端数据丢失**
   - 检查网络稳定性
   - 增加超时时间配置
   - 查看详细错误日志

### 调试技巧
- 使用API端点检查服务状态
- 通过日志追踪消息流
- 使用MQTT客户端工具测试消息
- 检查客户端连接列表

## 性能优化

### 配置建议
- 根据客户端数量调整`MaxClients`
- 根据网络环境调整`ClientTimeout`
- 适当设置`ReceiveBufferSize`

### 扩展性考虑
- 支持水平扩展（多实例部署）
- 负载均衡配置
- 数据库持久化选项

## 扩展开发

### 添加新功能
1. 在`ICoderService`接口中添加新方法
2. 在`CoderService`中实现具体逻辑
3. 在`CoderController`中添加对应的API端点
4. 在`CoderHostedService`中添加MQTT处理逻辑

### 支持新的消息格式
- 扩展`CodeInfo`模型
- 更新消息序列化逻辑
- 修改API响应格式

## 安全考虑

1. **网络安全**：使用VPN或专用网络
2. **认证授权**：API端点添加认证
3. **数据加密**：敏感数据传输加密
4. **访问控制**：限制客户端IP范围

## 版本历史

- **v1.0.0** - 初始版本，支持基本的Socket服务器和MQTT集成
- 基于原始CoderService项目迁移，适配IOS架构 