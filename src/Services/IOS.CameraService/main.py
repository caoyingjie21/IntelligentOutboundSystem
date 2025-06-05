from rknn.RknnYolo import RKNN_YOLO
from sick.SickSDK import QtVisionSick
from mqtt.ios_mqtt import IOSMqtt
from Qcommon.LogManager import LogManager
from Qcommon.Coordinate_conversion import CoordinateTransformer
import cv2
import time
import sys
import json
import threading
import queue
import numpy as np
sys.path.append('./models')
sys.path.append('./rknn')

# 初始化日志管理器
log_manager = LogManager(log_dir="log", app_name="IOS_CameraService")
logger = log_manager.get_logger("Main")

class IOSCameraService:
    """IOS相机服务主类"""
    
    def __init__(self):
        self.sick_camera = None
        self.rknn_yolo = None
        self.mqtt_client = None
        self.logger = logger
        
        # 使用队列来处理命令，实现异步处理
        self.command_queue = queue.Queue()
        self.shutdown_event = threading.Event()
        self.detection_thread = None
        
        # 启动命令处理线程
        self.start_command_processor()
    
    def start_command_processor(self):
        """启动命令处理线程"""
        self.detection_thread = threading.Thread(target=self._command_processor, daemon=True)
        self.detection_thread.start()
        self.logger.info("命令处理线程已启动")
    
    def _command_processor(self):
        """命令处理线程主循环"""
        while not self.shutdown_event.is_set():
            try:
                # 从队列中获取命令，设置超时避免阻塞
                command = self.command_queue.get(timeout=1.0)
                self.logger.info(f"处理命令: {command}")
                
                if command == "start":
                    self._perform_single_detection()
                elif command == "status":
                    self._handle_status_command()
                elif command == "shutdown":
                    self._handle_shutdown_command()
                else:
                    self.logger.warning(f"未知命令: {command}")
                
                self.command_queue.task_done()
                
            except queue.Empty:
                # 队列为空，继续循环
                continue
            except Exception as e:
                self.logger.error(f"处理命令时发生异常: {str(e)}")
    
    def _calculate_3d_coordinates_and_volume(self, detection_box, depth_data, camera_params):
        """
        计算检测框的3D坐标
        
        Args:
            detection_box: 检测框对象
            depth_data: 深度数据
            camera_params: 相机参数
            
        Returns:
            dict: 包含3D坐标信息的字典
        """
        try:
            # 获取检测框的四个角点
            corners = [
                (int(detection_box.pt1x), int(detection_box.pt1y)),
                (int(detection_box.pt2x), int(detection_box.pt2y)),
                (int(detection_box.pt3x), int(detection_box.pt3y)),
                (int(detection_box.pt4x), int(detection_box.pt4y))
            ]
            
            # 计算中心点
            center_x = int((detection_box.pt1x + detection_box.pt2x + detection_box.pt3x + detection_box.pt4x) / 4)
            center_y = int((detection_box.pt1y + detection_box.pt2y + detection_box.pt3y + detection_box.pt4y) / 4)
            
            # 输出检测框的2D坐标信息
            self.logger.info("=== 检测框坐标信息 ===")
            self.logger.info(f"角点1 (pt1): ({detection_box.pt1x:.1f}, {detection_box.pt1y:.1f})")
            self.logger.info(f"角点2 (pt2): ({detection_box.pt2x:.1f}, {detection_box.pt2y:.1f})")
            self.logger.info(f"角点3 (pt3): ({detection_box.pt3x:.1f}, {detection_box.pt3y:.1f})")
            self.logger.info(f"角点4 (pt4): ({detection_box.pt4x:.1f}, {detection_box.pt4y:.1f})")
            self.logger.info(f"中心点: ({center_x}, {center_y})")
            
            # 计算各点的3D坐标
            corner_3d_coords = []
            self.logger.info("=== 3D坐标计算 ===")
            
            for i, (x, y) in enumerate(corners):
                success, coords_3d = CoordinateTransformer.calculate_3d_coordinates_from_depth(
                    x, y, depth_data, camera_params
                )
                if success:
                    corner_3d_coords.append(coords_3d)
                    self.logger.info(f"角点{i+1} 3D坐标: ({coords_3d[0]:.2f}, {coords_3d[1]:.2f}, {coords_3d[2]:.2f}) mm")
                else:
                    self.logger.warning(f"无法计算角点{i+1}({x}, {y})的3D坐标")
                    corner_3d_coords.append((0, 0, 0))
                    self.logger.warning(f"角点{i+1} 3D坐标: 计算失败")
            
            # 计算中心点的3D坐标
            center_success, center_3d = CoordinateTransformer.calculate_3d_coordinates_from_depth(
                center_x, center_y, depth_data, camera_params
            )
            
            if center_success:
                self.logger.info(f"中心点 3D坐标: ({center_3d[0]:.2f}, {center_3d[1]:.2f}, {center_3d[2]:.2f}) mm")
            else:
                self.logger.warning(f"无法计算中心点({center_x}, {center_y})的3D坐标")
                center_3d = (0, 0, 0)
                self.logger.warning(f"中心点 3D坐标: 计算失败")
            
            # 构建返回结果
            result = {
                "center_2d": {"x": center_x, "y": center_y},
                "center_3d": {"x": center_3d[0], "y": center_3d[1], "z": center_3d[2]},
                "corners_2d": [{"x": x, "y": y} for x, y in corners],
                "corners_3d": [{"x": coord[0], "y": coord[1], "z": coord[2]} for coord in corner_3d_coords],
                "valid_3d": center_success and len([c for c in corner_3d_coords if c != (0, 0, 0)]) >= 4
            }
            
            self.logger.info("==================")
            return result
            
        except Exception as e:
            self.logger.error(f"计算3D坐标时发生异常: {str(e)}")
            return {
                "center_2d": {"x": 0, "y": 0},
                "center_3d": {"x": 0, "y": 0, "z": 0},
                "corners_2d": [],
                "corners_3d": [],
                "valid_3d": False
            }
    
    def _perform_single_detection(self):
        """执行单次检测"""
        try:
            self.logger.info("开始执行单次检测...")
            
            if not self.sick_camera or not self.rknn_yolo:
                self.logger.error("相机或检测模型未初始化")
                return False
            
            # 获取单帧数据
            success, depth_data, intensity_image, camera_params = self.sick_camera.get_frame()
            
            if success:
                self.logger.info("成功获取相机帧数据")
                
                # 运行RKNN YOLO检测
                detection_results = self.rknn_yolo.detect(intensity_image)
                
                # 绘制检测结果
                result_image = self.rknn_yolo.draw_result(intensity_image, detection_results)
                
                # 记录检测结果并计算3D坐标
                if detection_results:
                    self.logger.info(f"检测到 {len(detection_results)} 个目标")
                    
                    # 为每个检测结果计算3D坐标和体积
                    enhanced_results = []
                    
                    for i, box in enumerate(detection_results):
                        # 计算3D坐标和体积
                        coord_info = self._calculate_3d_coordinates_and_volume(box, depth_data, camera_params)
                        
                        # 构建增强的检测数据
                        detection_data = {
                            "id": i,
                            "class_id": box.classId,
                            "class_name": "box" if box.classId == 0 else f"class_{box.classId}",
                            "confidence": round(box.score, 3),
                            "angle": round(box.angle, 3),
                            "bbox_2d": {
                                "pt1": {"x": box.pt1x, "y": box.pt1y},
                                "pt2": {"x": box.pt2x, "y": box.pt2y},
                                "pt3": {"x": box.pt3x, "y": box.pt3y},
                                "pt4": {"x": box.pt4x, "y": box.pt4y}
                            },
                            "coordinates_3d": coord_info["center_3d"],
                            "corners_3d": coord_info["corners_3d"],
                            "is_3d_valid": coord_info["valid_3d"]
                        }
                        
                        enhanced_results.append(detection_data)
                        
                        if coord_info["valid_3d"]:
                            self.logger.info(f"目标{i+1}: 3D坐标计算成功")
                    
                    self.logger.info(f"成功处理 {len(enhanced_results)} 个检测目标")
                    
                    # 发布增强的检测结果
                    self._publish_enhanced_detection_results(enhanced_results)
                else:
                    self.logger.info("未检测到目标")
                    
                    # 发布空检测结果
                    self._publish_enhanced_detection_results([])
                
                # 保存结果图像
                cv2.imwrite("result.jpg", result_image)
                
                self.logger.info("单次检测完成")
                return True
                
            else:
                self.logger.error("获取相机帧数据失败")
                return False
                
        except Exception as e:
            self.logger.error(f"执行单次检测时发生异常: {str(e)}")
            return False
    
    def _publish_enhanced_detection_results(self, enhanced_results):
        """发布增强的检测结果到MQTT"""
        if not self.mqtt_client or not self.mqtt_client.is_alive():
            return
        
        try:
            detection_message = {
                "timestamp": time.time(),
                "detection_count": len(enhanced_results),
                "results": enhanced_results
            }
            
            # 发布检测结果
            self.mqtt_client.publish("ios/camera/detection_results", detection_message)
            self.logger.info("增强检测结果已发布到MQTT")
            
        except Exception as e:
            self.logger.error(f"发布增强检测结果时发生异常: {str(e)}")
    
    def _publish_detection_results(self, detection_results):
        """发布检测结果到MQTT（保留原方法兼容性）"""
        if not self.mqtt_client or not self.mqtt_client.is_alive():
            return
        
        try:
            if detection_results:
                detection_message = {
                    "timestamp": time.time(),
                    "detection_count": len(detection_results),
                    "results": []
                }
                
                for i, box in enumerate(detection_results):
                    detection_data = {
                        "id": i,
                        "class_id": box.classId,
                        "class_name": "box" if box.classId == 0 else f"class_{box.classId}",
                        "confidence": round(box.score, 3),
                        "bbox": {
                            "pt1": {"x": box.pt1x, "y": box.pt1y},
                            "pt2": {"x": box.pt2x, "y": box.pt2y},
                            "pt3": {"x": box.pt3x, "y": box.pt3y},
                            "pt4": {"x": box.pt4x, "y": box.pt4y}
                        },
                        "angle": round(box.angle, 3)
                    }
                    detection_message["results"].append(detection_data)
            else:
                detection_message = {
                    "timestamp": time.time(),
                    "detection_count": 0,
                    "results": []
                }
            
            # 发布检测结果
            self.mqtt_client.publish("ios/camera/detection_results", detection_message)
            self.logger.info("检测结果已发布到MQTT")
            
        except Exception as e:
            self.logger.error(f"发布检测结果时发生异常: {str(e)}")
    
    def _handle_status_command(self):
        """处理状态查询命令"""
        self.logger.info("系统状态: 运行中")
        
        if self.mqtt_client and self.mqtt_client.is_alive():
            status_info = {
                "timestamp": time.time(),
                "status": "running",
                "camera_connected": self.sick_camera.is_connected if self.sick_camera else False,
                "model_loaded": self.rknn_yolo is not None,
                "mqtt_connected": True
            }
            self.mqtt_client.publish("ios/camera/status", status_info)
    
    def _handle_shutdown_command(self):
        """处理关闭命令"""
        self.logger.warning("收到关闭命令，开始关闭服务")
        self.shutdown_event.set()
    
    def mqtt_callback(self, topic, message):
        """MQTT消息回调函数"""
        # 记录收到的MQTT消息到日志
        self.logger.info(f"收到MQTT消息 - 主题: {topic}")
        self.logger.info(f"消息内容: {message}")
        
        try:
            # 尝试解析JSON格式的消息
            if message.strip().startswith('{') and message.strip().endswith('}'):
                msg_data = json.loads(message)
                self.logger.info(f"解析后的消息数据: {json.dumps(msg_data, indent=2, ensure_ascii=False)}")
                
                # 根据消息类型处理
                if 'command' in msg_data:
                    command = msg_data.get('command', '')
                    self.logger.info(f"收到命令: {command}")
                    
                    # 将命令放入队列中异步处理
                    self.command_queue.put(command)
            else:
                # 非JSON格式的消息 - 直接处理简单命令
                command = message.strip().lower()
                self.logger.info(f"收到文本命令: {command}")
                
                # 将命令放入队列中异步处理
                self.command_queue.put(command)
                
        except json.JSONDecodeError as e:
            self.logger.warning(f"消息JSON解析失败: {str(e)}")
            self.logger.info(f"原始消息: {message}")
        except Exception as e:
            self.logger.error(f"处理MQTT消息时发生异常: {str(e)}")
        
        # 同时输出到控制台（保持原有行为）
        print(f"收到命令: {message}")
    
    def initialize_components(self):
        """初始化所有组件"""
        try:
    # 初始化RKNN YOLO模型
            self.logger.info("正在加载RKNN YOLO模型...")
            self.rknn_yolo = RKNN_YOLO(model_path='./models/best.pt', target='rk3588', conf_threshold=0.7, device_id=0)
            self.logger.info("RKNN YOLO模型加载成功")
            
            # 模型预热
            self.logger.info("开始模型预热...")
            self._warmup_model()
            
            # 初始化SICK相机
            self.logger.info("正在初始化SICK相机...")
            self.sick_camera = QtVisionSick(ipAddr='192.168.10.5')
            if self.sick_camera.connect(use_single_step=True):  # 使用单步模式
                self.logger.info("SICK相机连接成功")
            else:
                self.logger.error("SICK相机连接失败")
                raise Exception("SICK相机连接失败")
            
            # 初始化MQTT客户端
            self.logger.info("正在初始化MQTT客户端...")
            self.mqtt_client = IOSMqtt(
                broker_host="localhost",
                broker_port=1883,
                client_id="IOS_CameraService_Main"
            )
            
            # 连接MQTT代理
            self.logger.info("正在连接MQTT代理...")
            if self.mqtt_client.connect():
                self.logger.info("MQTT连接成功")
                
                # 订阅命令主题
                if self.mqtt_client.subscribe('ios/camera/command', self.mqtt_callback, qos=2):
                    self.logger.info("成功订阅主题: ios/camera/command")
                else:
                    self.logger.error("订阅主题失败")
                
                # 发布服务启动状态
                startup_status = {
                    "status": "online",
                    "timestamp": time.time(),
                    "service": "IOS_CameraService",
                    "version": "1.0.0",
                    "camera_connected": True,
                    "model_loaded": True
                }
                self.mqtt_client.publish("ios/camera/status", startup_status)
                self.logger.info("已发布服务启动状态")
                
            else:
                self.logger.error("MQTT连接失败，程序将继续运行但无MQTT功能")
            
            return True
            
        except Exception as e:
            self.logger.error(f"初始化组件时发生异常: {str(e)}")
            return False
    
    def _warmup_model(self):
        """使用warmup.jpg预热模型"""
        try:
            import os
            warmup_image_path = "./img/warmup.jpg"
            
            # 检查预热图片是否存在
            if not os.path.exists(warmup_image_path):
                self.logger.warning(f"预热图片 {warmup_image_path} 不存在，跳过模型预热")
                return False
            
            self.logger.info(f"使用 {warmup_image_path} 进行模型预热...")
            
            # 加载预热图片
            warmup_image = cv2.imread(warmup_image_path, cv2.IMREAD_GRAYSCALE)
            if warmup_image is None:
                self.logger.error(f"无法加载预热图片: {warmup_image_path}")
                return False
            
            self.logger.info(f"成功加载预热图片，尺寸: {warmup_image.shape}")
            
            # 执行预热推理
            start_time = time.time()
            warmup_results = self.rknn_yolo.detect(warmup_image)
            warmup_time = time.time() - start_time
            
            self.logger.info(f"模型预热完成，耗时: {warmup_time:.3f}秒")
            self.logger.info(f"预热检测结果数量: {len(warmup_results) if warmup_results else 0}")
            
            # 可选：保存预热结果图像
            if warmup_results:
                warmup_result_image = self.rknn_yolo.draw_result(warmup_image, warmup_results)
                cv2.imwrite("warmup_result.jpg", warmup_result_image)
                self.logger.info("预热结果已保存为 warmup_result.jpg")
            
            return True
            
        except Exception as e:
            self.logger.error(f"模型预热时发生异常: {str(e)}")
            return False
    
    def run(self):
        """运行主服务循环"""
        self.logger.info("进入主循环，程序开始运行...")
        self.logger.info("程序控制说明:")
        self.logger.info("- 发送MQTT消息到 'ios/camera/command' 主题进行控制")
        self.logger.info("- 支持的命令: start(开始检测), status(查询状态), shutdown(关闭程序)")
        
        loop_count = 0
        try:
            while not self.shutdown_event.is_set():
                loop_count += 1
                
                # 每100次循环记录一次心跳
                if loop_count % 100 == 0:
                    self.logger.debug(f"程序运行正常，循环计数: {loop_count}")
                    
                    # 发送心跳消息
                    if self.mqtt_client and self.mqtt_client.is_alive():
                        heartbeat_data = {
                            "timestamp": time.time(),
                            "loop_count": loop_count,
                            "status": "running",
                            "camera_connected": self.sick_camera.is_connected if self.sick_camera else False
                        }
                        self.mqtt_client.publish("ios/camera/heartbeat", heartbeat_data)
                
                time.sleep(1)
                
        except Exception as e:
            self.logger.error(f"主循环运行时发生异常: {str(e)}")
    
    def cleanup(self):
        """清理资源"""
        self.logger.info("开始清理资源...")
        
        # 设置关闭信号
        self.shutdown_event.set()
        
        # 等待命令处理线程结束
        if self.detection_thread and self.detection_thread.is_alive():
            self.detection_thread.join(timeout=5.0)
            self.logger.info("命令处理线程已结束")
        
        # 发布服务关闭状态
        try:
            if self.mqtt_client and self.mqtt_client.is_alive():
                shutdown_status = {
                    "status": "offline",
                    "timestamp": time.time(),
                    "reason": "normal_shutdown"
                }
                self.mqtt_client.publish("ios/camera/status", shutdown_status)
                self.mqtt_client.disconnect()
                self.logger.info("MQTT连接已断开")
        except Exception as e:
            self.logger.error(f"断开MQTT连接时发生异常: {str(e)}")
        
        # 断开SICK相机连接
        try:
            if self.sick_camera:
                self.sick_camera.disconnect()
                self.logger.info("SICK相机连接已断开")
        except Exception as e:
            self.logger.error(f"断开SICK相机连接时发生异常: {str(e)}")
        
        # 释放RKNN资源
        try:
            if self.rknn_yolo:
                self.rknn_yolo.release()
                self.logger.info("RKNN模型资源已释放")
        except Exception as e:
            self.logger.error(f"释放RKNN资源时发生异常: {str(e)}")
        
        # 关闭OpenCV窗口
        try:
            cv2.destroyAllWindows()
            self.logger.info("OpenCV窗口已关闭")
        except Exception as e:
            self.logger.error(f"关闭OpenCV窗口时发生异常: {str(e)}")
        
        self.logger.info("程序已完全退出")

def frame_callback(success, depth_data, intensity_image, camera_params, rknn_yolo):
    """帧处理回调函数（保留用于其他用途）"""
    if success:
        # 运行RKNN YOLO模型
        detection_results = rknn_yolo.detect(intensity_image)
        # 绘制检测结果
        result_image = rknn_yolo.draw_result(intensity_image, detection_results)
        
        # 记录检测结果到日志
        if detection_results:
            logger.info(f"检测到 {len(detection_results)} 个目标")
        
        # 显示结果
        cv2.imshow('Intensity Image', result_image)
    else:
        logger.error("获取帧失败")

if __name__ == '__main__':
    service = None
    try:
        logger.info("开始初始化IOS相机服务...")
        
        # 创建服务实例
        service = IOSCameraService()
        
        # 初始化所有组件
        if not service.initialize_components():
            logger.error("组件初始化失败，程序退出")
            sys.exit(1)
        
        # 运行主服务
        service.run()
        
    except KeyboardInterrupt:
        logger.info("接收到用户中断信号 (Ctrl+C)")
        print("程序被用户中断，正在退出...")
    except Exception as e:
        logger.error(f"程序运行时发生异常: {str(e)}")
        print(f"程序运行出错: {str(e)}")
    finally:
        if service:
            service.cleanup()
        print("资源已释放，程序退出")
