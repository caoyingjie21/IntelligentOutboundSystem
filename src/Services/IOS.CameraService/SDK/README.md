# Sick Vision Camera SDK

一个用于控制Sick视觉设备并获取数据的Python SDK。该SDK支持获取深度数据、强度图像、置信度数据以及3D坐标信息。

## 📋 目录

- [功能特性](#功能特性)
- [环境要求](#环境要求)
- [安装](#安装)
- [快速开始](#快速开始)
- [API文档](#api文档)
- [使用示例](#使用示例)
- [配置说明](#配置说明)
- [故障排除](#故障排除)
- [性能优化](#性能优化)

## 🚀 功能特性

- ✅ **多种数据获取模式**：支持连续流模式和单步模式
- ✅ **丰富的数据类型**：深度数据、强度图像、置信度数据、3D坐标
- ✅ **高性能计算**：使用矩阵化计算优化3D坐标转换
- ✅ **自动重连机制**：内置重试和错误恢复功能
- ✅ **上下文管理**：支持with语句自动资源管理
- ✅ **完整的日志系统**：详细的操作日志和错误追踪

## 📦 环境要求

- Python 3.7+
- NumPy
- OpenCV (cv2)
- Sick Vision Python库（common模块）

## 🔧 安装

1. 确保已安装依赖包：
```bash
pip install numpy opencv-python
```

2. 将SDK文件夹放置到你的项目中，确保目录结构如下：
```
your_project/
├── SickSDK.py
├── common/          # Sick Vision通用模块
├── Qcommon/         # 项目通用模块
└── README.md
```

## ⚡ 快速开始

### 基本使用

```python
from SickSDK import QtVisionSick

# 创建相机实例
camera = QtVisionSick(ipAddr="192.168.10.5", port=2122)

try:
    # 连接相机
    camera.connect()
    
    # 获取完整帧数据
    frame = camera.get_complete_frame()
    if frame.success:
        print(f"获取到 {len(frame.depth_data)} 个深度点")
        print(f"强度图像尺寸: {frame.intensity_image.shape}")
    
finally:
    # 断开连接
    camera.disconnect()
```

### 使用上下文管理器（推荐）

```python
from SickSDK import QtVisionSick

# 使用with语句自动管理连接
with QtVisionSick(ipAddr="192.168.10.5") as camera:
    # 获取深度数据
    depth_data = camera.get_depth_data()
    
    # 获取强度图像
    intensity_img = camera.get_intensity_image()
    
    # 获取3D坐标
    success, coordinates_3d = camera.get_3d_coordinates()
    if success:
        print(f"获取到 {len(coordinates_3d)} 个3D坐标点")
```

## 📚 API文档

### 类：QtVisionSick

#### 初始化参数

```python
QtVisionSick(ipAddr="192.168.10.5", port=2122, protocol="Cola2", use_single_step=False)
```

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| ipAddr | str | "192.168.10.5" | 相机IP地址 |
| port | int | 2122 | 控制端口 |
| protocol | str | "Cola2" | 通信协议 |
| use_single_step | bool | False | 是否使用单步模式 |

#### 主要方法

##### 连接管理

```python
# 连接相机
connect(use_single_step=False) -> bool

# 断开连接
disconnect() -> None

# 检查相机可用性
_check_camera_available() -> bool
```

##### 数据获取

```python
# 获取完整帧数据（推荐）
get_complete_frame() -> CameraFrame

# 获取深度数据
get_depth_data() -> List[float]

# 获取强度图像
get_intensity_image() -> np.ndarray

# 获取强度数据
get_intensity_data() -> List[float]

# 获取置信度数据
get_confidence_data() -> List[float]

# 获取3D坐标
get_3d_coordinates() -> Tuple[bool, List[Tuple[float, float, float]]]

# 获取Z坐标（性能优化版本）
get_z_coordinates() -> List[float]
```

##### 3D坐标计算

```python
# 计算特定像素点的3D坐标
_calculate_3d_coordinates_from_depth(x, y, depth_data, camera_params) -> Tuple[bool, Tuple[float, float, float]]
```

### 数据类：CameraFrame

```python
@dataclass
class CameraFrame:
    success: bool                           # 是否成功获取数据
    depth_data: Optional[List[float]]       # 深度数据
    intensity_image: Optional[np.ndarray]   # 强度图像
    confidence_data: Optional[List[float]]  # 置信度数据
    camera_params: Optional[object]         # 相机参数
    timestamp: Optional[float]              # 时间戳
```

## 💡 使用示例

### 示例1：获取并保存强度图像

```python
import cv2
from SickSDK import QtVisionSick

with QtVisionSick("192.168.10.5") as camera:
    # 获取强度图像
    intensity_img = camera.get_intensity_image()
    
    # 保存图像
    cv2.imwrite("intensity_image.png", intensity_img)
    print("强度图像已保存")
```

### 示例2：实时数据采集

```python
import time
from SickSDK import QtVisionSick

camera = QtVisionSick("192.168.10.5")
camera.connect(use_single_step=False)  # 使用连续流模式

try:
    for i in range(100):  # 采集100帧
        frame = camera.get_complete_frame()
        if frame.success:
            print(f"帧 {i}: 深度点数={len(frame.depth_data)}")
        time.sleep(0.1)  # 100ms间隔
        
finally:
    camera.disconnect()
```

### 示例3：3D点云处理

```python
import numpy as np
from SickSDK import QtVisionSick

with QtVisionSick("192.168.10.5") as camera:
    # 获取3D坐标
    success, coordinates_3d = camera.get_3d_coordinates()
    
    if success:
        # 转换为numpy数组便于处理
        points = np.array(coordinates_3d)
        
        # 过滤有效点（z > 0）
        valid_points = points[points[:, 2] > 0]
        
        print(f"有效3D点数: {len(valid_points)}")
        print(f"Z坐标范围: {valid_points[:, 2].min():.3f} - {valid_points[:, 2].max():.3f}")
```

### 示例4：特定区域3D坐标计算

```python
from SickSDK import QtVisionSick

with QtVisionSick("192.168.10.5") as camera:
    # 获取完整帧数据
    frame = camera.get_complete_frame()
    
    if frame.success:
        # 计算图像中心点的3D坐标
        center_x = frame.camera_params.width // 2
        center_y = frame.camera_params.height // 2
        
        success, (x_3d, y_3d, z_3d) = camera._calculate_3d_coordinates_from_depth(
            center_x, center_y, frame.depth_data, frame.camera_params
        )
        
        if success:
            print(f"中心点3D坐标: ({x_3d:.3f}, {y_3d:.3f}, {z_3d:.3f})")
```

## ⚙️ 配置说明

### 网络配置

确保相机和计算机在同一网络中：

```python
# 默认配置
camera = QtVisionSick(
    ipAddr="192.168.10.5",    # 相机IP地址
    port=2122,                # 控制端口
    protocol="Cola2"          # 通信协议
)
```

### 工作模式

#### 连续流模式（默认）
```python
camera.connect(use_single_step=False)
# 适用于：实时数据采集、高频率获取
```

#### 单步模式
```python
camera.connect(use_single_step=True)
# 适用于：按需获取、低功耗应用
```

## 🔧 故障排除

### 常见问题

#### 1. 连接失败
```
ConnectionError: Camera at 192.168.10.5:2122 is not accessible
```
**解决方案：**
- 检查网络连接
- 确认相机IP地址正确
- 检查防火墙设置
- 验证相机是否已启动

#### 2. 数据获取失败
```
ValueError: No depth map data available
```
**解决方案：**
- 检查相机是否正常工作
- 确认相机镜头未被遮挡
- 重新连接相机

#### 3. 登录失败
```
Service level login failed, trying client level
```
**解决方案：**
- 这是正常行为，SDK会自动尝试客户端级别登录
- 如果两种登录都失败，检查相机固件版本

### 调试技巧

#### 启用详细日志
```python
import logging
logging.basicConfig(level=logging.DEBUG)

# 现在可以看到详细的操作日志
with QtVisionSick("192.168.10.5") as camera:
    frame = camera.get_complete_frame()
```

#### 健康检查
```python
camera = QtVisionSick("192.168.10.5")

# 检查相机可用性
if camera._check_camera_available():
    print("相机网络连接正常")
else:
    print("相机无法访问")
```

## 🚀 性能优化

### 1. 选择合适的数据获取方法

```python
# 如果只需要深度数据
depth_data = camera.get_depth_data()  # 最快

# 如果只需要Z坐标
z_coords = camera.get_z_coordinates()  # 比get_3d_coordinates快60-70%

# 如果需要完整数据
frame = camera.get_complete_frame()  # 一次获取所有数据
```

### 2. 使用连续流模式进行高频采集

```python
# 高效的实时采集
camera.connect(use_single_step=False)
camera.start_continuous_mode()

# 现在可以高频率调用get_frame_no_step()
```

### 3. 批量处理3D坐标

```python
# 一次性获取所有3D坐标，然后批量处理
success, all_coords = camera.get_3d_coordinates()
if success:
    # 使用numpy进行批量计算
    coords_array = np.array(all_coords)
    # ... 批量处理
```

## 📄 许可证

本SDK遵循项目许可证。

## 🤝 贡献

欢迎提交Issue和Pull Request来改进这个SDK。

## 📞 支持

如有问题，请联系开发团队或查看项目文档。

---

**版本**: 1.0.0  
**作者**: Cao Yingjie  
**更新时间**: 2025/04/23 