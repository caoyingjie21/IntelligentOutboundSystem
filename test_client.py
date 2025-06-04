#!/usr/bin/env python3
"""
CoderService Socket 测试客户端
用于测试Socket服务器是否能正确接收消息
"""

import socket
import time
import sys

def test_socket_client():
    """测试Socket客户端连接和消息发送"""
    
    server_host = "127.0.0.1"
    server_port = 5000
    
    try:
        print(f"连接到 CoderService Socket服务器 {server_host}:{server_port}")
        
        # 创建TCP socket
        client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        client_socket.settimeout(10)  # 10秒超时
        
        # 连接到服务器
        client_socket.connect((server_host, server_port))
        print("✅ 连接成功！")
        
        # 发送测试消息
        test_messages = [
            "TEST_BARCODE_001",
            "TEST_BARCODE_002", 
            "TEST_BARCODE_003"
        ]
        
        for message in test_messages:
            print(f"发送消息: {message}")
            client_socket.send(message.encode('utf-8'))
            time.sleep(1)  # 间隔1秒
        
        print("✅ 所有测试消息已发送")
        print("保持连接30秒以便观察服务器日志...")
        
        # 保持连接一段时间
        time.sleep(30)
        
    except ConnectionRefusedError:
        print("❌ 连接被拒绝 - 请确保CoderService正在运行")
        return False
    except socket.timeout:
        print("❌ 连接超时")
        return False
    except Exception as e:
        print(f"❌ 发生错误: {e}")
        return False
    finally:
        try:
            client_socket.close()
            print("🔐 客户端连接已关闭")
        except:
            pass
    
    return True

if __name__ == "__main__":
    print("=== CoderService Socket 测试客户端 ===")
    success = test_socket_client()
    
    if success:
        print("✅ 测试完成")
        sys.exit(0)
    else:
        print("❌ 测试失败")
        sys.exit(1) 