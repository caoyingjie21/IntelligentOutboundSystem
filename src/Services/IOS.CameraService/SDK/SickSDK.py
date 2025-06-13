"""
@Description :   this moudle is used to control the sick vision device and get the data.
                 The common module in the folder is required
@Author      :   Cao Yingjie
@Time        :   2025/04/23 08:47:44
"""

from common.Control import Control
from common.Streaming import Data
from common.Stream import Streaming
from common.Streaming.BlobServerConfiguration import BlobClientConfig
from Qcommon.decorators import retry, require_connection, safe_disconnect
import cv2
import numpy as np
import time
from Qcommon.LogManager import LogManager
import socket
from dataclasses import dataclass
from typing import Optional

@dataclass
class CameraFrame:
    success: bool
    depth_data: Optional[list] = None
    intensity_image: Optional[np.ndarray] = None
    confidence_data: Optional[list] = None
    camera_params: Optional[object] = None
    timestamp: Optional[float] = None

class QtVisionSick:
    """
    西克相机控制类
    用于获取相机的强度图数据
    该类获取的流默认为TCP流,如果需要UDP流,请参考sick_visionary_python_samples/visionary_StreamingDemo.py
    """
    
    def __init__(self, ipAddr="192.168.10.5", port=2122, protocol="Cola2", use_single_step=False):
        """
        初始化西克相机
        
        Args:
            ipAddr (str): 相机IP地址
            port (int): 相机控制端口
            protocol (str): 通信协议
        """
        self.ipAddr = ipAddr
        self.control_port = port  # 控制端口
        self.streaming_port = 2114  # 数据流端口
        self.protocol = protocol
        self.deviceControl = None
        self.streaming_device = None
        self.is_connected = False
        self.logger = LogManager().get_logger()
        self.camera_params = None  # 存储相机参数
        self.use_single_step = False  # 默认使用单步模式
        
    def _check_camera_available(self):
        """
        检查相机是否可访问
        
        Returns:
            bool: 相机是否可访问
        """
        try:
            # 创建socket连接测试
            sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            sock.settimeout(2)  # 设置超时时间为2秒
            result = sock.connect_ex((self.ipAddr, self.control_port))
            sock.close()
            return result == 0
        except Exception as e:
            self.logger.error(f"Error checking camera availability: {str(e)}")
            return False
    
    @retry(max_retries=3, delay=1.0, logger_name=__name__)
    def connect(self, use_single_step=False):
        """
        连接相机并初始化流
        
        Args:
            use_single_step (bool): 是否使用单步模式
            
        Returns:
            bool: 连接是否成功
            
        Raises:
            Exception: 连接过程中的任何异常
        """
        if not self._check_camera_available():
            raise ConnectionError(f"Camera at {self.ipAddr}:{self.control_port} is not accessible")
            
        self.use_single_step = use_single_step
        
        # 创建设备控制实例
        self.deviceControl = Control(self.ipAddr, self.protocol, self.control_port)
        
        # 打开连接
        self.deviceControl.open()
        
        # 尝试登录 - 在连接时登录，保持登录状态
        try:
            self.deviceControl.login(Control.USERLEVEL_SERVICE, 'CUST_SERV')
            self.logger.info("以服务级别登录成功")
        except Exception as e:
            self.logger.warning(f"Service level login failed, trying client level: {str(e)}")
            self.deviceControl.login(Control.USERLEVEL_AUTH_CLIENT, 'CLIENT')
            self.logger.info("以客户端级别登录成功")
        
        # 获取设备信息
        name, version = self.deviceControl.getIdent()
        self.logger.info(f"Connected to device: {name.decode('utf-8')}, version: {version.decode('utf-8')}")
        
        # 尝试设置较低的帧速率以减少延迟
        try:
            # 获取当前帧周期 (微秒)
            current_frame_period = self.deviceControl.getFramePeriodUs()
            self.logger.info(f"当前帧周期: {current_frame_period} 微秒")
            
            # 设置较低的帧率 (例如 30 fps = 33333 微秒)
            self.deviceControl.setFramePeriodUs(33333)
            new_frame_period = self.deviceControl.getFramePeriodUs()
            self.logger.info(f"设置新帧周期: {new_frame_period} 微秒")
        except Exception as e:
            self.logger.warning(f"设置帧率失败: {str(e)}")
        
        # 配置流设置
        streamingSettings = BlobClientConfig()
        streamingSettings.setTransportProtocol(self.deviceControl, streamingSettings.PROTOCOL_TCP)
        streamingSettings.setBlobTcpPort(self.deviceControl, self.streaming_port)
        
        # 初始化流
        self.streaming_device = Streaming(self.ipAddr, self.streaming_port)
        self.streaming_device.openStream()
        
        # 根据模式决定流的处理方式
        if self.use_single_step:
            self.logger.info("使用单步模式，先停止流并设置为单步模式")
            # 确保流已停止
            self.deviceControl.stopStream()
        else:
            self.logger.info("使用连续流模式，启动流")
            self.deviceControl.startStream()
        
        self.is_connected = True
        self.logger.info("Successfully connected to camera")
        return True
    
    def _calculate_3d_coordinates_from_depth(self, x, y, depth_data, camera_params):
        """
        从深度数据计算3D坐标
        Args:
            x: 图像x坐标
            y: 图像y坐标
            depth_data: 深度数据
            camera_params: 相机参数
        Returns:
            tuple: (success, (x_cam, y_cam, z))
                success (bool): 是否成功计算坐标
                x_cam, y_cam, z: 相机坐标系下的3D坐标
        """
        try:
            # 检查输入参数
            if depth_data is None or camera_params is None:
                return False, (0, 0, 0)
                
            # 确保深度数据是列表或类似数组的对象
            if not hasattr(depth_data, '__len__'):
                return False, (0, 0, 0)
                
            # 检查相机参数是否有必要的属性
            required_attrs = ['width', 'height', 'cx', 'cy', 'fx', 'fy', 'k1', 'k2', 'f2rc', 'cam2worldMatrix']
            for attr in required_attrs:
                if not hasattr(camera_params, attr):
                    return False, (0, 0, 0)
                
            # 检查坐标是否在有效范围内
            if x < 0 or x >= camera_params.width or y < 0 or y >= camera_params.height:
                return False, (0, 0, 0)
                
            # 计算索引
            index = y * camera_params.width + x
            if index >= len(depth_data):
                return False, (0, 0, 0)
                
            # 获取深度值
            z = depth_data[index]
            
            # 检查深度值是否有效
            if z <= 0:
                return False, (0, 0, 0)
                
            # 计算相机坐标系下的x和y坐标
            xp = (camera_params.cx - x) / camera_params.fx
            yp = (camera_params.cy - y) / camera_params.fy
            
            # 计算径向畸变
            r2 = (xp * xp + yp * yp)
            r4 = r2 * r2
            k = 1 + camera_params.k1 * r2 + camera_params.k2 * r4
            
            xd = xp * k
            yd = yp * k
            
            # 计算相机坐标系下的坐标
            s0 = np.sqrt(xd*xd + yd*yd + 1)
            x_cam = xd * z / s0
            y_cam = yd * z / s0
            z_cam = z / s0 - camera_params.f2rc
            
            # 转换到世界坐标系
            # 检查cam2worldMatrix是否为有效值
            if not hasattr(camera_params.cam2worldMatrix, '__len__') or len(camera_params.cam2worldMatrix) != 16:
                return True, (x_cam, y_cam, z_cam)  # 返回相机坐标系下的坐标
                
            m_c2w = np.array(camera_params.cam2worldMatrix).reshape(4, 4)
            x_world = (m_c2w[0, 3] + z_cam * m_c2w[0, 2] + y_cam * m_c2w[0, 1] + x_cam * m_c2w[0, 0])
            y_world = (m_c2w[1, 3] + z_cam * m_c2w[1, 2] + y_cam * m_c2w[1, 1] + x_cam * m_c2w[1, 0])
            z_world = (m_c2w[2, 3] + z_cam * m_c2w[2, 2] + y_cam * m_c2w[2, 1] + x_cam * m_c2w[2, 0])
            
            return True, (x_world, y_world, z_world)
        except Exception:
            return False, (0, 0, 0)

    @require_connection
    def get_3d_coordinates(self):
        """
        获取整个画面3D坐标中的z坐标
        
        Returns:
            tuple: (success, 3d_coordinates_list)
                success (bool): 是否成功获取3D坐标
                3d_coordinates_list (list): 包含所有像素点3D坐标的列表，每个元素为(x, y, z)元组
        """
        try:
            # 获取帧数据
            myData = self._get_parsed_frame_data()
            if not myData.hasDepthMap:
                raise ValueError("No depth map data available")
            
            # 获取深度数据和相机参数
            depth_data = np.array(myData.depthmap.distance)
            camera_params = myData.cameraParams
            
            # 检查相机参数是否有必要的属性（复用_calculate_3d_coordinates_from_depth的检查逻辑）
            required_attrs = ['width', 'height', 'cx', 'cy', 'fx', 'fy', 'k1', 'k2', 'f2rc', 'cam2worldMatrix']
            for attr in required_attrs:
                if not hasattr(camera_params, attr):
                    return False, []
            
            # 获取图像尺寸
            width = camera_params.width
            height = camera_params.height
            
            # 创建像素坐标网格
            x_coords, y_coords = np.meshgrid(np.arange(width), np.arange(height))
            x_coords = x_coords.flatten()
            y_coords = y_coords.flatten()
            
            # 检查深度值有效性
            valid_mask = depth_data > 0
            
            # 计算相机坐标系下的x和y坐标（矩阵化计算）
            xp = (camera_params.cx - x_coords) / camera_params.fx
            yp = (camera_params.cy - y_coords) / camera_params.fy
            
            # 计算径向畸变（矩阵化计算）
            r2 = xp * xp + yp * yp
            r4 = r2 * r2
            k = 1 + camera_params.k1 * r2 + camera_params.k2 * r4
            
            xd = xp * k
            yd = yp * k
            
            # 计算相机坐标系下的坐标（矩阵化计算）
            s0 = np.sqrt(xd*xd + yd*yd + 1)
            x_cam = xd * depth_data / s0
            y_cam = yd * depth_data / s0
            z_cam = depth_data / s0 - camera_params.f2rc
            
            # 转换到世界坐标系（如果变换矩阵有效）
            if hasattr(camera_params.cam2worldMatrix, '__len__') and len(camera_params.cam2worldMatrix) == 16:
                m_c2w = np.array(camera_params.cam2worldMatrix).reshape(4, 4)
                x_world = (m_c2w[0, 3] + z_cam * m_c2w[0, 2] + y_cam * m_c2w[0, 1] + x_cam * m_c2w[0, 0])
                y_world = (m_c2w[1, 3] + z_cam * m_c2w[1, 2] + y_cam * m_c2w[1, 1] + x_cam * m_c2w[1, 0])
                z_world = (m_c2w[2, 3] + z_cam * m_c2w[2, 2] + y_cam * m_c2w[2, 1] + x_cam * m_c2w[2, 0])
                final_coords = np.column_stack([x_world, y_world, z_world])
            else:
                # 使用相机坐标系坐标
                final_coords = np.column_stack([x_cam, y_cam, z_cam])
            
            # 应用有效性掩码，无效点设为(0, 0, 0)
            final_coords[~valid_mask] = [0.0, 0.0, 0.0]
            
            # 转换为元组列表
            coordinates_3d = [tuple(coord) for coord in final_coords]
            
            return True, coordinates_3d
            
        except Exception as e:
            self.logger.error(f"获取3D坐标失败: {e}")
            return False, []

    @require_connection
    def get_z_coordinates(self):
        """
        获取3D坐标中的z坐标
        
        Returns:
            list: z坐标列表，对应每个像素点的z坐标值
        """
        try:
            # 获取帧数据
            myData = self._get_parsed_frame_data()
            if not myData.hasDepthMap:
                raise ValueError("No depth map data available")
            
            # 获取深度数据和相机参数
            depth_data = np.array(myData.depthmap.distance)
            camera_params = myData.cameraParams
            
            # 检查相机参数是否有必要的属性
            required_attrs = ['width', 'height', 'cx', 'cy', 'fx', 'fy', 'k1', 'k2', 'f2rc']
            for attr in required_attrs:
                if not hasattr(camera_params, attr):
                    return []
            
            # 获取图像尺寸
            width = camera_params.width
            height = camera_params.height
            
            # 创建像素坐标网格
            x_coords, y_coords = np.meshgrid(np.arange(width), np.arange(height))
            x_coords = x_coords.flatten()
            y_coords = y_coords.flatten()
            
            # 检查深度值有效性
            valid_mask = depth_data > 0
            
            # 计算相机坐标系下的x和y坐标（矩阵化计算）
            xp = (camera_params.cx - x_coords) / camera_params.fx
            yp = (camera_params.cy - y_coords) / camera_params.fy
            
            # 计算径向畸变（矩阵化计算）
            r2 = xp * xp + yp * yp
            r4 = r2 * r2
            k = 1 + camera_params.k1 * r2 + camera_params.k2 * r4
            
            xd = xp * k
            yd = yp * k
            
            # 只计算z坐标（优化：不计算x_cam和y_cam）
            s0 = np.sqrt(xd*xd + yd*yd + 1)
            z_cam = depth_data / s0 - camera_params.f2rc
            
            # 应用有效性掩码，无效点设为0
            z_cam[~valid_mask] = 0.0
            
            # 转换为列表返回
            return z_cam.tolist()
            
        except Exception as e:
            self.logger.error(f"获取Z坐标失败: {e}")
            return []

    @require_connection
    def get_frame(self):
        """
        获取当前帧数据
        
        Returns:
            tuple: (success, depth_data, intensity_image, camera_params)
                success (bool): 是否成功获取数据
                depth_data (list): 深度图数据
                intensity_image (numpy.ndarray): 强度图
                camera_params: 相机参数对象
        """
        # 执行单步模式下的一次获取
        if self.use_single_step:
            try:
                # 发送单步命令并获取帧
                self.deviceControl.singleStep()
                time.sleep(0.05)  # 等待相机响应
            except Exception as e:
                self.logger.warning(f"发送单步命令时出错: {str(e)}")
        
        # 获取帧数据
        return self._get_frame_data()
    
    @require_connection
    def get_depth_data(self):
        """
        获取深度数据
        
        Returns:
            list: depth_data
        """
        myData = self._get_parsed_frame_data()
        return list(myData.depthmap.distance)
    
    @require_connection
    def get_intensity_data(self):
        """
        获取强度数据
        
        Returns:
            list: intensity_data
        """
        myData = self._get_parsed_frame_data()
        if not myData.hasDepthMap:
            raise ValueError("No depth map data available")
        intensityData = list(myData.depthmap.intensity)
        return intensityData

    @require_connection
    def get_confidence_data(self):
        """
        获取置信度数据
        
        Returns:
            list: confidence_data
        """
        myData = self._get_parsed_frame_data()
        if not myData.hasDepthMap:
            raise ValueError("No depth map data available")
        confidenceData = list(myData.depthmap.confidence)
        return confidenceData

    @require_connection
    def get_intensity_image(self):
        """
        获取强度图
        
        Returns:
            numpy.ndarray: intensity_image
        """
        myData = self._get_parsed_frame_data()
        if not myData.hasDepthMap:
            raise ValueError("No depth map data available")
        # 获取强度数据
        intensityData = list(myData.depthmap.intensity)
        numCols = myData.cameraParams.width
        numRows = myData.cameraParams.height
        # 重塑数据为图像
        image = np.array(intensityData).reshape((numRows, numCols))
        # 直接调整对比度，不进行归一化
        adjusted_image = cv2.convertScaleAbs(image, alpha=0.05, beta=1)
        return adjusted_image

    @require_connection
    def get_complete_frame(self) -> CameraFrame:
        """获取完整的帧数据"""
        try:
            myData = self._get_parsed_frame_data()
            if not myData.hasDepthMap:
                raise ValueError("No depth map data available")
            # 获取深度数据
            distance_data = list(myData.depthmap.distance)
            # 获取强度数据
            intensityData = list(myData.depthmap.intensity)
            numCols = myData.cameraParams.width
            numRows = myData.cameraParams.height
            # 重塑数据为图像
            image = np.array(intensityData).reshape((numRows, numCols))
            # 直接调整对比度，不进行归一化
            adjusted_image = cv2.convertScaleAbs(image, alpha=0.05, beta=1)
            return CameraFrame(
                success=True,
                depth_data=distance_data,
                intensity_image=adjusted_image,
                camera_params=myData.cameraParams,
                timestamp=time.time()
            )
        except Exception as e:
            self.logger.error(f"获取帧数据失败: {e}")
            return CameraFrame(success=False)

    @require_connection
    @retry(max_retries=2, delay=0.5, logger_name=__name__)        
    def get_frame_no_step(self):
        """
        获取当前帧数据，不发送单步命令
        
        Returns:
            tuple: (success, depth_data, intensity_image, camera_params)
                success (bool): 是否成功获取数据
                depth_data (list): 深度图数据
                intensity_image (numpy.ndarray): 强度图
                camera_params: 相机参数对象
        """
        # 获取帧数据，不发送单步命令
        if not self.use_single_step:
            raise ValueError("连续流模式下不能使用get_frame_no_step")
        self.deviceControl.singleStep()
        return self._get_frame_data()
    
    def _get_frame_data(self):
        """
        内部方法：获取并处理帧数据
        
        Returns:
            tuple: (success, depth_data, intensity_image, camera_params)
        """
        myData = self._get_parsed_frame_data()
        if not myData.hasDepthMap:
            raise ValueError("No depth map data available")
        # 获取深度数据
        distance_data = list(myData.depthmap.distance)
        # 获取强度数据
        intensityData = list(myData.depthmap.intensity)
        numCols = myData.cameraParams.width
        numRows = myData.cameraParams.height
        # 重塑数据为图像
        image = np.array(intensityData).reshape((numRows, numCols))
        # 直接调整对比度，不进行归一化
        adjusted_image = cv2.convertScaleAbs(image, alpha=0.05, beta=1)
        # 保存相机参数
        self.camera_params = myData.cameraParams
        return True, distance_data, adjusted_image, self.camera_params
    
    def get_camera_params(self):
        """
        获取相机参数
        
        Returns:
            camera_params: 相机参数对象，如果未获取过帧数据则返回None
        """
        return getattr(self, 'camera_params', None)
    
    @require_connection    
    def start_continuous_mode(self):
        """
        切换到连续模式并启动流
        
        Returns:
            bool: 是否成功启动连续模式
        """
        try:
            # 确保设备处于客户端级别登录状态
            self.deviceControl.login(Control.USERLEVEL_AUTH_CLIENT, 'CLIENT')
            
            # 启动连续流
            self.deviceControl.startStream()
            self.use_single_step = False
            self.logger.info("已切换到连续流模式")
            return True
        except Exception as e:
            self.logger.error(f"启动连续模式失败: {str(e)}")
            return False
            
    @safe_disconnect  
    def disconnect(self):
        """断开相机连接并释放资源"""
        if self.is_connected:
            if self.deviceControl:
                # 先停止流
                try:
                    # 确保在停止流前先登录
                    try:
                        self.deviceControl.login(Control.USERLEVEL_AUTH_CLIENT, 'CLIENT')
                    except Exception as e:
                        self.logger.warning(f"登录设备时出错: {str(e)}")
                        
                    # 如果处于单步模式，先确保停止单步获取
                    if self.use_single_step:
                        try:
                            # 停止所有正在进行的单步操作
                            self.deviceControl.stopStream()
                            time.sleep(0.2)  # 等待相机处理命令
                            self.logger.info("单步模式已停止")
                        except Exception as e:
                            self.logger.warning(f"停止单步模式时出错: {str(e)}")
                    
                    # 停止数据流
                    self.deviceControl.stopStream()
                    time.sleep(0.2)  # 等待相机处理命令
                    self.logger.info("数据流已停止")
                except Exception as e:
                    self.logger.warning(f"停止流时出错: {str(e)}")
                    
                # 关闭流设备
                if self.streaming_device:
                    try:
                        self.streaming_device.closeStream()
                        self.logger.info("流连接已关闭")
                    except Exception as e:
                        self.logger.warning(f"关闭流连接时出错: {str(e)}")
                    
                # 登出设备
                try:
                    self.deviceControl.logout()
                    self.logger.info("设备已登出")
                except Exception as e:
                    self.logger.warning(f"登出设备时出错: {str(e)}")
                    
                # 关闭控制连接
                try:
                    self.deviceControl.close()
                    self.logger.info("控制连接已关闭")
                except Exception as e:
                    self.logger.warning(f"关闭控制连接时出错: {str(e)}")
                    
            self.is_connected = False
            self.logger.info("相机连接已完全断开")

    def _get_parsed_frame_data(self):
        """获取并解析帧数据的通用方法"""
        self.streaming_device.getFrame()
        wholeFrame = self.streaming_device.frame
        myData = Data.Data()
        myData.read(wholeFrame)
        if not myData.hasDepthMap:
            raise ValueError("No depth map data available")
        return myData
    
    def __enter__(self):
        """上下文管理器入口"""
        self.connect()
        return self
        
    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器退出"""
        self.disconnect()
        
    def __del__(self):
        """确保在销毁时断开连接"""
        self.logger.info("相机连接已销毁,释放资源")
        self.disconnect()

