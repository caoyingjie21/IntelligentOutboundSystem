using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace IOS.Shared.Models;

/// <summary>
/// 机器状态响应模型
/// </summary>
public class MachineStatusResponse
{
    [JsonPropertyName("machine")]
    public MachineInfo Machine { get; set; } = new();

    [JsonPropertyName("layer")]
    public LayerInfo Layer { get; set; } = new();

    [JsonPropertyName("volume")]
    public VolumeInfo Volume { get; set; } = new();

    [JsonPropertyName("topLayerByCodes")]
    public int TopLayerByCodes { get; set; }

    [JsonPropertyName("isVariety")]
    public bool IsVariety { get; set; }

    public bool HeightNotEnough { get; set; }

    public double LiftingHeight { get; set; }

    public double Height { get; set; }

    public double StackHeight { get; set; }

    public string Direction { get; set; } = string.Empty;

    public void Clear()
    {
        Machine = new MachineInfo();
        Layer = new LayerInfo();
        Volume = new VolumeInfo();
        TopLayerByCodes = 0;
        HeightNotEnough = false;
        LiftingHeight = 0;
        Height = 0;
        StackHeight = 0;
        Direction = string.Empty;
        IsVariety = false;
    }
}

/// <summary>
/// 机器信息
/// </summary>
public class MachineInfo
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("info")]
    public string Info { get; set; } = "运行正常";
}

/// <summary>
/// 层信息
/// </summary>
public class LayerInfo
{
    [JsonPropertyName("layer")]
    public int Layer { get; set; }

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; set; }

    [JsonPropertyName("detectNum")]
    public int DetectNum { get; set; }

    [JsonPropertyName("topLayerCount")]
    public int TopLayerCount { get; set; }

    public bool IsTrapezoid { get; set; }
}

/// <summary>
/// 货物信息
/// </summary>
public class VolumeInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "暂无";

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = "暂无";

    [JsonPropertyName("package_length")]
    public float PackageLength { get; set; }

    [JsonPropertyName("package_weight")]
    public float PackageWeight { get; set; }

    [JsonPropertyName("package_height")]
    public float PackageHeight { get; set; }
}

/// <summary>
/// 检测器状态响应
/// </summary>
public class DetectorStatusResponse
{
    [JsonPropertyName("detectNum")]
    public int DetectNum { get; set; }
    
    [JsonPropertyName("detectBoxAverage")]
    public List<double> DetectBoxAverage { get; set; } = new();
}

/// <summary>
/// 读码信息
/// </summary>
public class CodeInfo
{
    [Required]
    public string Order { get; set; } = string.Empty;

    [Required]
    public string Codes { get; set; } = string.Empty;

    public string Direction { get; set; } = string.Empty;

    public double StackHeight { get; set; }

    public DateTime ReadTime { get; set; } = DateTime.UtcNow;

    public QualityReport? Quality { get; set; }
}

/// <summary>
/// 质量报告
/// </summary>
public class QualityReport
{
    public double Confidence { get; set; }
    
    public double Clarity { get; set; }
    
    public TimeSpan ReadTime { get; set; }
    
    public string ErrorCorrectionLevel { get; set; } = string.Empty;
    
    public bool IsValid => Confidence > 0.8 && Clarity > 0.7;
}

/// <summary>
/// 运动控制命令
/// </summary>
public class MotionCommand
{
    [Required]
    public string CommandId { get; set; } = Guid.NewGuid().ToString();
    
    [Required]
    public MotionType Type { get; set; }
    
    public Point3D? TargetPosition { get; set; }
    
    public double Speed { get; set; }
    
    public double Acceleration { get; set; }
    
    public double Deceleration { get; set; }
    
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 3D坐标点
/// </summary>
public class Point3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double U { get; set; } // 旋转角度

    public Point3D() { }
    
    public Point3D(double x, double y, double z, double u = 0)
    {
        X = x;
        Y = y;
        Z = z;
        U = u;
    }
}

/// <summary>
/// 运动类型
/// </summary>
public enum MotionType
{
    /// <summary>
    /// 绝对位置移动
    /// </summary>
    MoveAbsolute,
    
    /// <summary>
    /// 相对位置移动
    /// </summary>
    MoveRelative,
    
    /// <summary>
    /// 回原点
    /// </summary>
    Home,
    
    /// <summary>
    /// 停止
    /// </summary>
    Stop,
    
    /// <summary>
    /// 暂停
    /// </summary>
    Pause,
    
    /// <summary>
    /// 继续
    /// </summary>
    Resume
}

/// <summary>
/// 视觉检测结果
/// </summary>
public class VisionDetectionResult
{
    public string DetectionId { get; set; } = Guid.NewGuid().ToString();
    
    public List<DetectedObject> Objects { get; set; } = new();
    
    public DateTime DetectionTime { get; set; } = DateTime.UtcNow;
    
    public string ImagePath { get; set; } = string.Empty;
    
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// 检测到的对象
/// </summary>
public class DetectedObject
{
    public string ClassName { get; set; } = string.Empty;
    
    public double Confidence { get; set; }
    
    public BoundingBox BoundingBox { get; set; } = new();
    
    public Point3D? WorldPosition { get; set; }
}

/// <summary>
/// 边界框
/// </summary>
public class BoundingBox
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;
} 