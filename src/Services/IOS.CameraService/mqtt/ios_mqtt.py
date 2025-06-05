import paho.mqtt.client as mqtt
import json
import threading
import time
from typing import Callable, Optional, Dict, Any
from datetime import datetime

class IOSMqtt:
    """
    IOS系统MQTT客户端类
    提供MQTT连接、订阅、发布和回调处理功能
    """
    
    def __init__(self, 
                 broker_host: str = "localhost", 
                 broker_port: int = 1883,
                 client_id: str = "IOS_CameraService",
                 username: Optional[str] = None,
                 password: Optional[str] = None,
                 keepalive: int = 60):
        """
        初始化MQTT客户端
        
        Args:
            broker_host (str): MQTT代理服务器地址
            broker_port (int): MQTT代理服务器端口
            client_id (str): 客户端ID
            username (str, optional): 用户名
            password (str, optional): 密码
            keepalive (int): 心跳间隔时间(秒)
        """
        self.broker_host = broker_host
        self.broker_port = broker_port
        self.client_id = client_id
        self.username = username
        self.password = password
        self.keepalive = keepalive
        
        # 初始化MQTT客户端
        self.client = mqtt.Client(client_id=self.client_id)
        
        # 连接状态
        self.is_connected = False
        self.connection_lock = threading.Lock()
        
        # 订阅主题和回调函数字典
        self.subscriptions: Dict[str, Callable] = {}
        
        # 设置MQTT客户端回调
        self.client.on_connect = self._on_connect
        self.client.on_disconnect = self._on_disconnect
        self.client.on_message = self._on_message
        self.client.on_subscribe = self._on_subscribe
        self.client.on_publish = self._on_publish
        self.client.on_log = self._on_log
        
        # 设置用户名和密码
        if self.username and self.password:
            self.client.username_pw_set(self.username, self.password)

    def connect(self, timeout: float = 10.0) -> bool:
        """
        连接到MQTT代理服务器
        
        Args:
            timeout (float): 连接超时时间(秒)
            
        Returns:
            bool: 连接是否成功
        """
        try:
            with self.connection_lock:
                if self.is_connected:
                    return True
                
                # 连接到代理
                result = self.client.connect(self.broker_host, self.broker_port, self.keepalive)
                
                if result == mqtt.MQTT_ERR_SUCCESS:
                    # 启动网络循环
                    self.client.loop_start()
                    
                    # 等待连接建立
                    start_time = time.time()
                    while not self.is_connected and (time.time() - start_time) < timeout:
                        time.sleep(0.1)
                    
                    if self.is_connected:
                        return True
                    else:
                        return False
                else:
                    return False
                    
        except Exception as e:
            return False

    def disconnect(self):
        """断开MQTT连接"""
        try:
            with self.connection_lock:
                if self.is_connected:
                    self.client.loop_stop()
                    self.client.disconnect()
                    self.is_connected = False
        except Exception as e:
            pass

    def subscribe(self, topic: str, callback: Callable[[str, str], None], qos: int = 1) -> bool:
        """
        订阅MQTT主题
        
        Args:
            topic (str): 要订阅的主题
            callback (Callable): 消息回调函数，接收参数(topic, message)
            qos (int): 服务质量等级 (0, 1, 2)
            
        Returns:
            bool: 订阅是否成功
        """
        try:
            if not self.is_connected:
                return False
            
            # 保存回调函数
            self.subscriptions[topic] = callback
            
            # 订阅主题
            result, mid = self.client.subscribe(topic, qos)
            
            if result == mqtt.MQTT_ERR_SUCCESS:
                return True
            else:
                return False
                
        except Exception as e:
            return False

    def unsubscribe(self, topic: str) -> bool:
        """
        取消订阅MQTT主题
        
        Args:
            topic (str): 要取消订阅的主题
            
        Returns:
            bool: 取消订阅是否成功
        """
        try:
            if not self.is_connected:
                return False
            
            # 取消订阅
            result, mid = self.client.unsubscribe(topic)
            
            if result == mqtt.MQTT_ERR_SUCCESS:
                # 移除回调函数
                if topic in self.subscriptions:
                    del self.subscriptions[topic]
                return True
            else:
                return False
                
        except Exception as e:
            return False

    def publish(self, topic: str, message: Any, qos: int = 1, retain: bool = False) -> bool:
        """
        发布消息到MQTT主题
        
        Args:
            topic (str): 发布的主题
            message (Any): 要发布的消息（支持字符串、字典、列表等）
            qos (int): 服务质量等级 (0, 1, 2)
            retain (bool): 是否保留消息
            
        Returns:
            bool: 发布是否成功
        """
        try:
            if not self.is_connected:
                return False
            
            # 处理消息格式
            if isinstance(message, (dict, list)):
                payload = json.dumps(message, ensure_ascii=False)
            else:
                payload = str(message)
            
            # 发布消息
            result = self.client.publish(topic, payload, qos, retain)
            
            if result.rc == mqtt.MQTT_ERR_SUCCESS:
                return True
            else:
                return False
                
        except Exception as e:
            return False

    def _on_connect(self, client, userdata, flags, rc):
        """连接回调"""
        if rc == 0:
            self.is_connected = True
            
            # 重新订阅之前的主题
            for topic in self.subscriptions.keys():
                client.subscribe(topic)

    def _on_disconnect(self, client, userdata, rc):
        """断开连接回调"""
        self.is_connected = False

    def _on_message(self, client, userdata, msg):
        """消息接收回调"""
        try:
            topic = msg.topic
            payload = msg.payload.decode('utf-8')
            
            # 查找对应的回调函数
            callback = self.subscriptions.get(topic)
            if callback:
                try:
                    callback(topic, payload)
                except Exception as e:
                    pass
                
        except Exception as e:
            pass

    def _on_subscribe(self, client, userdata, mid, granted_qos):
        """订阅成功回调"""
        pass

    def _on_publish(self, client, userdata, mid):
        """发布成功回调"""
        pass

    def _on_log(self, client, userdata, level, buf):
        """日志回调"""
        pass

    def is_alive(self) -> bool:
        """
        检查MQTT连接是否活跃
        
        Returns:
            bool: 连接是否活跃
        """
        return self.is_connected

    def get_subscriptions(self) -> list:
        """
        获取当前订阅的主题列表
        
        Returns:
            list: 主题列表
        """
        return list(self.subscriptions.keys())

    def __enter__(self):
        """上下文管理器入口"""
        self.connect()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器出口"""
        self.disconnect()

    def __del__(self):
        """析构函数"""
        try:
            self.disconnect()
        except:
            pass