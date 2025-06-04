from rknn.RknnYolo import RKNN_YOLO
from sick.SickSDK import QtVisionSick
import cv2
import sys
sys.path.append('./models')
sys.path.append('./rknn')

def frame_callback(success, depth_data, intensity_image, camera_params, rknn_yolo):
    """帧处理回调函数"""
    if success:
        # 运行RKNN YOLO模型
        detection_results = rknn_yolo.detect(intensity_image)
        # 绘制检测结果
        result_image = rknn_yolo.draw_result(intensity_image, detection_results)
        
        # 显示结果
        cv2.imshow('Intensity Image', result_image)
    else:
        print("获取帧失败")

if __name__ == '__main__':
    # 初始化RKNN YOLO模型
    rknn_yolo = RKNN_YOLO(model_path='./models/best.pt', target='rk3588', conf_threshold=0.7, device_id=0)
    
    try:
        # 初始化SICK SDK
        with QtVisionSick(ipAddr='192.168.10.5') as sick:
            # 连接SICK相机
            if not sick.connect(use_single_step=False):
                print("SICK相机连接失败")
                exit()
            
            # 切换到连续模式
            sick.start_continuous_mode()
            print("开始连续流处理，按 'q' 键退出...")
            
            # 使用回调函数处理连续流
            for _ in sick.get_continuous_stream(
                callback=lambda success, depth_data, intensity_image, camera_params: 
                frame_callback(success, depth_data, intensity_image, camera_params, rknn_yolo),
                max_frames=None  # 无限制帧数
            ):
                # 检查退出条件
                if cv2.waitKey(1) & 0xFF == ord('q'):
                    print("用户退出")
                    break
                    
    except KeyboardInterrupt:
        print("\n程序被用户中断")
    except Exception as e:
        print(f"程序运行出错: {str(e)}")
    finally:
        # 释放资源
        rknn_yolo.release()
        cv2.destroyAllWindows()
        print("资源已释放")
