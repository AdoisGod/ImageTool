using System.Drawing;

namespace VisionVerificationToolkit.Objects;

/// <summary>
/// 量測結果類型
/// </summary>
public enum MeasurementType
{
    Distance,
    Angle,
    Circularity,
    Linearity,
    Parallelism,
    Perpendicularity,
    Concentricity
}

/// <summary>
/// 量測結果
/// </summary>
public class MeasurementResult : GeometryObjectBase
{
    public override string ObjectType => "Measurement";

    public MeasurementType Type { get; set; }
    public double Value { get; set; }
    public double? ValueMm { get; set; }
    public string Unit { get; set; } = "px";
    public List<IGeometryObject> SourceObjects { get; set; } = new();
    public List<PointF> AnnotationPoints { get; set; } = new();

    public MeasurementResult(MeasurementType type, double value, string? name = null) : base(name)
    {
        Type = type;
        Value = value;
        DisplayColor = Color.Orange;
    }

    public override RectangleF GetBoundingBox()
    {
        if (AnnotationPoints.Count < 2)
            return RectangleF.Empty;

        float minX = AnnotationPoints.Min(p => p.X);
        float minY = AnnotationPoints.Min(p => p.Y);
        float maxX = AnnotationPoints.Max(p => p.X);
        float maxY = AnnotationPoints.Max(p => p.Y);
        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    public override bool HitTest(PointF point, float tolerance = 5f)
    {
        return AnnotationPoints.Any(p =>
        {
            float dx = p.X - point.X;
            float dy = p.Y - point.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
        });
    }

    public override string GetSummary()
    {
        string unit = Type == MeasurementType.Angle ? "°" : Unit;
        return $"{GetTypeString()}: {Value:F3} {unit}";
    }

    private string GetTypeString() => Type switch
    {
        MeasurementType.Distance => "距離",
        MeasurementType.Angle => "角度",
        MeasurementType.Circularity => "圓度",
        MeasurementType.Linearity => "直線度",
        MeasurementType.Parallelism => "平行度",
        MeasurementType.Perpendicularity => "垂直度",
        MeasurementType.Concentricity => "同心度",
        _ => Type.ToString()
    };

    public override Dictionary<string, object> GetDetails()
    {
        var details = new Dictionary<string, object>
        {
            ["Type"] = GetTypeString(),
            ["Value (px)"] = Value,
            ["Unit"] = Unit
        };

        if (ValueMm.HasValue)
        {
            details["Value (mm)"] = ValueMm.Value;
        }

        for (int i = 0; i < SourceObjects.Count; i++)
        {
            details[$"Source {i + 1}"] = SourceObjects[i].Name;
        }

        return details;
    }
}

/// <summary>
/// 距離量測結果
/// </summary>
public class DistanceMeasurementResult : MeasurementResult
{
    public double HorizontalDistance { get; set; }
    public double VerticalDistance { get; set; }
    public PointF Point1 { get; set; }
    public PointF Point2 { get; set; }

    public DistanceMeasurementResult(PointF p1, PointF p2, string? name = null)
        : base(MeasurementType.Distance, 0, name)
    {
        Point1 = p1;
        Point2 = p2;

        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        Value = Math.Sqrt(dx * dx + dy * dy);
        HorizontalDistance = Math.Abs(dx);
        VerticalDistance = Math.Abs(dy);

        AnnotationPoints.Add(p1);
        AnnotationPoints.Add(p2);
    }

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["HorizontalDistance"] = HorizontalDistance;
        details["VerticalDistance"] = VerticalDistance;
        details["Point1"] = $"({Point1.X:F2}, {Point1.Y:F2})";
        details["Point2"] = $"({Point2.X:F2}, {Point2.Y:F2})";
        return details;
    }
}

/// <summary>
/// 角度量測結果
/// </summary>
public class AngleMeasurementResult : MeasurementResult
{
    public PointF Point1 { get; set; }
    public PointF Vertex { get; set; }
    public PointF Point2 { get; set; }
    public double SupplementaryAngle => 180 - Value;

    public AngleMeasurementResult(PointF p1, PointF vertex, PointF p2, string? name = null)
        : base(MeasurementType.Angle, 0, name)
    {
        Point1 = p1;
        Vertex = vertex;
        Point2 = p2;
        Unit = "°";

        // 計算角度
        double angle1 = Math.Atan2(p1.Y - vertex.Y, p1.X - vertex.X);
        double angle2 = Math.Atan2(p2.Y - vertex.Y, p2.X - vertex.X);
        double angle = Math.Abs(angle1 - angle2) * 180 / Math.PI;
        if (angle > 180) angle = 360 - angle;
        Value = angle;

        AnnotationPoints.Add(p1);
        AnnotationPoints.Add(vertex);
        AnnotationPoints.Add(p2);
    }

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["Point1"] = $"({Point1.X:F2}, {Point1.Y:F2})";
        details["Vertex"] = $"({Vertex.X:F2}, {Vertex.Y:F2})";
        details["Point2"] = $"({Point2.X:F2}, {Point2.Y:F2})";
        details["SupplementaryAngle"] = SupplementaryAngle;
        return details;
    }
}
