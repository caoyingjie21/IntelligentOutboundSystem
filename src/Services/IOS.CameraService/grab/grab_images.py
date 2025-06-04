import os
import cv2
import numpy as np
import time
from datetime import datetime
import sys
import json

# 添加父目录到路径以便导入SickSDK
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from sick.SickSDK import QtVisionSick

class ImageGrabber:
    """
    图像采集器类
    用于连接西克相机并采集保存图像数据
    """
    
    def __init__(self, camera_ip="192.168.10.5", save_path="./data"):
        """
        初始化图像采集器
        
        Args:
            camera_ip (str): 相机IP地址
            save_path (str): 图像保存路径
        """
        self.camera_ip = camera_ip
        self.save_path = save_path
        self.camera = None
        
        # 确保保存目录存在
        self._ensure_save_directory()
        
    def _ensure_save_directory(self):
        """确保保存目录存在"""
        if not os.path.exists(self.save_path):
            os.makedirs(self.save_path)
            print(f"创建保存目录: {self.save_path}")
        
        # 创建子目录
        subdirs = ['intensity', 'depth', 'metadata']
        for subdir in subdirs:
            full_path = os.path.join(self.save_path, subdir)
            if not os.path.exists(full_path):
                os.makedirs(full_path)
                print(f"创建子目录: {full_path}")
    
    def connect_camera(self):
        """
        连接相机
        
        Returns:
            bool: 连接是否成功
        """
        try:
            self.camera = QtVisionSick(ipAddr=self.camera_ip)
            success = self.camera.connect(use_single_step=True)
            if success:
                print(f"成功连接到相机: {self.camera_ip}")
                return True
            else:
                print(f"连接相机失败: {self.camera_ip}")
                return False
        except Exception as e:
            print(f"连接相机时发生错误: {str(e)}")
            return False
    
    def grab_single_image(self, filename_prefix=None):
        """
        采集单张图像
        
        Args:
            filename_prefix (str): 文件名前缀，如果为None则使用时间戳
            
        Returns:
            tuple: (success, intensity_filename, depth_filename, metadata_filename)
        """
        if not self.camera or not self.camera.is_connected:
            print("相机未连接，无法采集图像")
            return False, None, None, None
        
        try:
            # 获取图像数据
            success, depth_data, intensity_image, camera_params = self.camera.get_frame()
            
            if not success:
                print("获取图像数据失败")
                return False, None, None, None
            
            # 生成文件名
            if filename_prefix is None:
                timestamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")[:-3]  # 毫秒精度
                filename_prefix = f"image_{timestamp}"
            
            # 保存强度图像
            intensity_filename = f"{filename_prefix}_intensity.png"
            intensity_path = os.path.join(self.save_path, "intensity", intensity_filename)
            cv2.imwrite(intensity_path, intensity_image)
            
            # 保存深度数据为numpy数组
            depth_filename = f"{filename_prefix}_depth.npy"
            depth_path = os.path.join(self.save_path, "depth", depth_filename)
            depth_array = np.array(depth_data).reshape((camera_params.height, camera_params.width))
            np.save(depth_path, depth_array)
            
            # 保存相机参数和元数据
            metadata_filename = f"{filename_prefix}_metadata.json"
            metadata_path = os.path.join(self.save_path, "metadata", metadata_filename)
            metadata = {
                "timestamp": datetime.now().isoformat(),
                "camera_ip": self.camera_ip,
                "image_width": camera_params.width,
                "image_height": camera_params.height,
                "intensity_filename": intensity_filename,
                "depth_filename": depth_filename,
                "depth_data_shape": [camera_params.height, camera_params.width],
                "camera_params": {
                    "width": camera_params.width,
                    "height": camera_params.height,
                    # 可以添加更多相机参数信息
                }
            }
            
            with open(metadata_path, 'w', encoding='utf-8') as f:
                json.dump(metadata, f, indent=2, ensure_ascii=False)
            
            print(f"成功保存图像:")
            print(f"  强度图: {intensity_path}")
            print(f"  深度数据: {depth_path}")
            print(f"  元数据: {metadata_path}")
            
            return True, intensity_filename, depth_filename, metadata_filename
            
        except Exception as e:
            print(f"采集图像时发生错误: {str(e)}")
            return False, None, None, None
    
    def grab_multiple_images(self, count=10, interval=1.0):
        """
        连续采集多张图像
        
        Args:
            count (int): 采集图像数量
            interval (float): 采集间隔（秒）
        
        Returns:
            list: 成功采集的图像信息列表
        """
        if not self.camera or not self.camera.is_connected:
            print("相机未连接，无法采集图像")
            return []
        
        successful_captures = []
        print(f"开始连续采集 {count} 张图像，间隔 {interval} 秒...")
        
        for i in range(count):
            print(f"\n正在采集第 {i+1}/{count} 张图像...")
            
            # 使用序号作为前缀
            filename_prefix = f"batch_{datetime.now().strftime('%Y%m%d_%H%M%S')}_{i+1:03d}"
            
            success, intensity_file, depth_file, metadata_file = self.grab_single_image(filename_prefix)
            
            if success:
                successful_captures.append({
                    "index": i + 1,
                    "intensity_file": intensity_file,
                    "depth_file": depth_file,
                    "metadata_file": metadata_file
                })
                print(f"第 {i+1} 张图像采集成功")
            else:
                print(f"第 {i+1} 张图像采集失败")
            
            # 等待间隔时间（最后一张不用等待）
            if i < count - 1:
                time.sleep(interval)
        
        print(f"\n采集完成！成功采集 {len(successful_captures)}/{count} 张图像")
        return successful_captures
    
    def disconnect_camera(self):
        """断开相机连接"""
        if self.camera:
            self.camera.disconnect()
            print("相机连接已断开")
    
    def __enter__(self):
        """上下文管理器入口"""
        if self.connect_camera():
            return self
        else:
            raise ConnectionError("无法连接到相机")
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器退出"""
        self.disconnect_camera()

def main():
    """主函数"""
    print("=== 西克相机图像采集程序 ===")
    
    # 配置参数
    CAMERA_IP = "192.168.10.5"  # 相机IP地址
    SAVE_PATH = "./data"        # 保存路径
    
    try:
        # 使用上下文管理器自动管理连接
        with ImageGrabber(camera_ip=CAMERA_IP, save_path=SAVE_PATH) as grabber:
            while True:
                print("\n请选择操作:")
                print("1. 采集单张图像")
                print("2. 连续采集多张图像")
                print("3. 退出程序")
                
                choice = input("请输入选择 (1-3): ").strip()
                
                if choice == "1":
                    # 采集单张图像
                    grabber.grab_single_image()
                    
                elif choice == "2":
                    # 连续采集多张图像
                    try:
                        count = int(input("请输入采集数量 (默认10张): ") or "10")
                        interval = float(input("请输入采集间隔秒数 (默认1.0秒): ") or "1.0")
                        grabber.grab_multiple_images(count=count, interval=interval)
                    except ValueError:
                        print("输入格式错误，使用默认参数")
                        grabber.grab_multiple_images()
                        
                elif choice == "3":
                    print("退出程序")
                    break
                    
                else:
                    print("无效选择，请重新输入")
                    
    except ConnectionError as e:
        print(f"连接错误: {str(e)}")
    except KeyboardInterrupt:
        print("\n程序被用户中断")
    except Exception as e:
        print(f"程序运行时发生错误: {str(e)}")

if __name__ == "__main__":
    main()
