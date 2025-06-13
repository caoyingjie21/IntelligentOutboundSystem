"""
日志管理模块使用示例
"""

from logger import get_logger, LogLevel, CameraLogger

def example_basic_usage():
    """基本使用示例"""
    print("=== 基本使用示例 ===")
    
    # 使用便捷函数
    from logger import info, warning, error, debug
    
    info("这是一条信息日志")
    warning("这是一条警告日志")
    error("这是一条错误日志")
    debug("这是一条调试日志（默认级别下看不到）")

def example_custom_logger():
    """自定义日志器示例"""
    print("\n=== 自定义日志器示例 ===")
    
    # 创建自定义日志器
    custom_logger = CameraLogger(
        name="CustomCamera",
        log_file="logs/custom_camera.log",
        log_level=LogLevel.DEBUG
    )
    
    custom_logger.debug("这是调试信息")
    custom_logger.info("相机初始化完成")
    custom_logger.warning("相机温度过高")
    custom_logger.error("相机连接失败")

def example_exception_logging():
    """异常日志示例"""
    print("\n=== 异常日志示例 ===")
    
    logger = get_logger("ExceptionTest")
    
    try:
        # 模拟一个异常
        result = 10 / 0
    except ZeroDivisionError as e:
        logger.exception("除零错误发生")
        logger.error(f"错误详情: {e}")

def example_different_levels():
    """不同日志级别示例"""
    print("\n=== 不同日志级别示例 ===")
    
    # 创建DEBUG级别的日志器
    debug_logger = CameraLogger(
        name="DebugLogger",
        log_level=LogLevel.DEBUG
    )
    
    print("DEBUG级别日志器输出:")
    debug_logger.debug("调试信息")
    debug_logger.info("普通信息")
    debug_logger.warning("警告信息")
    debug_logger.error("错误信息")
    debug_logger.critical("严重错误")
    
    # 创建WARNING级别的日志器
    warning_logger = CameraLogger(
        name="WarningLogger", 
        log_level=LogLevel.WARNING
    )
    
    print("\nWARNING级别日志器输出:")
    warning_logger.debug("调试信息（不会显示）")
    warning_logger.info("普通信息（不会显示）")
    warning_logger.warning("警告信息")
    warning_logger.error("错误信息")
    warning_logger.critical("严重错误")

if __name__ == "__main__":
    # 运行所有示例
    example_basic_usage()
    example_custom_logger()
    example_exception_logging()
    example_different_levels()
    
    print("\n=== 日志文件 ===")
    print("日志文件保存在 logs/ 目录下")
    print("- cameraservice_YYYYMMDD.log : 默认日志文件")
    print("- custom_camera.log : 自定义日志文件") 