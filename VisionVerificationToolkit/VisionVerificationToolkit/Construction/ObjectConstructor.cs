using VisionVerificationToolkit.Objects;
using System.Drawing;

namespace VisionVerificationToolkit.Construction;

/// <summary>
/// 虛擬物件建構器
/// </summary>
public static class ObjectConstructor
{
    /// <summary>
    /// 計算兩線交點
    /// </summary>
    public static PointObject? LineIntersection(LineObject line1, LineObject line2)
    {
        var intersection = LineObject.Intersection(line1, line2);
        if (intersection == null) return null;

        return new PointObject(intersection.Value, $"交點_{line1.Name}_{line2.Name}")
        {
            DisplayColor = Color.Magenta
        };
    }

    /// <summary>
    /// 建立平行線 (過指定點)
    /// </summary>
    public static LineObject? ParallelLine(LineObject line, PointObject point)
    {
        var result = LineObject.ParallelLine(line, point.Position);
        if (result != null)
        {
            result.Name = $"平行線_{line.Name}";
            result.DisplayColor = Color.Cyan;
        }
        return result;
    }

    /// <summary>
    /// 建立平行線 (指定距離)
    /// </summary>
    public static LineObject? ParallelLineAtDistance(LineObject line, double distance, bool above = true)
    {
        // 計算法向量方向
        double dx = line.EndPoint.X - line.StartPoint.X;
        double dy = line.EndPoint.Y - line.StartPoint.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-10) return null;

        // 垂直方向單位向量
        double nx = -dy / len;
        double ny = dx / len;
        if (!above) { nx = -nx; ny = -ny; }

        // 平移起終點
        var newStart = new PointF(
            (float)(line.StartPoint.X + nx * distance),
            (float)(line.StartPoint.Y + ny * distance));
        var newEnd = new PointF(
            (float)(line.EndPoint.X + nx * distance),
            (float)(line.EndPoint.Y + ny * distance));

        return new LineObject(newStart, newEnd, $"平行線_{line.Name}_{distance:F1}")
        {
            DisplayColor = Color.Cyan
        };
    }

    /// <summary>
    /// 建立垂直線 (過指定點)
    /// </summary>
    public static LineObject? PerpendicularLine(LineObject line, PointObject point)
    {
        var result = LineObject.PerpendicularLine(line, point.Position);
        if (result != null)
        {
            result.Name = $"垂直線_{line.Name}";
            result.DisplayColor = Color.Orange;
        }
        return result;
    }

    /// <summary>
    /// 連接兩圓心
    /// </summary>
    public static LineObject ConnectCircleCenters(CircleObject circle1, CircleObject circle2)
    {
        return new LineObject(circle1.Center, circle2.Center, $"圓心連線_{circle1.Name}_{circle2.Name}")
        {
            DisplayColor = Color.LightBlue
        };
    }

    /// <summary>
    /// 提取圓心為點
    /// </summary>
    public static PointObject ExtractCircleCenter(CircleObject circle)
    {
        return new PointObject(circle.Center, $"圓心_{circle.Name}")
        {
            DisplayColor = Color.Red
        };
    }

    /// <summary>
    /// 計算兩點中點
    /// </summary>
    public static PointObject Midpoint(PointObject point1, PointObject point2)
    {
        var mid = new PointF(
            (point1.Position.X + point2.Position.X) / 2,
            (point1.Position.Y + point2.Position.Y) / 2);

        return new PointObject(mid, $"中點_{point1.Name}_{point2.Name}")
        {
            DisplayColor = Color.Green
        };
    }

    /// <summary>
    /// 連接兩點成線
    /// </summary>
    public static LineObject ConnectPoints(PointObject point1, PointObject point2)
    {
        return new LineObject(point1.Position, point2.Position, $"連線_{point1.Name}_{point2.Name}")
        {
            DisplayColor = Color.Yellow
        };
    }

    /// <summary>
    /// 計算切線 (從點到圓)
    /// </summary>
    public static List<LineObject> TangentLines(CircleObject circle, PointObject point)
    {
        var results = new List<LineObject>();

        double dx = point.Position.X - circle.CenterX;
        double dy = point.Position.Y - circle.CenterY;
        double dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist <= circle.Radius) return results; // 點在圓內，無切線

        // 計算切點角度
        double angle = Math.Acos(circle.Radius / dist);
        double baseAngle = Math.Atan2(dy, dx);

        // 兩個切點
        double angle1 = baseAngle + angle;
        double angle2 = baseAngle - angle;

        var tangent1 = new PointF(
            (float)(circle.CenterX + circle.Radius * Math.Cos(angle1)),
            (float)(circle.CenterY + circle.Radius * Math.Sin(angle1)));
        var tangent2 = new PointF(
            (float)(circle.CenterX + circle.Radius * Math.Cos(angle2)),
            (float)(circle.CenterY + circle.Radius * Math.Sin(angle2)));

        results.Add(new LineObject(point.Position, tangent1, $"切線1_{circle.Name}")
        {
            DisplayColor = Color.Pink
        });
        results.Add(new LineObject(point.Position, tangent2, $"切線2_{circle.Name}")
        {
            DisplayColor = Color.Pink
        });

        return results;
    }

    /// <summary>
    /// 計算圓與線的交點
    /// </summary>
    public static List<PointObject> CircleLineIntersection(CircleObject circle, LineObject line)
    {
        var results = new List<PointObject>();

        // 直線方程: ax + by + c = 0
        double a = line.A;
        double b = line.B;
        double c = line.C;
        double r = circle.Radius;
        double cx = circle.CenterX;
        double cy = circle.CenterY;

        // 圓心到直線的距離
        double dist = Math.Abs(a * cx + b * cy + c) / Math.Sqrt(a * a + b * b);

        if (dist > r) return results; // 無交點

        // 計算交點
        double norm2 = a * a + b * b;
        double t = -(a * cx + b * cy + c) / norm2;
        double footX = cx + a * t;
        double footY = cy + b * t;

        if (Math.Abs(dist - r) < 1e-6)
        {
            // 相切，一個交點
            results.Add(new PointObject(new PointF((float)footX, (float)footY), $"切點_{circle.Name}_{line.Name}")
            {
                DisplayColor = Color.Orange
            });
        }
        else
        {
            // 相交，兩個交點
            double half = Math.Sqrt(r * r - dist * dist);
            double ux = -b / Math.Sqrt(norm2);
            double uy = a / Math.Sqrt(norm2);

            results.Add(new PointObject(new PointF((float)(footX + ux * half), (float)(footY + uy * half)), $"交點1_{circle.Name}_{line.Name}")
            {
                DisplayColor = Color.Orange
            });
            results.Add(new PointObject(new PointF((float)(footX - ux * half), (float)(footY - uy * half)), $"交點2_{circle.Name}_{line.Name}")
            {
                DisplayColor = Color.Orange
            });
        }

        return results;
    }
}
