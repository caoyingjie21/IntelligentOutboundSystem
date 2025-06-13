from mqtt.CameraMqtt import CameraMqtt, MqttConfig, MqttQos
import time

def on_camera_command(topic: str, data, msg):
  print(f"收到调度器指令: {topic} -> {data}")
  if data == "IN" or data == "OUT":
    print("开始相机")
  elif data == "stop":
    print("停止相机")

if __name__ == "__main__":
  mqtt_config = MqttConfig(
    broker_host="127.0.0.1",
    broker_port=1883,
    client_id="camera_service_test",
  )

  camera_mqtt = CameraMqtt(mqtt_config)
  camera_mqtt.connect()
  camera_mqtt.subscribe("vision/height", qos=MqttQos.EXACTLY_ONCE, callback=on_camera_command)
  while True:
    time.sleep(1)
  camera_mqtt.disconnect()

