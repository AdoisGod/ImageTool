namespace VisionMeasurementToolkit.Core;

/// <summary>
/// 量測結果基底類別
/// </summary>
public abstract class MeasurementResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public abstract string ResultType { get; }
    public abstract string GetSummary(double resolution);
    public abstract void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected);
}

/// <summary>
/// 圓形量測結果
/// </summary>
public class CircleResult : MeasurementResult
{
    public PointF Center { get; set; }
    public double RadiusPixels { get; set; }
    public double? RSquared { get; set; }

    public override string ResultType => "圓";
    public double DiameterPixels => RadiusPixels * 2;
    public double RadiusMm(double res) => RadiusPixels * res;
    public double DiameterMm(double res) => DiameterPixels * res;

    public override string GetSummary(double resolution)
    {
        return $"圓心:({Center.X:F1},{Center.Y:F1}) 直徑:{DiameterPixels:F2}px ({DiameterMm(resolution):F3}mm)";
    }

    public override void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected)
    {
        var center = toScreen(Center);
        var edgePoint = toScreen(new PointF(Center.X + (float)RadiusPixels, Center.Y));
        float screenRadius = Math.Abs(edgePoint.X - center.X);

        using var pen = new Pen(isSelected ? Color.Yellow : Color.Lime, isSelected ? 3 : 2);
        g.DrawEllipse(pen, center.X - screenRadius, center.Y - screenRadius, screenRadius * 2, screenRadius * 2);

        // 圓心十字
        float crossSize = 10;
        g.DrawLine(pen, center.X - crossSize, center.Y, center.X + crossSize, center.Y);
        g.DrawLine(pen, center.X, center.Y - crossSize, center.X, center.Y + crossSize);

        // 直徑標註
        using var font = new Font("Consolas", 9);
        using var brush = new SolidBrush(Color.Yellow);
        g.DrawString($"D={DiameterPixels:F1}px", font, brush, center.X + 5, center.Y - screenRadius - 20);
    }
}

/// <summary>
/// 直線量測結果
/// </summary>
public class LineResult : MeasurementResult
{
    public PointF StartPoint { get; set; }
    public PointF EndPoint { get; set; }
    public double Slope { get; set; }
    public double Intercept { get; set; }
    public double AngleDegrees { get; set; }
    public double LengthPixels { get; set; }
    public double? RSquared { get; set; }
    public List<PointF> EdgePoints { get; set; } = new();

    public override string ResultType => "線";

    public double LengthMm(double res) => LengthPixels * res;

    public override string GetSummary(double resolution)
    {
        return $"角度:{AngleDegrees:F2}° 長度:{LengthPixels:F1}px ({LengthMm(resolution):F3}mm)";
    }

    public override void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected)
    {
        var p1 = toScreen(StartPoint);
        var p2 = toScreen(EndPoint);

        using var pen = new Pen(isSelected ? Color.Yellow : Color.DodgerBlue, isSelected ? 3 : 2);
        g.DrawLine(pen, p1, p2);

        // 繪製邊緣點
        using var pointBrush = new SolidBrush(Color.Red);
        foreach (var pt in EdgePoints)
        {
            var sp = toScreen(pt);
            g.FillEllipse(pointBrush, sp.X - 3, sp.Y - 3, 6, 6);
        }

        // 標註
        using var font = new Font("Consolas", 9);
        using var brush = new SolidBrush(Color.Yellow);
        var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        g.DrawString($"{AngleDegrees:F1}°", font, brush, mid.X + 5, mid.Y - 15);
    }
}

/// <summary>
/// 點到點量測結果
/// </summary>
public class PointToPointResult : MeasurementResult
{
    public PointF Point1 { get; set; }
    public PointF Point2 { get; set; }
    public double HorizontalDistance { get; set; }
    public double VerticalDistance { get; set; }
    public double Distance { get; set; }
    public double AngleDegrees { get; set; }

    public override string ResultType => "距離";

    public override string GetSummary(double resolution)
    {
        return $"距離:{Distance:F2}px ({Distance * resolution:F3}mm) 角度:{AngleDegrees:F2}°";
    }

    public override void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected)
    {
        var p1 = toScreen(Point1);
        var p2 = toScreen(Point2);

        using var pen = new Pen(isSelected ? Color.Yellow : Color.Orange, isSelected ? 3 : 2);
        g.DrawLine(pen, p1, p2);

        // 端點
        using var brush = new SolidBrush(Color.Red);
        g.FillEllipse(brush, p1.X - 5, p1.Y - 5, 10, 10);
        g.FillEllipse(brush, p2.X - 5, p2.Y - 5, 10, 10);

        // 標註
        using var font = new Font("Consolas", 9);
        using var textBrush = new SolidBrush(Color.Yellow);
        var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
        g.DrawString($"{Distance:F1}px", font, textBrush, mid.X + 5, mid.Y - 15);
    }
}

/// <summary>
/// 角度量測結果
/// </summary>
public class AngleResult : MeasurementResult
{
    public PointF Vertex { get; set; }
    public PointF Point1 { get; set; }
    public PointF Point2 { get; set; }
    public double Angle { get; set; }
    public double SupplementaryAngle => 180 - Angle;

    public override string ResultType => "角度";

    public override string GetSummary(double resolution)
    {
        return $"夾角:{Angle:F2}° 補角:{SupplementaryAngle:F2}°";
    }

    public override void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected)
    {
        var v = toScreen(Vertex);
        var p1 = toScreen(Point1);
        var p2 = toScreen(Point2);

        using var pen = new Pen(isSelected ? Color.Yellow : Color.Magenta, isSelected ? 3 : 2);
        g.DrawLine(pen, v, p1);
        g.DrawLine(pen, v, p2);

        // 角度弧
        float arcRadius = 30;
        float startAngle = (float)(Math.Atan2(p1.Y - v.Y, p1.X - v.X) * 180 / Math.PI);
        float sweepAngle = (float)Angle;
        g.DrawArc(pen, v.X - arcRadius, v.Y - arcRadius, arcRadius * 2, arcRadius * 2, startAngle, sweepAngle);

        // 標註
        using var font = new Font("Consolas", 9);
        using var brush = new SolidBrush(Color.Yellow);
        g.DrawString($"{Angle:F1}°", font, brush, v.X + 15, v.Y - 25);
    }
}

/// <summary>
/// 線到線距離結果
/// </summary>
public class LineToLineResult : MeasurementResult
{
    public LineResult Line1 { get; set; } = null!;
    public LineResult Line2 { get; set; } = null!;
    public bool IsParallel { get; set; }
    public double AngleBetween { get; set; }
    public double? PerpendicularDistance { get; set; }
    public PointF? IntersectionPoint { get; set; }

    public override string ResultType => "線距";

    public override string GetSummary(double resolution)
    {
        if (IsParallel && PerpendicularDistance.HasValue)
            return $"平行 距離:{PerpendicularDistance.Value:F2}px ({PerpendicularDistance.Value * resolution:F3}mm)";
        if (IntersectionPoint.HasValue)
            return $"相交 夾角:{AngleBetween:F2}° 交點:({IntersectionPoint.Value.X:F1},{IntersectionPoint.Value.Y:F1})";
        return "計算失敗";
    }

    public override void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected)
    {
        Line1.Draw(g, toScreen, isSelected);
        Line2.Draw(g, toScreen, isSelected);

        if (IntersectionPoint.HasValue)
        {
            var pt = toScreen(IntersectionPoint.Value);
            using var brush = new SolidBrush(Color.Red);
            g.FillEllipse(brush, pt.X - 6, pt.Y - 6, 12, 12);
        }
    }
}
