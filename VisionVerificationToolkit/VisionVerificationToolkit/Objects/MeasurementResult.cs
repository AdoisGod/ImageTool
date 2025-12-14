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

/// <summary>
/// 同心度量測結果 (兩圓圓心的距離)
/// </summary>
public class ConcentricityMeasurementResult : MeasurementResult
{
    public PointF Center1 { get; set; }
    public PointF Center2 { get; set; }
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }

    public ConcentricityMeasurementResult(CircleObject circle1, CircleObject circle2, string? name = null)
        : base(MeasurementType.Concentricity, 0, name)
    {
        Center1 = circle1.Center;
        Center2 = circle2.Center;

        double dx = Center2.X - Center1.X;
        double dy = Center2.Y - Center1.Y;
        Value = Math.Sqrt(dx * dx + dy * dy);
        HorizontalOffset = dx;
        VerticalOffset = dy;

        SourceObjects.Add(circle1);
        SourceObjects.Add(circle2);

        AnnotationPoints.Add(Center1);
        AnnotationPoints.Add(Center2);
    }

    public override string GetSummary() => $"同心度: {Value:F3} {Unit} (ΔX={HorizontalOffset:F2}, ΔY={VerticalOffset:F2})";

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["Center1"] = $"({Center1.X:F2}, {Center1.Y:F2})";
        details["Center2"] = $"({Center2.X:F2}, {Center2.Y:F2})";
        details["HorizontalOffset"] = HorizontalOffset;
        details["VerticalOffset"] = VerticalOffset;
        return details;
    }
}

/// <summary>
/// 平行度量測結果 (兩直線的夾角，理想平行應為0度)
/// </summary>
public class ParallelismMeasurementResult : MeasurementResult
{
    public double Line1Angle { get; set; }
    public double Line2Angle { get; set; }
    public double AngleDifference { get; set; }
    public double PerpendicularDistance { get; set; }

    public ParallelismMeasurementResult(LineObject line1, LineObject line2, string? name = null)
        : base(MeasurementType.Parallelism, 0, name)
    {
        Line1Angle = line1.Angle;
        Line2Angle = line2.Angle;

        // 計算角度差（0度表示完全平行）
        double diff = Math.Abs(Line1Angle - Line2Angle);
        if (diff > 90) diff = 180 - diff;
        AngleDifference = diff;
        Value = diff; // 平行度以角度差表示
        Unit = "°";

        // 計算垂直距離 (line1 中點到 line2 的距離)
        var midPoint = new PointF(
            (line1.StartPoint.X + line1.EndPoint.X) / 2,
            (line1.StartPoint.Y + line1.EndPoint.Y) / 2);
        PerpendicularDistance = LineObject.PointToLineDistance(midPoint, line2);

        SourceObjects.Add(line1);
        SourceObjects.Add(line2);

        AnnotationPoints.Add(line1.StartPoint);
        AnnotationPoints.Add(line1.EndPoint);
        AnnotationPoints.Add(line2.StartPoint);
        AnnotationPoints.Add(line2.EndPoint);
    }

    public override string GetSummary() => $"平行度: {Value:F3}° (垂直距離={PerpendicularDistance:F2}px)";

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["Line1Angle"] = $"{Line1Angle:F2}°";
        details["Line2Angle"] = $"{Line2Angle:F2}°";
        details["AngleDifference"] = $"{AngleDifference:F3}°";
        details["PerpendicularDistance"] = $"{PerpendicularDistance:F2} px";
        return details;
    }
}

/// <summary>
/// 垂直度量測結果 (兩直線夾角與90度的偏差)
/// </summary>
public class PerpendicularityMeasurementResult : MeasurementResult
{
    public double Line1Angle { get; set; }
    public double Line2Angle { get; set; }
    public double ActualAngle { get; set; }
    public double DeviationFrom90 { get; set; }

    public PerpendicularityMeasurementResult(LineObject line1, LineObject line2, string? name = null)
        : base(MeasurementType.Perpendicularity, 0, name)
    {
        Line1Angle = line1.Angle;
        Line2Angle = line2.Angle;

        // 計算兩線夾角
        double diff = Math.Abs(Line1Angle - Line2Angle);
        if (diff > 90) diff = 180 - diff;
        ActualAngle = diff;

        // 與90度的偏差（0度表示完全垂直）
        DeviationFrom90 = Math.Abs(90 - diff);
        Value = DeviationFrom90;
        Unit = "°";

        SourceObjects.Add(line1);
        SourceObjects.Add(line2);

        AnnotationPoints.Add(line1.StartPoint);
        AnnotationPoints.Add(line1.EndPoint);
        AnnotationPoints.Add(line2.StartPoint);
        AnnotationPoints.Add(line2.EndPoint);
    }

    public override string GetSummary() => $"垂直度: {Value:F3}° (實際夾角={ActualAngle:F2}°)";

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["Line1Angle"] = $"{Line1Angle:F2}°";
        details["Line2Angle"] = $"{Line2Angle:F2}°";
        details["ActualAngle"] = $"{ActualAngle:F2}°";
        details["DeviationFrom90"] = $"{DeviationFrom90:F3}°";
        return details;
    }
}

/// <summary>
/// 圓度量測結果 (擬合殘差)
/// </summary>
public class CircularityMeasurementResult : MeasurementResult
{
    public double RSquared { get; set; }
    public double Radius { get; set; }
    public PointF Center { get; set; }

    public CircularityMeasurementResult(CircleObject circle, string? name = null)
        : base(MeasurementType.Circularity, circle.RSquared ?? 0, name)
    {
        RSquared = circle.RSquared ?? 0;
        Radius = circle.Radius;
        Center = circle.Center;
        Value = RSquared;
        Unit = "";

        SourceObjects.Add(circle);
        AnnotationPoints.Add(Center);
    }

    public override string GetSummary() => $"圓度 R²: {Value:F4}";

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["R²"] = RSquared;
        details["Radius"] = Radius;
        details["Center"] = $"({Center.X:F2}, {Center.Y:F2})";
        return details;
    }
}

/// <summary>
/// 直線度量測結果 (擬合殘差)
/// </summary>
public class LinearityMeasurementResult : MeasurementResult
{
    public double RSquared { get; set; }
    public double Angle { get; set; }

    public LinearityMeasurementResult(LineObject line, string? name = null)
        : base(MeasurementType.Linearity, line.RSquared ?? 0, name)
    {
        RSquared = line.RSquared ?? 0;
        Angle = line.Angle;
        Value = RSquared;
        Unit = "";

        SourceObjects.Add(line);
        AnnotationPoints.Add(line.StartPoint);
        AnnotationPoints.Add(line.EndPoint);
    }

    public override string GetSummary() => $"直線度 R²: {Value:F4}";

    public override Dictionary<string, object> GetDetails()
    {
        var details = base.GetDetails();
        details["R²"] = RSquared;
        details["Angle"] = $"{Angle:F2}°";
        return details;
    }
}
