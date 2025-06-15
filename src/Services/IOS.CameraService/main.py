from dataclasses import dataclass
from mqtt.CameraMqtt import CameraMqtt, MqttConfig, MqttQos
import time
import sys
sys.path.append("./SDK")
sys.path.append("E:\CaoSpace\IOS\IntelligentOutboundSystem\src\Services\IOS.CameraService\SDK")
from SDK.SickSDK import QtVisionSick
from logger import get_logger, LogLevel
import json

# 初始化日志器
logger = get_logger("CameraService", log_level=LogLevel.INFO)


def on_camera_command(topic: str, data, msg):
  try:
    print(data)
    direction = data["direction"]
    logger.info(f"收到调度器指令: {topic} -> {direction}")
    if direction == "IN" or direction == "OUT":
      # min_z = camera.get_min_z_coordinate()
      min_z = 2.1
      logger.info(f"开始相机，方向: {direction}, 最小高度: {min_z}")
      camera_mqtt.publish("vision/height/result", {"min_height": min_z})
  except Exception as e:
    logger.exception(f"数据非json格式: {e}")

if __name__ == "__main__":
  logger.info("相机服务启动中...")
  
  mqtt_config = MqttConfig(
    broker_host="127.0.0.1",
    broker_port=1883,
    client_id="camera_service_test",
  )
  
  try:
    # debug 假设已初始化相机
    logger.info("初始化相机连接...")
    camera = QtVisionSick(ipAddr="192.168.10.5", port=2122, protocol="Cola2", use_single_step=False)
    # camera.connect()
    logger.info("相机连接成功")

    logger.info("初始化MQTT连接...")
    camera_mqtt = CameraMqtt(mqtt_config)
    if not camera_mqtt.connect():
      logger.error("MQTT连接失败")
      sys.exit(1)
    logger.info("MQTT连接成功")

    logger.info("订阅MQTT主题...")
    camera_mqtt.subscribe("vision/height", qos=MqttQos.EXACTLY_ONCE, callback=on_camera_command)
    logger.info("成功订阅主题: vision/height")
    
    logger.info("相机服务启动完成，开始等待指令...")
    
    # 保持程序运行
    try:
      while True:
        time.sleep(1)
    except KeyboardInterrupt:
      logger.info("收到中断信号，正在关闭服务...")
      camera_mqtt.disconnect()
      logger.info("相机服务已关闭")
      
  except Exception as e:
    logger.exception(f"相机服务启动失败: {e}")
    sys.exit(1)

