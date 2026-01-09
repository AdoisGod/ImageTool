using System.Drawing;

namespace VisionVerificationToolkit.Objects;

/// <summary>
/// 點物件
/// </summary>
public class PointObject : GeometryObjectBase
{
    public override string ObjectType => "Point";

    public PointF Position { get; set; }
    public double Quality { get; set; } = 1.0;
    public string Source { get; set; } = "Manual";

    public double X => Position.X;
    public double Y => Position.Y;

    public PointObject(PointF position, string? name = null) : base(name)
    {
        Position = position;
        DisplayColor = Color.Cyan;
    }

    public PointObject(double x, double y, string? name = null) : this(new PointF((float)x, (float)y), name) { }

    public override RectangleF GetBoundingBox()
    {
        const float size = 10;
        return new RectangleF(Position.X - size / 2, Position.Y - size / 2, size, size);
    }

    public override bool HitTest(PointF point, float tolerance = 5f)
    {
        float dx = point.X - Position.X;
        float dy = point.Y - Position.Y;
        return Math.Sqrt(dx * dx + dy * dy) <= tolerance;
    }

    public override string GetSummary() => $"({X:F2}, {Y:F2})";

    public override Dictionary<string, object> GetDetails() => new()
    {
        ["X"] = X,
        ["Y"] = Y,
        ["Quality"] = Quality,
        ["Source"] = Source
    };

    public double DistanceTo(PointObject other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static PointObject Midpoint(PointObject p1, PointObject p2)
    {
        return new PointObject((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
    }
}
