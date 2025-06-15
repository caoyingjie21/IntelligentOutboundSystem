"""
@Description :   MQTT客户端类，用于相机服务的消息订阅、发布和回调处理
@Author      :   Cao Yingjie
@Time        :   2025/04/23 10:00:00
"""

import paho.mqtt.client as mqtt
import json
import time
import threading
from typing import Dict, Callable, Any, Optional
from dataclasses import dataclass
from enum import Enum
import logging


class MqttQos(Enum):
    """MQTT服务质量等级"""
    AT_MOST_ONCE = 0    # 最多一次
    AT_LEAST_ONCE = 1   # 至少一次
    EXACTLY_ONCE = 2    # 恰好一次


@dataclass
class MqttConfig:
    """MQTT配置类"""
    broker_host: str = "localhost"
    broker_port: int = 1883
    username: Optional[str] = None
    password: Optional[str] = None
    client_id: Optional[str] = None
    keepalive: int = 60
    clean_session: bool = True
    reconnect_delay: int = 5
    max_reconnect_attempts: int = 10


class CameraMqtt:
    """
    相机MQTT客户端类
    提供MQTT连接、订阅、发布和回调功能
    """
    
    def __init__(self, config: MqttConfig = None):
        """
        初始化MQTT客户端
        
        Args:
            config (MqttConfig): MQTT配置对象
        """
        self.config = config or MqttConfig()
        self.client = None
        self.is_connected = False
        self.logger = logging.getLogger(__name__)
        
        # 回调函数字典 {topic: callback_function}
        self.topic_callbacks: Dict[str, Callable] = {}
        
        # 连接状态回调
        self.on_connect_callback: Optional[Callable] = None
        self.on_disconnect_callback: Optional[Callable] = None
        
        # 重连相关
        self.reconnect_thread = None
        self.reconnect_attempts = 0
        self.should_reconnect = True
        
        # 线程锁
        self._lock = threading.Lock()
        
        self._setup_client()
    
    def _setup_client(self):
        """设置MQTT客户端"""
        client_id = self.config.client_id or f"camera_mqtt_{int(time.time())}"
        self.client = mqtt.Client(client_id=client_id, clean_session=self.config.clean_session)
        
        # 设置用户名和密码
        if self.config.username and self.config.password:
            self.client.username_pw_set(self.config.username, self.config.password)
        
        # 设置回调函数
        self.client.on_connect = self._on_connect
        self.client.on_disconnect = self._on_disconnect
        self.client.on_message = self._on_message
        self.client.on_publish = self._on_publish
        self.client.on_subscribe = self._on_subscribe
        self.client.on_unsubscribe = self._on_unsubscribe
    
    def connect(self) -> bool:
        """
        连接到MQTT代理
        
        Returns:
            bool: 连接是否成功
        """
        try:
            self.logger.info(f"正在连接到MQTT代理 {self.config.broker_host}:{self.config.broker_port}")
            
            result = self.client.connect(
                self.config.broker_host,
                self.config.broker_port,
                self.config.keepalive
            )
            
            if result == mqtt.MQTT_ERR_SUCCESS:
                # 启动网络循环
                self.client.loop_start()
                
                # 等待连接建立
                timeout = 10  # 10秒超时
                start_time = time.time()
                while not self.is_connected and (time.time() - start_time) < timeout:
                    time.sleep(0.1)
                
                if self.is_connected:
                    self.logger.info("MQTT连接成功")
                    self.reconnect_attempts = 0
                    return True
                else:
                    self.logger.error("MQTT连接超时")
                    return False
            else:
                self.logger.error(f"MQTT连接失败，错误代码: {result}")
                return False
                
        except Exception as e:
            self.logger.error(f"MQTT连接异常: {str(e)}")
            return False
    
    def disconnect(self):
        """断开MQTT连接"""
        try:
            self.should_reconnect = False
            
            if self.reconnect_thread and self.reconnect_thread.is_alive():
                self.reconnect_thread.join(timeout=2)
            
            if self.client and self.is_connected:
                self.client.loop_stop()
                self.client.disconnect()
                self.logger.info("MQTT连接已断开")
                
        except Exception as e:
            self.logger.error(f"断开MQTT连接时出错: {str(e)}")
    
    def subscribe(self, topic: str, qos: MqttQos = MqttQos.AT_LEAST_ONCE, callback: Callable = None) -> bool:
        """
        订阅主题
        
        Args:
            topic (str): 要订阅的主题
            qos (MqttQos): 服务质量等级
            callback (Callable): 消息回调函数
            
        Returns:
            bool: 订阅是否成功
        """
        if not self.is_connected:
            self.logger.error("MQTT未连接，无法订阅主题")
            return False
        
        try:
            with self._lock:
                result, mid = self.client.subscribe(topic, qos.value)
                
                if result == mqtt.MQTT_ERR_SUCCESS:
                    # 注册回调函数
                    if callback:
                        self.topic_callbacks[topic] = callback
                    
                    self.logger.info(f"成功订阅主题: {topic}, QoS: {qos.value}")
                    return True
                else:
                    self.logger.error(f"订阅主题失败: {topic}, 错误代码: {result}")
                    return False
                    
        except Exception as e:
            self.logger.error(f"订阅主题异常: {str(e)}")
            return False
    
    def unsubscribe(self, topic: str) -> bool:
        """
        取消订阅主题
        
        Args:
            topic (str): 要取消订阅的主题
            
        Returns:
            bool: 取消订阅是否成功
        """
        if not self.is_connected:
            self.logger.error("MQTT未连接，无法取消订阅")
            return False
        
        try:
            with self._lock:
                result, mid = self.client.unsubscribe(topic)
                
                if result == mqtt.MQTT_ERR_SUCCESS:
                    # 移除回调函数
                    if topic in self.topic_callbacks:
                        del self.topic_callbacks[topic]
                    
                    self.logger.info(f"成功取消订阅主题: {topic}")
                    return True
                else:
                    self.logger.error(f"取消订阅主题失败: {topic}, 错误代码: {result}")
                    return False
                    
        except Exception as e:
            self.logger.error(f"取消订阅主题异常: {str(e)}")
            return False
    
    def publish(self, topic: str, payload: Any, qos: MqttQos = MqttQos.AT_LEAST_ONCE, retain: bool = False) -> bool:
        """
        发布消息
        
        Args:
            topic (str): 发布主题
            payload (Any): 消息内容（支持字典、字符串等）
            qos (MqttQos): 服务质量等级
            retain (bool): 是否保留消息
            
        Returns:
            bool: 发布是否成功
        """
        if not self.is_connected:
            self.logger.error("MQTT未连接，无法发布消息")
            return False
        
        try:
            # 处理消息内容
            if isinstance(payload, (dict, list)):
                message = json.dumps(payload, ensure_ascii=False)
                print(message)
            else:
                message = str(payload)
            
            result = self.client.publish(topic, message, qos.value, retain)
            
            if result.rc == mqtt.MQTT_ERR_SUCCESS:
                self.logger.debug(f"消息发布成功: {topic}")
                return True
            else:
                self.logger.error(f"消息发布失败: {topic}, 错误代码: {result.rc}")
                return False
                
        except Exception as e:
            self.logger.error(f"发布消息异常: {str(e)}")
            return False
    
    def publish_camera_data(self, camera_id: str, data_type: str, data: Any) -> bool:
        """
        发布相机数据的便捷方法
        
        Args:
            camera_id (str): 相机ID
            data_type (str): 数据类型 (depth, intensity, coordinates等)
            data (Any): 数据内容
            
        Returns:
            bool: 发布是否成功
        """
        topic = f"camera/{camera_id}/{data_type}"
        
        payload = {
            "camera_id": camera_id,
            "data_type": data_type,
            "timestamp": time.time(),
            "data": data
        }
        
        return self.publish(topic, payload)
    
    def set_on_connect_callback(self, callback: Callable):
        """设置连接成功回调函数"""
        self.on_connect_callback = callback
    
    def set_on_disconnect_callback(self, callback: Callable):
        """设置断开连接回调函数"""
        self.on_disconnect_callback = callback
    
    def _on_connect(self, client, userdata, flags, rc):
        """连接回调"""
        if rc == 0:
            self.is_connected = True
            self.logger.info("MQTT连接建立成功")
            
            # 调用用户定义的连接回调
            if self.on_connect_callback:
                try:
                    self.on_connect_callback(client, userdata, flags, rc)
                except Exception as e:
                    self.logger.error(f"连接回调函数执行出错: {str(e)}")
        else:
            self.is_connected = False
            self.logger.error(f"MQTT连接失败，返回码: {rc}")
    
    def _on_disconnect(self, client, userdata, rc):
        """断开连接回调"""
        self.is_connected = False
        
        if rc != 0:
            self.logger.warning(f"MQTT意外断开连接，返回码: {rc}")
            
            # 启动自动重连
            if self.should_reconnect:
                self._start_reconnect()
        else:
            self.logger.info("MQTT正常断开连接")
        
        # 调用用户定义的断开连接回调
        if self.on_disconnect_callback:
            try:
                self.on_disconnect_callback(client, userdata, rc)
            except Exception as e:
                self.logger.error(f"断开连接回调函数执行出错: {str(e)}")
    
    def _on_message(self, client, userdata, msg):
        """消息接收回调"""
        try:
            topic = msg.topic
            payload = msg.payload.decode('utf-8')
            
            self.logger.debug(f"收到消息 - 主题: {topic}, 内容: {payload}")
            
            # 查找对应的回调函数
            callback = None
            
            # 精确匹配
            if topic in self.topic_callbacks:
                callback = self.topic_callbacks[topic]
            else:
                # 通配符匹配
                for registered_topic, registered_callback in self.topic_callbacks.items():
                    if self._topic_matches(registered_topic, topic):
                        callback = registered_callback
                        break
            
            if callback:
                try:
                    # 尝试解析JSON
                    try:
                        data = json.loads(payload)
                    except json.JSONDecodeError:
                        data = payload
                    
                    # 调用回调函数
                    callback(topic, data, msg)
                    
                except Exception as e:
                    self.logger.error(f"消息回调函数执行出错: {str(e)}")
            else:
                self.logger.warning(f"未找到主题 {topic} 的回调函数")
                
        except Exception as e:
            self.logger.error(f"处理接收消息时出错: {str(e)}")
    
    def _on_publish(self, client, userdata, mid):
        """发布回调"""
        self.logger.debug(f"消息发布确认，消息ID: {mid}")
    
    def _on_subscribe(self, client, userdata, mid, granted_qos):
        """订阅回调"""
        self.logger.debug(f"订阅确认，消息ID: {mid}, QoS: {granted_qos}")
    
    def _on_unsubscribe(self, client, userdata, mid):
        """取消订阅回调"""
        self.logger.debug(f"取消订阅确认，消息ID: {mid}")
    
    def _topic_matches(self, pattern: str, topic: str) -> bool:
        """
        检查主题是否匹配通配符模式
        支持MQTT通配符: + (单级) 和 # (多级)
        """
        pattern_parts = pattern.split('/')
        topic_parts = topic.split('/')
        
        i = j = 0
        while i < len(pattern_parts) and j < len(topic_parts):
            if pattern_parts[i] == '#':
                return True
            elif pattern_parts[i] == '+':
                i += 1
                j += 1
            elif pattern_parts[i] == topic_parts[j]:
                i += 1
                j += 1
            else:
                return False
        
        return i == len(pattern_parts) and j == len(topic_parts)
    
    def _start_reconnect(self):
        """启动重连线程"""
        if self.reconnect_thread and self.reconnect_thread.is_alive():
            return
        
        self.reconnect_thread = threading.Thread(target=self._reconnect_loop, daemon=True)
        self.reconnect_thread.start()
    
    def _reconnect_loop(self):
        """重连循环"""
        while self.should_reconnect and self.reconnect_attempts < self.config.max_reconnect_attempts:
            self.reconnect_attempts += 1
            self.logger.info(f"尝试重连 MQTT ({self.reconnect_attempts}/{self.config.max_reconnect_attempts})")
            
            time.sleep(self.config.reconnect_delay)
            
            if self.connect():
                self.logger.info("MQTT重连成功")
                break
            else:
                self.logger.warning(f"MQTT重连失败，{self.config.reconnect_delay}秒后重试")
        
        if self.reconnect_attempts >= self.config.max_reconnect_attempts:
            self.logger.error("MQTT重连次数超过限制，停止重连")
    
    def get_connection_status(self) -> Dict[str, Any]:
        """
        获取连接状态信息
        
        Returns:
            Dict: 连接状态信息
        """
        return {
            "connected": self.is_connected,
            "broker_host": self.config.broker_host,
            "broker_port": self.config.broker_port,
            "client_id": self.client._client_id.decode() if self.client else None,
            "reconnect_attempts": self.reconnect_attempts,
            "subscribed_topics": list(self.topic_callbacks.keys())
        }
    
    def __enter__(self):
        """上下文管理器入口"""
        self.connect()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        """上下文管理器退出"""
        self.disconnect()
    
    def __del__(self):
        """析构函数"""
        self.disconnect()