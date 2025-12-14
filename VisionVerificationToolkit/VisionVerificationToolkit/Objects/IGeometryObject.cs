using System.Drawing;

namespace VisionVerificationToolkit.Objects;

/// <summary>
/// 幾何物件介面
/// </summary>
public interface IGeometryObject
{
    /// <summary>物件ID</summary>
    string Id { get; }

    /// <summary>物件名稱</summary>
    string Name { get; set; }

    /// <summary>物件類型</summary>
    string ObjectType { get; }

    /// <summary>建立時間</summary>
    DateTime CreatedAt { get; }

    /// <summary>是否可見</summary>
    bool IsVisible { get; set; }

    /// <summary>是否選中</summary>
    bool IsSelected { get; set; }

    /// <summary>顯示顏色</summary>
    Color DisplayColor { get; set; }

    /// <summary>取得邊界框</summary>
    RectangleF GetBoundingBox();

    /// <summary>檢查點是否在物件上</summary>
    bool HitTest(PointF point, float tolerance = 5f);

    /// <summary>取得摘要描述</summary>
    string GetSummary();

    /// <summary>取得詳細資訊</summary>
    Dictionary<string, object> GetDetails();
}

/// <summary>
/// 幾何物件基底類別
/// </summary>
public abstract class GeometryObjectBase : IGeometryObject
{
    private static int _idCounter = 0;

    public string Id { get; }
    public string Name { get; set; }
    public abstract string ObjectType { get; }
    public DateTime CreatedAt { get; }
    public bool IsVisible { get; set; } = true;
    public bool IsSelected { get; set; }
    public Color DisplayColor { get; set; } = Color.Lime;

    protected GeometryObjectBase(string? name = null)
    {
        Id = $"{ObjectType}_{++_idCounter:D3}";
        Name = name ?? Id;
        CreatedAt = DateTime.Now;
    }

    public abstract RectangleF GetBoundingBox();
    public abstract bool HitTest(PointF point, float tolerance = 5f);
    public abstract string GetSummary();
    public abstract Dictionary<string, object> GetDetails();

    public static void ResetIdCounter() => _idCounter = 0;
}
