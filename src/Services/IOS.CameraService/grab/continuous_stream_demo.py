#!/usr/bin/env python3
"""
连续流演示程序
展示如何使用QtVisionSick类的get_continuous_stream方法
"""

import os
import sys
import cv2
import numpy as np
from datetime import datetime
import time
import argparse

# 添加父目录到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from sick.SickSDK import QtVisionSick

class ContinuousStreamDemo:
    """连续流演示类"""
    
    def __init__(self, camera_ip="192.168.10.5", save_dir="./data"):
        self.camera_ip = camera_ip
        self.save_dir = save_dir
        self.camera = None
        self.frame_count = 0
        self.save_enabled = False
        
        # 确保保存目录存在
        self._ensure_directories()
        
    def _ensure_directories(self):
        """确保保存目录存在"""
        intensity_dir = os.path.join(self.save_dir, "stream_intensity")
        depth_dir = os.path.join(self.save_dir, "stream_depth")
        
        os.makedirs(intensity_dir, exist_ok=True)
        os.makedirs(depth_dir, exist_ok=True)
        
    def demo_generator_mode(self):
        """演示生成器模式的连续流"""
        print("=== 生成器模式连续流演示 ===")
        print("按 'q' 键退出，按 's' 键保存当前帧")
        
        try:
            with QtVisionSick(ipAddr=self.camera_ip) as camera:
                # 切换到连续模式
                camera.connect(use_single_step=False)
                camera.start_continuous_mode()
                
                # 使用生成器获取连续流
                for success, depth_data, intensity_image, camera_params in camera.get_continuous_stream(max_frames=1000):
                    if success:
                        self.frame_count += 1
                        
                        # 显示图像
                        display_image = cv2.resize(intensity_image, (640, 480))
                        cv2.putText(display_image, f"Frame: {self.frame_count}", (10, 30), 
                                  cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
                        cv2.imshow('Continuous Stream - Generator Mode', display_image)
                        
                        # 检查按键
                        key = cv2.waitKey(1) & 0xFF
                        if key == ord('q'):
                            print("用户退出")
                            break
                        elif key == ord('s'):
                            self._save_frame(depth_data, intensity_image, camera_params)
                            
                    else:
                        print("获取帧失败")
                        
        except Exception as e:
            print(f"生成器模式演示出错: {str(e)}")
        finally:
            cv2.destroyAllWindows()
            print(f"生成器模式演示结束，共处理 {self.frame_count} 帧")
    
    def demo_callback_mode(self):
        """演示回调函数模式的连续流"""
        print("=== 回调函数模式连续流演示 ===")
        print("按 'q' 键退出，按 's' 键切换保存模式")
        
        def frame_callback(success, depth_data, intensity_image, camera_params):
            """帧处理回调函数"""
            if success:
                self.frame_count += 1
                
                # 显示图像
                display_image = cv2.resize(intensity_image, (640, 480))
                cv2.putText(display_image, f"Frame: {self.frame_count}", (10, 30), 
                          cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
                
                if self.save_enabled:
                    cv2.putText(display_image, "SAVING", (10, 60), 
                              cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                    self._save_frame(depth_data, intensity_image, camera_params)
                
                cv2.imshow('Continuous Stream - Callback Mode', display_image)
        
        try:
            with QtVisionSick(ipAddr=self.camera_ip) as camera:
                # 切换到连续模式
                camera.connect(use_single_step=False)
                camera.start_continuous_mode()
                
                # 使用回调函数获取连续流
                for _ in camera.get_continuous_stream(callback=frame_callback, max_frames=1000):
                    # 检查按键
                    key = cv2.waitKey(1) & 0xFF
                    if key == ord('q'):
                        print("用户退出")
                        break
                    elif key == ord('s'):
                        self.save_enabled = not self.save_enabled
                        print(f"保存模式: {'开启' if self.save_enabled else '关闭'}")
                        
        except Exception as e:
            print(f"回调函数模式演示出错: {str(e)}")
        finally:
            cv2.destroyAllWindows()
            print(f"回调函数模式演示结束，共处理 {self.frame_count} 帧")
    
    def demo_background_thread_mode(self):
        """演示后台线程模式的连续流"""
        print("=== 后台线程模式连续流演示 ===")
        print("按 'q' 键退出，按 's' 键切换保存模式")
        
        def thread_callback(success, depth_data, intensity_image, camera_params):
            """后台线程回调函数"""
            if success:
                self.frame_count += 1
                
                # 显示图像
                display_image = cv2.resize(intensity_image, (640, 480))
                cv2.putText(display_image, f"Frame: {self.frame_count}", (10, 30), 
                          cv2.FONT_HERSHEY_SIMPLEX, 1, (255, 255, 255), 2)
                cv2.putText(display_image, "BACKGROUND THREAD", (10, 450), 
                          cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 0), 2)
                
                if self.save_enabled:
                    cv2.putText(display_image, "SAVING", (10, 60), 
                              cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                    self._save_frame(depth_data, intensity_image, camera_params)
                
                cv2.imshow('Continuous Stream - Background Thread', display_image)
        
        try:
            with QtVisionSick(ipAddr=self.camera_ip) as camera:
                # 切换到连续模式
                camera.connect(use_single_step=False)
                camera.start_continuous_mode()
                
                # 启动后台线程
                if camera.start_continuous_stream_thread(callback=thread_callback, frame_interval=0.033):
                    print("后台线程已启动，正在采集图像...")
                    
                    # 主线程处理用户输入
                    while camera.is_streaming_active():
                        key = cv2.waitKey(100) & 0xFF
                        if key == ord('q'):
                            print("用户退出")
                            break
                        elif key == ord('s'):
                            self.save_enabled = not self.save_enabled
                            print(f"保存模式: {'开启' if self.save_enabled else '关闭'}")
                    
                    # 停止后台线程
                    camera.stop_continuous_stream_thread()
                else:
                    print("启动后台线程失败")
                    
        except Exception as e:
            print(f"后台线程模式演示出错: {str(e)}")
        finally:
            cv2.destroyAllWindows()
            print(f"后台线程模式演示结束，共处理 {self.frame_count} 帧")
    
    def demo_performance_test(self):
        """性能测试演示"""
        print("=== 性能测试演示 ===")
        print("测试连续流的帧率性能...")
        
        frame_times = []
        
        def performance_callback(success, depth_data, intensity_image, camera_params):
            """性能测试回调函数"""
            current_time = time.time()
            frame_times.append(current_time)
            
            if success:
                self.frame_count += 1
                
                # 每100帧显示一次统计
                if self.frame_count % 100 == 0:
                    if len(frame_times) >= 2:
                        recent_times = frame_times[-100:]
                        if len(recent_times) >= 2:
                            duration = recent_times[-1] - recent_times[0]
                            fps = (len(recent_times) - 1) / duration if duration > 0 else 0
                            print(f"帧数: {self.frame_count}, 平均FPS: {fps:.2f}")
        
        try:
            with QtVisionSick(ipAddr=self.camera_ip) as camera:
                camera.connect(use_single_step=False)
                camera.start_continuous_mode()
                
                print("开始性能测试，将采集1000帧...")
                start_time = time.time()
                
                for _ in camera.get_continuous_stream(callback=performance_callback, max_frames=1000):
                    pass
                
                end_time = time.time()
                total_duration = end_time - start_time
                average_fps = self.frame_count / total_duration if total_duration > 0 else 0
                
                print(f"\n性能测试结果:")
                print(f"总帧数: {self.frame_count}")
                print(f"总时间: {total_duration:.2f} 秒")
                print(f"平均帧率: {average_fps:.2f} FPS")
                
        except Exception as e:
            print(f"性能测试出错: {str(e)}")
    
    def _save_frame(self, depth_data, intensity_image, camera_params):
        """保存单帧数据"""
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")[:-3]
        
        # 保存强度图像
        intensity_filename = f"stream_intensity_{timestamp}.png"
        intensity_path = os.path.join(self.save_dir, "stream_intensity", intensity_filename)
        cv2.imwrite(intensity_path, intensity_image)
        
        # 保存深度数据
        depth_filename = f"stream_depth_{timestamp}.npy"
        depth_path = os.path.join(self.save_dir, "stream_depth", depth_filename)
        depth_array = np.array(depth_data).reshape((camera_params.height, camera_params.width))
        np.save(depth_path, depth_array)
        
        print(f"保存帧 {self.frame_count}: {intensity_filename}")

def main():
    """主函数"""
    parser = argparse.ArgumentParser(description="连续流演示程序")
    parser.add_argument("--ip", default="192.168.10.5", help="相机IP地址")
    parser.add_argument("--dir", default="./data", help="保存目录")
    parser.add_argument("--mode", choices=["generator", "callback", "thread", "performance"], 
                       default="generator", help="演示模式")
    
    args = parser.parse_args()
    
    demo = ContinuousStreamDemo(camera_ip=args.ip, save_dir=args.dir)
    
    print("=== 西克相机连续流演示程序 ===")
    print(f"相机IP: {args.ip}")
    print(f"保存目录: {args.dir}")
    print(f"演示模式: {args.mode}")
    print("-" * 50)
    
    try:
        if args.mode == "generator":
            demo.demo_generator_mode()
        elif args.mode == "callback":
            demo.demo_callback_mode()
        elif args.mode == "thread":
            demo.demo_background_thread_mode()
        elif args.mode == "performance":
            demo.demo_performance_test()
    except KeyboardInterrupt:
        print("\n程序被用户中断")
    except Exception as e:
        print(f"程序运行出错: {str(e)}")

if __name__ == "__main__":
    main() 