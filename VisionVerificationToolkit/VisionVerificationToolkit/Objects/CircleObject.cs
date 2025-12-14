using System.Drawing;

namespace VisionVerificationToolkit.Objects;

/// <summary>
/// 圓形物件
/// </summary>
public class CircleObject : GeometryObjectBase
{
    public override string ObjectType => "Circle";

    public PointF Center { get; set; }
    public double Radius { get; set; }
    public double? RSquared { get; set; }
    public List<PointF> EdgePoints { get; set; } = new();

    public double CenterX => Center.X;
    public double CenterY => Center.Y;
    public double Diameter => Radius * 2;
    public double Area => Math.PI * Radius * Radius;
    public double Circumference => 2 * Math.PI * Radius;

    public CircleObject(PointF center, double radius, string? name = null) : base(name)
    {
        Center = center;
        Radius = radius;
        DisplayColor = Color.Lime;
    }

    public CircleObject(double cx, double cy, double radius, string? name = null)
        : this(new PointF((float)cx, (float)cy), radius, name) { }

    public override RectangleF GetBoundingBox()
    {
        float r = (float)Radius;
        return new RectangleF(Center.X - r, Center.Y - r, r * 2, r * 2);
    }

    public override bool HitTest(PointF point, float tolerance = 5f)
    {
        double dist = DistanceFromCenter(point);
        return Math.Abs(dist - Radius) <= tolerance;
    }

    public double DistanceFromCenter(PointF point)
    {
        double dx = point.X - CenterX;
        double dy = point.Y - CenterY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public double DistanceToEdge(PointF point)
    {
        return Math.Abs(DistanceFromCenter(point) - Radius);
    }

    public bool ContainsPoint(PointF point)
    {
        return DistanceFromCenter(point) <= Radius;
    }

    public override string GetSummary() => $"圓心=({CenterX:F2},{CenterY:F2}), R={Radius:F2}px";

    public override Dictionary<string, object> GetDetails() => new()
    {
        ["CenterX"] = CenterX,
        ["CenterY"] = CenterY,
        ["Radius"] = Radius,
        ["Diameter"] = Diameter,
        ["Area"] = Area,
        ["Circumference"] = Circumference,
        ["R²"] = RSquared ?? 0,
        ["EdgePoints"] = EdgePoints.Count
    };

    public static double CenterDistance(CircleObject c1, CircleObject c2)
    {
        double dx = c1.CenterX - c2.CenterX;
        double dy = c1.CenterY - c2.CenterY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double EdgeDistance(CircleObject c1, CircleObject c2)
    {
        double centerDist = CenterDistance(c1, c2);
        return Math.Max(0, centerDist - c1.Radius - c2.Radius);
    }

    public static PointObject ExtractCenter(CircleObject circle)
    {
        return new PointObject(circle.Center, $"{circle.Name}_Center");
    }

    public static LineObject? ConnectCenters(CircleObject c1, CircleObject c2)
    {
        return new LineObject(c1.Center, c2.Center, $"Line_{c1.Name}_{c2.Name}");
    }
}
