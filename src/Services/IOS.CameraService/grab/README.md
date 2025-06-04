# 西克相机图像采集程序

本目录包含用于西克(SICK)相机图像采集的程序。

## 文件说明

### 1. `grab_images.py` - 完整功能采集程序
功能最全面的交互式图像采集程序，支持：
- 单张图像采集
- 批量图像采集
- 完整的元数据保存
- 交互式用户界面

### 2. `quick_grab.py` - 快速采集工具
简化版的命令行工具，用于快速采集图像：
- 支持命令行参数
- 快速采集指定数量的图像
- 自动生成时间戳文件名

### 3. `continuous_stream_demo.py` - 连续流演示程序 🆕
演示连续流功能的完整示例程序：
- 生成器模式连续流
- 回调函数模式连续流
- 后台线程模式连续流
- 性能测试模式

## 使用方法

### 使用完整功能程序

```bash
cd intelligentoutboundsystem/src/Services/IOS.CameraService/grab
python grab_images.py
```

程序启动后会显示交互菜单：
```
=== 西克相机图像采集程序 ===

请选择操作:
1. 采集单张图像
2. 连续采集多张图像
3. 退出程序
```

### 使用快速采集工具

#### 采集单张图像（默认）
```bash
python quick_grab.py
```

#### 采集多张图像
```bash
python quick_grab.py --count 10
```

#### 指定相机IP和保存目录
```bash
python quick_grab.py --ip 192.168.10.100 --count 5 --dir ./my_data
```

#### 查看帮助
```bash
python quick_grab.py --help
```

### 使用连续流演示程序 🆕

#### 生成器模式（默认）
```bash
python continuous_stream_demo.py --mode generator
```

#### 回调函数模式
```bash
python continuous_stream_demo.py --mode callback
```

#### 后台线程模式
```bash
python continuous_stream_demo.py --mode thread
```

#### 性能测试模式
```bash
python continuous_stream_demo.py --mode performance
```

#### 指定参数
```bash
python continuous_stream_demo.py --ip 192.168.10.100 --dir ./stream_data --mode callback
```

## 连续流功能详解 🆕

### QtVisionSick 类新增方法

#### 1. `get_continuous_stream()` - 连续流生成器
```python
def get_continuous_stream(self, 
                         callback: Optional[Callable] = None, 
                         max_frames: Optional[int] = None,
                         timeout: Optional[float] = None) -> Generator:
```

**功能特点：**
- 支持生成器模式和回调函数模式
- 可设置最大帧数和超时时间
- 自动处理异常和资源清理

**使用示例：**

```python
# 生成器模式
for success, depth_data, intensity_image, camera_params in camera.get_continuous_stream(max_frames=100):
    if success:
        cv2.imshow('Live Stream', intensity_image)
        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

# 回调函数模式
def process_frame(success, depth_data, intensity_image, camera_params):
    if success:
        cv2.imshow('Live Stream', intensity_image)

for _ in camera.get_continuous_stream(callback=process_frame, timeout=30.0):
    if cv2.waitKey(1) & 0xFF == ord('q'):
        break
```

#### 2. `start_continuous_stream_thread()` - 后台线程模式
```python
def start_continuous_stream_thread(self, 
                                 callback: Callable,
                                 max_frames: Optional[int] = None,
                                 frame_interval: float = 0.033) -> bool:
```

**功能特点：**
- 在后台线程中处理图像流
- 主线程可以处理用户界面或其他任务
- 支持帧率控制
- 线程安全的启停控制

**使用示例：**

```python
def frame_processor(success, depth_data, intensity_image, camera_params):
    if success:
        # 处理图像数据
        cv2.imshow('Background Stream', intensity_image)

# 启动后台线程
camera.start_continuous_stream_thread(frame_processor, frame_interval=0.033)

# 主线程处理其他任务
while camera.is_streaming_active():
    key = cv2.waitKey(100) & 0xFF
    if key == ord('q'):
        break

# 停止后台线程
camera.stop_continuous_stream_thread()
```

#### 3. 辅助方法

- `stop_continuous_stream_thread()` - 停止后台线程
- `is_streaming_active()` - 检查流是否活动
- `start_continuous_mode()` - 切换到连续模式

### 连续流模式对比

| 模式 | 优点 | 缺点 | 适用场景 |
|------|------|------|----------|
| **生成器模式** | 简单直观，内存效率高 | 阻塞主线程 | 简单的图像处理任务 |
| **回调函数模式** | 灵活的处理逻辑 | 阻塞主线程 | 复杂的图像处理逻辑 |
| **后台线程模式** | 不阻塞主线程，支持并发 | 复杂度较高 | 实时显示+用户交互 |

## 输出文件结构

程序会在指定的保存目录中创建以下结构：

```
data/
├── intensity/              # 强度图像文件（单次采集）
│   ├── image_20240101_120000_001_intensity.png
│   └── ...
├── depth/                  # 深度数据文件（单次采集）
│   ├── image_20240101_120000_001_depth.npy
│   └── ...
├── metadata/               # 元数据文件（仅完整功能程序）
│   ├── image_20240101_120000_001_metadata.json
│   └── ...
├── stream_intensity/       # 连续流强度图像 🆕
│   ├── stream_intensity_20240101_120000_001.png
│   └── ...
└── stream_depth/          # 连续流深度数据 🆕
    ├── stream_depth_20240101_120000_001.npy
    └── ...
```

## 文件格式说明

### 强度图像文件 (.png)
- 格式：PNG图像文件
- 内容：相机获取的强度图像数据
- 可以使用任何图像查看器打开

### 深度数据文件 (.npy)
- 格式：NumPy数组文件
- 内容：深度图数据，形状为 [height, width]
- 读取方式：
```python
import numpy as np
depth_data = np.load('depth_file.npy')
```

### 元数据文件 (.json)
- 格式：JSON文件
- 内容：包含图像采集时的所有相关信息
- 示例内容：
```json
{
  "timestamp": "2024-01-01T12:00:00.123456",
  "camera_ip": "192.168.10.5",
  "image_width": 640,
  "image_height": 480,
  "intensity_filename": "image_20240101_120000_001_intensity.png",
  "depth_filename": "image_20240101_120000_001_depth.npy",
  "depth_data_shape": [480, 640],
  "camera_params": {
    "width": 640,
    "height": 480
  }
}
```

## 配置说明

### 默认配置
- 相机IP地址：`192.168.10.5`
- 控制端口：`2122`
- 数据流端口：`2114`
- 保存目录：`./data`
- 连续流帧率：约30fps（0.033秒间隔）

### 修改配置
在程序中修改以下变量：
```python
CAMERA_IP = "192.168.10.5"  # 相机IP地址
SAVE_PATH = "./data"        # 保存路径
```

## 故障排除

### 1. 连接失败
- 检查相机IP地址是否正确
- 确认网络连接正常
- 检查防火墙设置

### 2. 导入错误
- 确保所有依赖包已安装：`opencv-python`, `numpy`
- 检查SickSDK模块路径是否正确

### 3. 权限错误
- 确保程序有写入保存目录的权限
- 在Linux/Mac上可能需要使用`sudo`

### 4. 连续流问题 🆕
- 确保已调用`start_continuous_mode()`切换到连续模式
- 检查相机是否支持连续流模式
- 如果帧率过低，尝试调整`frame_interval`参数
- 后台线程模式下，确保正确调用`stop_continuous_stream_thread()`

### 5. 性能问题 🆕
- 连续流模式下CPU使用率较高是正常现象
- 可以通过调整帧间隔来控制CPU使用
- 避免在回调函数中进行耗时操作
- 考虑使用后台线程模式来避免阻塞主线程

## 依赖要求

```
opencv-python
numpy
```

安装依赖：
```bash
pip install opencv-python numpy
```

## 注意事项

1. 确保相机已正确配置网络设置
2. 建议在采集大量图像前测试单张图像采集
3. 深度数据文件较大，注意磁盘空间
4. 程序会自动创建不存在的目录
5. 使用Ctrl+C可以安全中断程序
6. **连续流模式下的注意事项** 🆕：
   - 连续流会持续占用网络带宽和系统资源
   - 长时间运行时建议监控系统资源使用情况
   - 在图像处理回调函数中避免阻塞操作
   - 合理设置最大帧数和超时时间来控制采集时长
   - 退出程序前确保正确停止连续流线程 