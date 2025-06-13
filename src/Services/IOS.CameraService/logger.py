import logging
import os
from datetime import datetime
from typing import Optional
from enum import Enum

class LogLevel(Enum):
    """日志级别枚举"""
    DEBUG = logging.DEBUG
    INFO = logging.INFO
    WARNING = logging.WARNING
    ERROR = logging.ERROR
    CRITICAL = logging.CRITICAL

class CameraLogger:
    """轻量级的日志管理器"""
    
    def __init__(self, 
                 name: str = "CameraService",
                 log_file: Optional[str] = None,
                 log_level: LogLevel = LogLevel.INFO,
                 max_file_size: int = 10 * 1024 * 1024,  # 10MB
                 backup_count: int = 5):
        """
        初始化日志管理器
        
        Args:
            name: 日志器名称
            log_file: 日志文件路径，如果为None则只输出到控制台
            log_level: 日志级别
            max_file_size: 日志文件最大大小（字节）
            backup_count: 备份文件数量
        """
        self.logger = logging.getLogger(name)
        self.logger.setLevel(log_level.value)
        
        # 清除已有的处理器
        for handler in self.logger.handlers[:]:
            self.logger.removeHandler(handler)
        
        # 设置日志格式
        formatter = logging.Formatter(
            '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S'
        )
        
        # 控制台处理器
        console_handler = logging.StreamHandler()
        console_handler.setFormatter(formatter)
        self.logger.addHandler(console_handler)
        
        # 文件处理器（如果指定了日志文件）
        if log_file:
            # 确保日志目录存在
            log_dir = os.path.dirname(log_file)
            if log_dir and not os.path.exists(log_dir):
                os.makedirs(log_dir)
            
            from logging.handlers import RotatingFileHandler
            file_handler = RotatingFileHandler(
                log_file,
                maxBytes=max_file_size,
                backupCount=backup_count,
                encoding='utf-8'
            )
            file_handler.setFormatter(formatter)
            self.logger.addHandler(file_handler)
    
    def debug(self, message: str, *args, **kwargs):
        """调试日志"""
        self.logger.debug(message, *args, **kwargs)
    
    def info(self, message: str, *args, **kwargs):
        """信息日志"""
        self.logger.info(message, *args, **kwargs)
    
    def warning(self, message: str, *args, **kwargs):
        """警告日志"""
        self.logger.warning(message, *args, **kwargs)
    
    def error(self, message: str, *args, **kwargs):
        """错误日志"""
        self.logger.error(message, *args, **kwargs)
    
    def critical(self, message: str, *args, **kwargs):
        """严重错误日志"""
        self.logger.critical(message, *args, **kwargs)
    
    def exception(self, message: str, *args, **kwargs):
        """异常日志（包含异常堆栈信息）"""
        self.logger.exception(message, *args, **kwargs)
    
    def set_level(self, level: LogLevel):
        """设置日志级别"""
        self.logger.setLevel(level.value)

# 全局日志器实例
_default_logger = None

def get_logger(name: str = "CameraService", 
               log_file: Optional[str] = None,
               log_level: LogLevel = LogLevel.INFO) -> CameraLogger:
    """获取默认日志器实例"""
    global _default_logger
    if _default_logger is None:
        # 如果没有指定日志文件，创建默认的日志文件路径
        if log_file is None:
            log_dir = "logs"
            if not os.path.exists(log_dir):
                os.makedirs(log_dir)
            log_file = os.path.join(log_dir, f"{name.lower()}_{datetime.now().strftime('%Y%m%d')}.log")
        
        _default_logger = CameraLogger(name, log_file, log_level)
    return _default_logger

# 便捷的日志函数
def debug(message: str, *args, **kwargs):
    """调试日志"""
    get_logger().debug(message, *args, **kwargs)

def info(message: str, *args, **kwargs):
    """信息日志"""
    get_logger().info(message, *args, **kwargs)

def warning(message: str, *args, **kwargs):
    """警告日志"""
    get_logger().warning(message, *args, **kwargs)

def error(message: str, *args, **kwargs):
    """错误日志"""
    get_logger().error(message, *args, **kwargs)

def critical(message: str, *args, **kwargs):
    """严重错误日志"""
    get_logger().critical(message, *args, **kwargs)

def exception(message: str, *args, **kwargs):
    """异常日志（包含异常堆栈信息）"""
    get_logger().exception(message, *args, **kwargs) 