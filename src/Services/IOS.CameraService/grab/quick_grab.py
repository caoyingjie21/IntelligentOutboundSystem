#!/usr/bin/env python3
"""
快速图像采集脚本
简化版本，用于快速采集西克相机图像
"""

import os
import sys
import cv2
import numpy as np
from datetime import datetime

# 添加父目录到路径
sys.path.append(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from sick.SickSDK import QtVisionSick

def quick_grab(camera_ip="192.168.10.5", count=1, save_dir="./data"):
    """
    快速采集图像
    
    Args:
        camera_ip (str): 相机IP地址
        count (int): 采集数量
        save_dir (str): 保存目录
    """
    # 确保保存目录存在
    intensity_dir = os.path.join(save_dir, "intensity")
    depth_dir = os.path.join(save_dir, "depth")
    
    os.makedirs(intensity_dir, exist_ok=True)
    os.makedirs(depth_dir, exist_ok=True)
    
    print(f"开始连接相机: {camera_ip}")
    
    try:
        # 使用上下文管理器自动管理连接
        with QtVisionSick(ipAddr=camera_ip) as camera:
            camera.connect(use_single_step=True)
            
            print(f"开始采集 {count} 张图像...")
            
            for i in range(count):
                print(f"正在采集第 {i+1}/{count} 张图像...")
                
                # 获取图像数据
                success, depth_data, intensity_image, camera_params = camera.get_frame()
                
                if success:
                    # 生成文件名
                    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S_%f")[:-3]
                    
                    # 保存强度图像
                    intensity_filename = f"intensity_{timestamp}.png"
                    intensity_path = os.path.join(intensity_dir, intensity_filename)
                    cv2.imwrite(intensity_path, intensity_image)
                    
                    # 保存深度数据
                    depth_filename = f"depth_{timestamp}.npy"
                    depth_path = os.path.join(depth_dir, depth_filename)
                    depth_array = np.array(depth_data).reshape((camera_params.height, camera_params.width))
                    np.save(depth_path, depth_array)
                    
                    print(f"  第 {i+1} 张图像保存成功")
                    print(f"    强度图: {intensity_path}")
                    print(f"    深度数据: {depth_path}")
                else:
                    print(f"  第 {i+1} 张图像采集失败")
            
            print("图像采集完成！")
            
    except Exception as e:
        print(f"采集过程中发生错误: {str(e)}")

def main():
    """主函数"""
    import argparse
    
    parser = argparse.ArgumentParser(description="快速图像采集工具")
    parser.add_argument("--ip", default="192.168.10.5", help="相机IP地址")
    parser.add_argument("--count", type=int, default=1, help="采集图像数量")
    parser.add_argument("--dir", default="./data", help="保存目录")
    
    args = parser.parse_args()
    
    print("=== 快速图像采集工具 ===")
    print(f"相机IP: {args.ip}")
    print(f"采集数量: {args.count}")
    print(f"保存目录: {args.dir}")
    print("-" * 30)
    
    quick_grab(camera_ip=args.ip, count=args.count, save_dir=args.dir)

if __name__ == "__main__":
    main() 