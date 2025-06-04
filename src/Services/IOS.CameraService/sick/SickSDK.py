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
import threading
from typing import Callable, Optional, Generator, Tuple

class QtVisionSick:
    """
    西克相机控制类
    用于获取相机的强度图数据
    该类获取的流默认为TCP流,如果需要UDP流,请参考sick_visionary_python_samples/visionary_StreamingDemo.py
    """
    
    def __init__(self, ipAddr="192.168.10.5", port=2122, protocol="Cola2"):
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
        self.use_single_step = True  # 默认使用单步模式
        
        # 连续流相关属性
        self._streaming_active = False
        self._streaming_thread = None
        self._stream_callback = None
        self._stream_stop_event = threading.Event()
        
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
    def connect(self, use_single_step=True):
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
        self.streaming_device.getFrame()
        wholeFrame = self.streaming_device.frame
        # 解析数据
        myData = Data.Data()
        myData.read(wholeFrame)
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
    
    @require_connection
    def get_continuous_stream(self, 
                             callback: Optional[Callable] = None, 
                             max_frames: Optional[int] = None,
                             timeout: Optional[float] = None) -> Generator[Tuple[bool, list, np.ndarray, object], None, None]:
        """
        获取连续图像流
        
        Args:
            callback (Callable, optional): 回调函数，接收参数 (success, depth_data, intensity_image, camera_params)
            max_frames (int, optional): 最大帧数，None表示无限制
            timeout (float, optional): 超时时间（秒），None表示无超时
            
        Yields:
            tuple: (success, depth_data, intensity_image, camera_params)
            
        Examples:
            # 使用生成器方式
            for success, depth, intensity, params in camera.get_continuous_stream(max_frames=100):
                if success:
                    print(f"获取到图像，尺寸: {intensity.shape}")
                    
            # 使用回调函数方式
            def process_frame(success, depth_data, intensity_image, camera_params):
                if success:
                    cv2.imshow('Intensity', intensity_image)
                    cv2.waitKey(1)
                    
            for _ in camera.get_continuous_stream(callback=process_frame, max_frames=50):
                pass  # callback函数已处理数据
        """
        if self.use_single_step:
            raise ValueError("连续流模式下才能使用get_continuous_stream，请先调用start_continuous_mode()")
        
        self.logger.info("开始连续流采集")
        frame_count = 0
        start_time = time.time()
        
        try:
            while True:
                # 检查最大帧数限制
                if max_frames is not None and frame_count >= max_frames:
                    self.logger.info(f"达到最大帧数限制: {max_frames}")
                    break
                
                # 检查超时
                if timeout is not None and (time.time() - start_time) > timeout:
                    self.logger.info(f"达到超时限制: {timeout}秒")
                    break
                
                try:
                    # 获取帧数据
                    success, depth_data, intensity_image, camera_params = self._get_frame_data()
                    
                    frame_count += 1
                    
                    # 如果有回调函数，调用它
                    if callback:
                        try:
                            callback(success, depth_data, intensity_image, camera_params)
                        except Exception as e:
                            self.logger.error(f"回调函数执行出错: {str(e)}")
                    
                    # 生成数据
                    yield success, depth_data, intensity_image, camera_params
                    
                except Exception as e:
                    self.logger.error(f"获取帧数据时出错: {str(e)}")
                    yield False, None, None, None
                    
        except KeyboardInterrupt:
            self.logger.info("用户中断连续流采集")
        except Exception as e:
            self.logger.error(f"连续流采集过程中发生错误: {str(e)}")
        finally:
            self.logger.info(f"连续流采集结束，共采集 {frame_count} 帧")
    
    def start_continuous_stream_thread(self, 
                                     callback: Callable,
                                     max_frames: Optional[int] = None,
                                     frame_interval: float = 0.033) -> bool:
        """
        在后台线程中启动连续流采集
        
        Args:
            callback (Callable): 回调函数，接收参数 (success, depth_data, intensity_image, camera_params)
            max_frames (int, optional): 最大帧数，None表示无限制
            frame_interval (float): 帧间隔时间（秒），默认约30fps
            
        Returns:
            bool: 是否成功启动线程
            
        Example:
            def process_frame(success, depth_data, intensity_image, camera_params):
                if success:
                    cv2.imshow('Live Stream', intensity_image)
                    if cv2.waitKey(1) & 0xFF == ord('q'):
                        camera.stop_continuous_stream_thread()
                        
            camera.start_continuous_stream_thread(process_frame)
        """
        if self.use_single_step:
            self.logger.error("需要先切换到连续流模式")
            return False
            
        if self._streaming_active:
            self.logger.warning("连续流线程已在运行")
            return False
        
        self._stream_callback = callback
        self._stream_stop_event.clear()
        self._streaming_active = True
        
        def _stream_worker():
            """后台流处理工作线程"""
            frame_count = 0
            last_frame_time = time.time()
            
            try:
                self.logger.info("后台连续流线程已启动")
                
                while not self._stream_stop_event.is_set():
                    try:
                        # 控制帧率
                        current_time = time.time()
                        elapsed = current_time - last_frame_time
                        if elapsed < frame_interval:
                            time.sleep(frame_interval - elapsed)
                        
                        # 获取帧数据
                        success, depth_data, intensity_image, camera_params = self._get_frame_data()
                        frame_count += 1
                        last_frame_time = time.time()
                        
                        # 调用回调函数
                        if self._stream_callback:
                            try:
                                self._stream_callback(success, depth_data, intensity_image, camera_params)
                            except Exception as e:
                                self.logger.error(f"回调函数执行出错: {str(e)}")
                        
                        # 检查最大帧数限制
                        if max_frames is not None and frame_count >= max_frames:
                            self.logger.info(f"达到最大帧数限制: {max_frames}")
                            break
                            
                    except Exception as e:
                        self.logger.error(f"流处理线程中获取帧数据出错: {str(e)}")
                        time.sleep(0.1)  # 出错时短暂等待
                        
            except Exception as e:
                self.logger.error(f"流处理线程发生严重错误: {str(e)}")
            finally:
                self._streaming_active = False
                self.logger.info(f"后台连续流线程结束，共处理 {frame_count} 帧")
        
        # 启动后台线程
        self._streaming_thread = threading.Thread(target=_stream_worker, daemon=True)
        self._streaming_thread.start()
        
        self.logger.info("后台连续流线程已启动")
        return True
    
    def stop_continuous_stream_thread(self):
        """
        停止后台连续流线程
        
        Returns:
            bool: 是否成功停止
        """
        if not self._streaming_active:
            self.logger.warning("连续流线程未在运行")
            return False
        
        self.logger.info("正在停止后台连续流线程...")
        self._stream_stop_event.set()
        
        # 等待线程结束
        if self._streaming_thread and self._streaming_thread.is_alive():
            self._streaming_thread.join(timeout=5.0)
            if self._streaming_thread.is_alive():
                self.logger.warning("线程未能在超时时间内结束")
                return False
        
        self._streaming_active = False
        self._stream_callback = None
        self.logger.info("后台连续流线程已停止")
        return True
    
    def is_streaming_active(self) -> bool:
        """
        检查连续流是否处于活动状态
        
        Returns:
            bool: 连续流是否活动
        """
        return self._streaming_active
    
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
        # 先停止后台流线程
        if self._streaming_active:
            self.stop_continuous_stream_thread()
            
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

  
  

