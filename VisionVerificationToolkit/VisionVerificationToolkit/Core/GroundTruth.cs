using System.Drawing;
using VisionVerificationToolkit.Objects;

namespace VisionVerificationToolkit.Core;

/// <summary>
/// Ground Truth 標記類型
/// </summary>
public enum GroundTruthType
{
    Point,
    Circle,
    Line
}

/// <summary>
/// Ground Truth 標記項目
/// </summary>
public class GroundTruthItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public GroundTruthType Type { get; set; }

    // Point 類型
    public PointF? Position { get; set; }

    // Circle 類型
    public PointF? Center { get; set; }
    public double? Radius { get; set; }

    // Line 類型
    public PointF? StartPoint { get; set; }
    public PointF? EndPoint { get; set; }

    /// <summary>
    /// 計算與檢測物件的位置誤差
    /// </summary>
    public double CalculateError(IGeometryObject obj)
    {
        return Type switch
        {
            GroundTruthType.Point => CalculatePointError(obj),
            GroundTruthType.Circle => CalculateCircleError(obj),
            GroundTruthType.Line => CalculateLineError(obj),
            _ => double.MaxValue
        };
    }

    private double CalculatePointError(IGeometryObject obj)
    {
        if (Position == null) return double.MaxValue;

        if (obj is PointObject point)
        {
            double dx = point.X - Position.Value.X;
            double dy = point.Y - Position.Value.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        else if (obj is CircleObject circle)
        {
            double dx = circle.CenterX - Position.Value.X;
            double dy = circle.CenterY - Position.Value.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        return double.MaxValue;
    }

    private double CalculateCircleError(IGeometryObject obj)
    {
        if (Center == null || Radius == null) return double.MaxValue;

        if (obj is CircleObject circle)
        {
            // 計算圓心距離 + 半徑差
            double dx = circle.CenterX - Center.Value.X;
            double dy = circle.CenterY - Center.Value.Y;
            double centerDist = Math.Sqrt(dx * dx + dy * dy);
            double radiusDiff = Math.Abs(circle.Radius - Radius.Value);
            return centerDist + radiusDiff;
        }

        return double.MaxValue;
    }

    private double CalculateLineError(IGeometryObject obj)
    {
        if (StartPoint == null || EndPoint == null) return double.MaxValue;

        if (obj is LineObject line)
        {
            // 計算端點距離的平均值
            double d1 = Distance(line.StartPoint, StartPoint.Value) + Distance(line.EndPoint, EndPoint.Value);
            double d2 = Distance(line.StartPoint, EndPoint.Value) + Distance(line.EndPoint, StartPoint.Value);
            return Math.Min(d1, d2) / 2;
        }

        return double.MaxValue;
    }

    private static double Distance(PointF p1, PointF p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString()
    {
        return Type switch
        {
            GroundTruthType.Point => $"Point ({Position?.X:F1}, {Position?.Y:F1})",
            GroundTruthType.Circle => $"Circle ({Center?.X:F1}, {Center?.Y:F1}) R={Radius:F1}",
            GroundTruthType.Line => $"Line ({StartPoint?.X:F1}, {StartPoint?.Y:F1}) - ({EndPoint?.X:F1}, {EndPoint?.Y:F1})",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Ground Truth 管理器
/// </summary>
public class GroundTruthManager
{
    private readonly List<GroundTruthItem> _items = new();

    public IReadOnlyList<GroundTruthItem> Items => _items;

    public int Count => _items.Count;

    public bool HasGroundTruth => _items.Count > 0;

    public void Add(GroundTruthItem item)
    {
        _items.Add(item);
    }

    public void AddPoint(PointF position)
    {
        _items.Add(new GroundTruthItem
        {
            Type = GroundTruthType.Point,
            Position = position
        });
    }

    public void AddCircle(PointF center, double radius)
    {
        _items.Add(new GroundTruthItem
        {
            Type = GroundTruthType.Circle,
            Center = center,
            Radius = radius
        });
    }

    public void AddLine(PointF start, PointF end)
    {
        _items.Add(new GroundTruthItem
        {
            Type = GroundTruthType.Line,
            StartPoint = start,
            EndPoint = end
        });
    }

    public void Remove(GroundTruthItem item)
    {
        _items.Remove(item);
    }

    public void Clear()
    {
        _items.Clear();
    }

    /// <summary>
    /// 評估檢測結果與 Ground Truth 的符合程度
    /// </summary>
    /// <param name="detectedObjects">檢測到的物件</param>
    /// <param name="tolerance">容許誤差（像素）</param>
    /// <returns>評分 (0-1)，1表示完全符合</returns>
    public double EvaluatePositionMatch(IEnumerable<IGeometryObject> detectedObjects, double tolerance = 10.0)
    {
        if (_items.Count == 0) return 0;

        var objects = detectedObjects.ToList();
        if (objects.Count == 0) return 0;

        double totalScore = 0;
        var usedObjects = new HashSet<IGeometryObject>();

        foreach (var gt in _items)
        {
            double bestError = double.MaxValue;
            IGeometryObject? bestMatch = null;

            foreach (var obj in objects)
            {
                if (usedObjects.Contains(obj)) continue;

                double error = gt.CalculateError(obj);
                if (error < bestError)
                {
                    bestError = error;
                    bestMatch = obj;
                }
            }

            if (bestMatch != null && bestError < tolerance * 3)
            {
                usedObjects.Add(bestMatch);
                // 將誤差轉換為分數 (誤差越小分數越高)
                double itemScore = 1.0 - Math.Min(bestError / tolerance, 1.0);
                totalScore += itemScore;
            }
        }

        // 檢查是否有多餘的檢測結果（可能是誤檢）
        int extraDetections = objects.Count - usedObjects.Count;
        double penalty = extraDetections * 0.1; // 每個多餘檢測扣 10%

        double finalScore = (totalScore / _items.Count) - penalty;
        return Math.Max(0, Math.Min(1, finalScore));
    }

    /// <summary>
    /// 取得詳細的匹配報告
    /// </summary>
    public List<(GroundTruthItem GT, IGeometryObject? Match, double Error)> GetMatchReport(
        IEnumerable<IGeometryObject> detectedObjects)
    {
        var result = new List<(GroundTruthItem, IGeometryObject?, double)>();
        var objects = detectedObjects.ToList();
        var usedObjects = new HashSet<IGeometryObject>();

        foreach (var gt in _items)
        {
            double bestError = double.MaxValue;
            IGeometryObject? bestMatch = null;

            foreach (var obj in objects)
            {
                if (usedObjects.Contains(obj)) continue;

                double error = gt.CalculateError(obj);
                if (error < bestError)
                {
                    bestError = error;
                    bestMatch = obj;
                }
            }

            if (bestMatch != null)
            {
                usedObjects.Add(bestMatch);
                result.Add((gt, bestMatch, bestError));
            }
            else
            {
                result.Add((gt, null, double.MaxValue));
            }
        }

        return result;
    }
}
