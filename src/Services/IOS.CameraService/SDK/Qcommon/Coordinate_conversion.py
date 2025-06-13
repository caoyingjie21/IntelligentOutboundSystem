import numpy as np
from scipy.optimize import least_squares
import json
import os

class CoordinateTransformer:
    def __init__(self, matrix_path='transformation_matrix.json'):
        """
        初始化坐标转换器
        
        Args:
            matrix_path: 变换矩阵保存路径
        """
        self.transformation_matrix = None
        self.matrix_path = matrix_path
        self.load_transformation_matrix()
        
    def save_transformation_matrix(self):
        """
        保存变换矩阵到文件
        """
        if self.transformation_matrix is None:
            raise ValueError("没有可保存的变换矩阵")
            
        matrix_data = {
            'matrix': self.transformation_matrix.tolist()
        }
        
        with open(self.matrix_path, 'w') as f:
            json.dump(matrix_data, f)
            
    def load_transformation_matrix(self):
        """
        从文件加载变换矩阵
        """
        if os.path.exists(self.matrix_path):
            with open(self.matrix_path, 'r') as f:
                matrix_data = json.load(f)
                self.transformation_matrix = np.array(matrix_data['matrix'])
                
    def calculate_transformation_matrix(self, source_points, target_points):
        """
        计算从源坐标系到目标坐标系的变换矩阵
        
        Args:
            source_points: 源坐标系中的点，形状为(n, 3)的numpy数组，每行表示一个点的(x,y,z)坐标
            target_points: 目标坐标系中的点，形状为(n, 3)的numpy数组，每行表示一个点的(x,y,z)坐标
            
        Returns:
            transformation_matrix: 4x4的变换矩阵
        """
        if len(source_points) != len(target_points):
            raise ValueError("源点和目标点的数量必须相同")
            
        if len(source_points) < 4:
            raise ValueError("至少需要4组对应点来计算变换矩阵")
            
        # 构建最小二乘问题的目标函数
        def objective_function(params):
            # 从参数中提取旋转矩阵和平移向量
            rotation_matrix = params[:9].reshape(3, 3)
            translation = params[9:]
            
            # 计算变换后的点
            transformed_points = np.dot(source_points, rotation_matrix.T) + translation
            
            # 计算误差
            error = (transformed_points - target_points).flatten()
            return error
            
        # 初始参数（单位旋转矩阵和零平移向量）
        initial_params = np.array([1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0])
        
        # 使用最小二乘法求解
        result = least_squares(objective_function, initial_params, method='trf')
        
        # 提取结果
        rotation_matrix = result.x[:9].reshape(3, 3)
        translation = result.x[9:]
        
        # 构建4x4变换矩阵
        transformation_matrix = np.eye(4)
        transformation_matrix[:3, :3] = rotation_matrix
        transformation_matrix[:3, 3] = translation
        
        self.transformation_matrix = transformation_matrix
        # 保存变换矩阵
        self.save_transformation_matrix()
        return transformation_matrix

    def transform_angle_from_camera_to_robot(self, camera_angle_deg):
        """
        将相机坐标系中的角度转换为机器人坐标系中的角度
        
        Args:
            camera_angle_deg (float): 相机坐标系中的角度（度，0-180范围）
        
        Returns:
            float: 机器人坐标系中的角度（度）
        """
        if self.transformation_matrix is None:
            raise ValueError("请先完成坐标系标定，计算变换矩阵")
        
        # 将角度转换为弧度
        camera_angle_rad = np.radians(camera_angle_deg)
        
        # 在相机坐标系中创建两个方向向量来表示物体的朝向
        # 第一个点：物体中心
        center = np.array([0, 0, 0])
        
        # 第二个点：沿着检测角度方向的点（距离为1个单位）
        direction_point = np.array([
            np.cos(camera_angle_rad),  # x方向
            np.sin(camera_angle_rad),  # y方向  
            0                          # z方向为0（假设在xy平面内）
        ])
        
        # 获取变换矩阵的旋转部分（前3x3）
        rotation_matrix = self.transformation_matrix[:3, :3]
        
        # 转换两个点到机器人坐标系
        center_robot = np.dot(rotation_matrix, center)
        direction_robot = np.dot(rotation_matrix, direction_point)
        
        # 计算机器人坐标系中的方向向量
        direction_vector = direction_robot - center_robot
        
        # 计算机器人坐标系中的角度（在XY平面的投影）
        robot_angle_rad = np.arctan2(direction_vector[1], direction_vector[0])
        
        # 转换为度数并规范化到 [0, 360) 范围
        robot_angle_deg = np.degrees(robot_angle_rad) % 360
        
        return robot_angle_deg

    def calculate_robot_angle_and_compensation(self, camera_angle, target_angle=0.0):
        """
        计算机器人坐标系中的角度并返回补偿值
        
        Args:
            camera_angle (float): 相机坐标系中的角度
            target_angle (float): 目标角度（p_home的角度，默认0度）
        
        Returns:
            tuple: (robot_angle, compensation_angle)
        """
        try:
            # 1. 将相机角度转换为机器人坐标系角度
            robot_angle = self.transform_angle_from_camera_to_robot(camera_angle)
            
            # 2. 计算到目标角度的补偿值
            angle_diff = (target_angle - robot_angle) % 360
            
            # 3. 选择最短旋转路径
            if angle_diff > 180:
                angle_diff -= 360
            
            compensation_angle = angle_diff
            
            # 4. 记录调试信息
            self.add_log(f"角度转换: 相机={camera_angle:.1f}° → 机器人={robot_angle:.1f}°", "debug")
            self.add_log(f"角度补偿: 当前={robot_angle:.1f}° → 目标={target_angle:.1f}° → 补偿={compensation_angle:.1f}°", "info")
            
            return robot_angle, compensation_angle
            
        except Exception as e:
            self.add_log(f"角度转换失败: {str(e)}", "error")
            return camera_angle, 0.0  # 失败时返回原始角度和零补偿

    @staticmethod
    def calculate_3d_coordinates_from_depth(x, y, depth_data, camera_params):
        """
        从深度数据计算3D坐标
        Args:
            x: 图像x坐标
            y: 图像y坐标
            depth_data: 深度数据
            camera_params: 相机参数
        Returns:
            tuple: (success, (x_cam, y_cam, z))
                success (bool): 是否成功计算坐标
                x_cam, y_cam, z: 相机坐标系下的3D坐标
        """
        try:
            # 检查输入参数
            if depth_data is None or camera_params is None:
                return False, (0, 0, 0)
                
            # 确保深度数据是列表或类似数组的对象
            if not hasattr(depth_data, '__len__'):
                return False, (0, 0, 0)
                
            # 检查相机参数是否有必要的属性
            required_attrs = ['width', 'height', 'cx', 'cy', 'fx', 'fy', 'k1', 'k2', 'f2rc', 'cam2worldMatrix']
            for attr in required_attrs:
                if not hasattr(camera_params, attr):
                    return False, (0, 0, 0)
                
            # 检查坐标是否在有效范围内
            if x < 0 or x >= camera_params.width or y < 0 or y >= camera_params.height:
                return False, (0, 0, 0)
                
            # 计算索引
            index = y * camera_params.width + x
            if index >= len(depth_data):
                return False, (0, 0, 0)
                
            # 获取深度值
            z = depth_data[index]
            
            # 检查深度值是否有效
            if z <= 0:
                return False, (0, 0, 0)
                
            # 计算相机坐标系下的x和y坐标
            xp = (camera_params.cx - x) / camera_params.fx
            yp = (camera_params.cy - y) / camera_params.fy
            
            # 计算径向畸变
            r2 = (xp * xp + yp * yp)
            r4 = r2 * r2
            k = 1 + camera_params.k1 * r2 + camera_params.k2 * r4
            
            xd = xp * k
            yd = yp * k
            
            # 计算相机坐标系下的坐标
            s0 = np.sqrt(xd*xd + yd*yd + 1)
            x_cam = xd * z / s0
            y_cam = yd * z / s0
            z_cam = z / s0 - camera_params.f2rc
            
            # 转换到世界坐标系
            # 检查cam2worldMatrix是否为有效值
            if not hasattr(camera_params.cam2worldMatrix, '__len__') or len(camera_params.cam2worldMatrix) != 16:
                return True, (x_cam, y_cam, z_cam)  # 返回相机坐标系下的坐标
                
            m_c2w = np.array(camera_params.cam2worldMatrix).reshape(4, 4)
            x_world = (m_c2w[0, 3] + z_cam * m_c2w[0, 2] + y_cam * m_c2w[0, 1] + x_cam * m_c2w[0, 0])
            y_world = (m_c2w[1, 3] + z_cam * m_c2w[1, 2] + y_cam * m_c2w[1, 1] + x_cam * m_c2w[1, 0])
            z_world = (m_c2w[2, 3] + z_cam * m_c2w[2, 2] + y_cam * m_c2w[2, 1] + x_cam * m_c2w[2, 0])
            
            return True, (x_world, y_world, z_world)
        except Exception:
            return False, (0, 0, 0)

    def transform_point(self, point):
        """
        使用计算得到的变换矩阵转换单个点
        
        Args:
            point: 源坐标系中的点，形状为(3,)的numpy数组，表示(x,y,z)坐标
            
        Returns:
            transformed_point: 目标坐标系中的点，形状为(3,)的numpy数组
        """
        if self.transformation_matrix is None:
            raise ValueError("请先计算变换矩阵")
            
        # 将点转换为齐次坐标
        homogeneous_point = np.append(point, 1)
        
        # 应用变换
        transformed_homogeneous = np.dot(self.transformation_matrix, homogeneous_point)
        
        # 转换回非齐次坐标
        transformed_point = transformed_homogeneous[:3] / transformed_homogeneous[3]
        return transformed_point
        
    def transform_points(self, points):
        """
        使用计算得到的变换矩阵转换多个点
        
        Args:
            points: 源坐标系中的点，形状为(n, 3)的numpy数组，每行表示一个点的(x,y,z)坐标
            
        Returns:
            transformed_points: 目标坐标系中的点，形状为(n, 3)的numpy数组
        """
        if self.transformation_matrix is None:
            raise ValueError("请先计算变换矩阵")
            
        # 将点转换为齐次坐标
        homogeneous_points = np.hstack([points, np.ones((len(points), 1))])
        
        # 应用变换
        transformed_homogeneous = np.dot(homogeneous_points, self.transformation_matrix.T)
        
        # 转换回非齐次坐标
        transformed_points = transformed_homogeneous[:, :3] / transformed_homogeneous[:, 3:]
        return transformed_points

# 使用示例
if __name__ == "__main__":
    # 创建坐标转换器实例
    transformer = CoordinateTransformer()
    
    # 示例：源坐标系中的点
    source_points = np.array([
        [49.24, 126.58, 693.11],    # 点1
        [-96.26, 161.03, 694.54],    # 点2
        [91.29, -106.77, 692.71],    # 点3
        [-64.92, -38.69, 705.15],     # 点4
        [-121.28, 136.80, 717.99],
        [44.85,166.02,715.75],
        [55.59,-31.76,724.85],
        [-99.38,-116.60,730.93],
    ])
    
    # 示例：目标坐标系中的对应点
    target_points = np.array([
        [122.608, 354.251, -70.000],    # 点1
        [173.772, 217.600, -70.019],    # 点2
        [-101.206, 376.620, -51.069],    # 点3
        [-24.955, 222.800, -81.085],     # 点4
        [150.499, 186.214, -80.007],    # 点5
        [158.587,355.957, -80.007],    # 点6
        [-32.569, 348.415, -80.016],    # 点7
        [-105.295,183.443,-80.031],     # 点8
    ])
    
    # 计算变换矩阵
    transformation_matrix = transformer.calculate_transformation_matrix(source_points, target_points)
    print("变换矩阵：")
    print(transformation_matrix)
    
    # 测试转换单个点
    test_point = np.array([-53.09, 57.95, 612.69])
    transformed_point = transformer.transform_point(test_point)
    print("\n测试点转换结果：")
    print(f"原始点: {test_point}")
    print(f"转换后: {transformed_point}")
    
    # 测试转换多个点
    test_points = np.array([
        [0.5, 0.5, 0.5],
        [0.7, 0.3, 0.2]
    ])
    transformed_points = transformer.transform_points(test_points)
    print("\n多个点转换结果：")
    print("原始点：")
    print(test_points)
    print("转换后：")
    print(transformed_points)
