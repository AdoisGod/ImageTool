using System.Drawing;

namespace VisionVerificationToolkit.Objects;

/// <summary>
/// 直線物件
/// </summary>
public class LineObject : GeometryObjectBase
{
    public override string ObjectType => "Line";

    public PointF StartPoint { get; set; }
    public PointF EndPoint { get; set; }
    public double? RSquared { get; set; }
    public List<PointF> EdgePoints { get; set; } = new();

    public double StartX => StartPoint.X;
    public double StartY => StartPoint.Y;
    public double EndX => EndPoint.X;
    public double EndY => EndPoint.Y;

    public double Length => Math.Sqrt(Math.Pow(EndX - StartX, 2) + Math.Pow(EndY - StartY, 2));
    public double AngleDegrees => Math.Atan2(EndY - StartY, EndX - StartX) * 180.0 / Math.PI;
    public double AngleRadians => Math.Atan2(EndY - StartY, EndX - StartX);
    public double Angle => AngleDegrees; // 別名

    // 直線方程式 ax + by + c = 0
    public double A => EndY - StartY;
    public double B => StartX - EndX;
    public double C => EndX * StartY - StartX * EndY;

    public double Slope => Math.Abs(B) < 1e-10 ? double.PositiveInfinity : -A / B;
    public double Intercept => Math.Abs(B) < 1e-10 ? double.NaN : -C / B;

    public LineObject(PointF start, PointF end, string? name = null) : base(name)
    {
        StartPoint = start;
        EndPoint = end;
        DisplayColor = Color.Yellow;
    }

    public LineObject(double x1, double y1, double x2, double y2, string? name = null)
        : this(new PointF((float)x1, (float)y1), new PointF((float)x2, (float)y2), name) { }

    public override RectangleF GetBoundingBox()
    {
        float minX = Math.Min(StartPoint.X, EndPoint.X);
        float minY = Math.Min(StartPoint.Y, EndPoint.Y);
        float maxX = Math.Max(StartPoint.X, EndPoint.X);
        float maxY = Math.Max(StartPoint.Y, EndPoint.Y);
        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    public override bool HitTest(PointF point, float tolerance = 5f)
    {
        return DistanceToPoint(point) <= tolerance;
    }

    public double DistanceToPoint(PointF point)
    {
        double norm = Math.Sqrt(A * A + B * B);
        if (norm < 1e-10) return double.PositiveInfinity;
        return Math.Abs(A * point.X + B * point.Y + C) / norm;
    }

    public PointF ProjectPoint(PointF point)
    {
        double norm2 = A * A + B * B;
        if (norm2 < 1e-10) return StartPoint;

        double t = -(A * point.X + B * point.Y + C) / norm2;
        return new PointF((float)(point.X + A * t), (float)(point.Y + B * t));
    }

    public override string GetSummary() => $"長度={Length:F2}px, 角度={AngleDegrees:F2}°";

    public override Dictionary<string, object> GetDetails() => new()
    {
        ["StartX"] = StartX,
        ["StartY"] = StartY,
        ["EndX"] = EndX,
        ["EndY"] = EndY,
        ["Length"] = Length,
        ["Angle"] = AngleDegrees,
        ["Slope"] = Slope,
        ["Intercept"] = Intercept,
        ["R²"] = RSquared ?? 0,
        ["EdgePoints"] = EdgePoints.Count
    };

    public static PointF? Intersection(LineObject line1, LineObject line2)
    {
        double det = line1.A * line2.B - line2.A * line1.B;
        if (Math.Abs(det) < 1e-10) return null; // 平行

        double x = (line1.B * line2.C - line2.B * line1.C) / det;
        double y = (line2.A * line1.C - line1.A * line2.C) / det;
        return new PointF((float)x, (float)y);
    }

    public static double AngleBetween(LineObject line1, LineObject line2)
    {
        double angle = Math.Abs(line1.AngleDegrees - line2.AngleDegrees);
        if (angle > 180) angle = 360 - angle;
        if (angle > 90) angle = 180 - angle;
        return angle;
    }

    public static LineObject? ParallelLine(LineObject line, PointF point)
    {
        // 過 point 的平行線
        double newC = -line.A * point.X - line.B * point.Y;
        // 找兩個端點
        if (Math.Abs(line.B) > 1e-10)
        {
            double y1 = (-line.A * (point.X - 50) - newC) / line.B;
            double y2 = (-line.A * (point.X + 50) - newC) / line.B;
            return new LineObject(point.X - 50, y1, point.X + 50, y2);
        }
        else
        {
            return new LineObject(point.X, point.Y - 50, point.X, point.Y + 50);
        }
    }

    public static LineObject? PerpendicularLine(LineObject line, PointF point)
    {
        // 過 point 的垂直線 (交換 A, B 並取負)
        double newA = line.B;
        double newB = -line.A;
        double newC = -newA * point.X - newB * point.Y;

        if (Math.Abs(newB) > 1e-10)
        {
            double y1 = (-newA * (point.X - 50) - newC) / newB;
            double y2 = (-newA * (point.X + 50) - newC) / newB;
            return new LineObject(point.X - 50, y1, point.X + 50, y2);
        }
        else
        {
            return new LineObject(point.X, point.Y - 50, point.X, point.Y + 50);
        }
    }

    /// <summary>
    /// 計算點到直線的距離 (靜態方法)
    /// </summary>
    public static double PointToLineDistance(PointF point, LineObject line)
    {
        return line.DistanceToPoint(point);
    }
}
