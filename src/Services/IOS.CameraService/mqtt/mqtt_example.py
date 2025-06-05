#!/usr/bin/env python3
"""
MQTT使用示例
演示如何使用IOSMqtt类进行MQTT通信
"""

import time
import json
from ios_mqtt import IOSMqtt
from Qcommon.LogManager import LogManager

# 设置日志 - 使用LogManager
log_manager = LogManager(log_dir="log", app_name="MqttExample")
logger = log_manager.get_logger("MqttExample")

def command_callback(topic: str, message: str):
    """
    处理命令消息的回调函数
    
    Args:
        topic (str): 消息主题
        message (str): 消息内容
    """
    print(f"收到命令消息 - 主题: {topic}")
    try:
        cmd_data = json.loads(message)
        command = cmd_data.get('command', '')
        parameters = cmd_data.get('parameters', {})
        
        print(f"命令: {command}")
        print(f"参数: {parameters}")
        
        # 根据命令执行相应操作
        if command == "start_detection":
            print("开始检测...")
        elif command == "stop_detection":
            print("停止检测...")
        elif command == "set_threshold":
            threshold = parameters.get('confidence_threshold', 0.5)
            print(f"设置置信度阈值为: {threshold}")
        else:
            print(f"未知命令: {command}")
            
    except json.JSONDecodeError:
        print(f"消息格式错误: {message}")

def config_callback(topic: str, message: str):
    """
    处理配置消息的回调函数
    
    Args:
        topic (str): 消息主题  
        message (str): 消息内容
    """
    print(f"收到配置消息 - 主题: {topic}")
    try:
        config_data = json.loads(message)
        print(f"新配置: {json.dumps(config_data, indent=2, ensure_ascii=False)}")
        # 在这里处理配置更新逻辑
        
    except json.JSONDecodeError:
        print(f"配置消息格式错误: {message}")

def main():
    """主函数示例"""
    # 创建MQTT客户端实例
    mqtt_client = IOSMqtt(
        broker_host="localhost",  # MQTT代理服务器地址
        broker_port=1883,         # MQTT代理服务器端口
        client_id="IOS_Camera_Example",
        username=None,            # 如需认证，设置用户名
        password=None             # 如需认证，设置密码
    )
    
    try:
        # 连接到MQTT代理
        if mqtt_client.connect():
            logger.info("MQTT连接成功!")
            
            # 订阅命令主题
            mqtt_client.subscribe("ios/camera/commands", command_callback)
            
            # 订阅配置主题
            mqtt_client.subscribe("ios/camera/config", config_callback)
            
            # 发布相机状态
            mqtt_client.publish_camera_status("online", {
                "ip": "192.168.10.5",
                "model": "SICK_Camera",
                "firmware_version": "1.0.0"
            })
            
            # 模拟检测结果发布
            logger.info("开始模拟检测结果发布...")
            
            for i in range(5):
                # 模拟检测结果数据
                mock_detection_results = []
                
                # 创建模拟的DetectBox对象
                class MockDetectBox:
                    def __init__(self, class_id, score, pt1x, pt1y, pt2x, pt2y, pt3x, pt3y, pt4x, pt4y, angle):
                        self.classId = class_id
                        self.score = score
                        self.pt1x = pt1x
                        self.pt1y = pt1y
                        self.pt2x = pt2x
                        self.pt2y = pt2y
                        self.pt3x = pt3x
                        self.pt3y = pt3y
                        self.pt4x = pt4x
                        self.pt4y = pt4y
                        self.angle = angle
                
                # 添加一些模拟检测结果
                if i % 2 == 0:  # 模拟有时有检测结果，有时没有
                    mock_box = MockDetectBox(
                        class_id=0,
                        score=0.85 + i * 0.02,
                        pt1x=100 + i * 10, pt1y=50 + i * 5,
                        pt2x=200 + i * 10, pt2y=50 + i * 5,
                        pt3x=200 + i * 10, pt3y=150 + i * 5,
                        pt4x=100 + i * 10, pt4y=150 + i * 5,
                        angle=0.1 * i
                    )
                    mock_detection_results.append(mock_box)
                
                # 发布检测结果
                success = mqtt_client.publish_detection_result(
                    mock_detection_results,
                    camera_info={"camera_id": "sick_camera_01", "ip": "192.168.10.5"}
                )
                
                if success:
                    logger.info(f"发布检测结果 {i+1}/5 成功，检测到 {len(mock_detection_results)} 个目标")
                else:
                    logger.error(f"发布检测结果 {i+1}/5 失败")
                
                time.sleep(2)  # 等待2秒
            
            # 发布一些自定义消息
            logger.info("发布自定义消息...")
            mqtt_client.publish("ios/camera/heartbeat", {
                "timestamp": time.time(),
                "status": "running",
                "uptime": 3600
            })
            
            # 保持连接一段时间以接收消息
            print("保持连接30秒以接收消息...")
            print("你可以使用MQTT客户端工具发送消息到以下主题进行测试:")
            print("- ios/camera/commands")
            print("- ios/camera/config")
            print("\n示例命令消息:")
            print('{"command": "start_detection", "parameters": {}}')
            print('{"command": "set_threshold", "parameters": {"confidence_threshold": 0.7}}')
            
            time.sleep(30)
            
        else:
            logger.error("MQTT连接失败!")
            
    except KeyboardInterrupt:
        logger.info("接收到中断信号，正在退出...")
        
    finally:
        # 发布离线状态
        mqtt_client.publish_camera_status("offline", {"reason": "normal_shutdown"})
        
        # 断开连接
        mqtt_client.disconnect()
        logger.info("MQTT连接已断开")

def context_manager_example():
    """使用上下文管理器的示例"""
    print("\n=== 上下文管理器使用示例 ===")
    
    # 使用with语句自动管理连接
    with IOSMqtt(broker_host="localhost", client_id="IOS_Context_Example") as mqtt:
        if mqtt.is_alive():
            logger.info("在with块中，MQTT连接活跃")
            
            # 发布消息
            mqtt.publish("ios/test/message", "这是一条测试消息")
            
            time.sleep(2)
        else:
            logger.error("MQTT连接失败")
    
    logger.info("退出with块，连接自动断开")

if __name__ == "__main__":
    print("MQTT使用示例")
    print("=" * 50)
    
    # 运行主示例
    main()
    
    # 运行上下文管理器示例
    context_manager_example()
    
    print("示例程序结束") 