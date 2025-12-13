namespace VisionMeasurementToolkit.Core;

/// <summary>
/// 幾何計算輔助類別
/// </summary>
public static class GeometryMath
{
    /// <summary>
    /// 計算兩點距離
    /// </summary>
    public static double Distance(PointF p1, PointF p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// 計算點到直線的距離
    /// 直線由兩點定義
    /// </summary>
    public static double PointToLineDistance(PointF point, PointF lineP1, PointF lineP2)
    {
        double a = lineP2.Y - lineP1.Y;
        double b = lineP1.X - lineP2.X;
        double c = lineP2.X * lineP1.Y - lineP1.X * lineP2.Y;

        return Math.Abs(a * point.X + b * point.Y + c) / Math.Sqrt(a * a + b * b);
    }

    /// <summary>
    /// 計算兩直線的交點
    /// </summary>
    public static PointF? LineIntersection(PointF l1p1, PointF l1p2, PointF l2p1, PointF l2p2)
    {
        double a1 = l1p2.Y - l1p1.Y;
        double b1 = l1p1.X - l1p2.X;
        double c1 = a1 * l1p1.X + b1 * l1p1.Y;

        double a2 = l2p2.Y - l2p1.Y;
        double b2 = l2p1.X - l2p2.X;
        double c2 = a2 * l2p1.X + b2 * l2p1.Y;

        double det = a1 * b2 - a2 * b1;
        if (Math.Abs(det) < 1e-10)
            return null; // 平行

        double x = (b2 * c1 - b1 * c2) / det;
        double y = (a1 * c2 - a2 * c1) / det;

        return new PointF((float)x, (float)y);
    }

    /// <summary>
    /// 計算兩直線的夾角 (度)
    /// </summary>
    public static double AngleBetweenLines(double slope1, double slope2)
    {
        double angle1 = Math.Atan(slope1);
        double angle2 = Math.Atan(slope2);
        double diff = Math.Abs(angle1 - angle2);
        double degrees = diff * 180 / Math.PI;
        return degrees > 90 ? 180 - degrees : degrees;
    }

    /// <summary>
    /// 計算兩直線的夾角 (度) - 使用端點
    /// </summary>
    public static double AngleBetweenLines(PointF l1p1, PointF l1p2, PointF l2p1, PointF l2p2)
    {
        double angle1 = Math.Atan2(l1p2.Y - l1p1.Y, l1p2.X - l1p1.X);
        double angle2 = Math.Atan2(l2p2.Y - l2p1.Y, l2p2.X - l2p1.X);
        double diff = Math.Abs(angle1 - angle2);
        double degrees = diff * 180 / Math.PI;
        if (degrees > 180) degrees = 360 - degrees;
        return degrees > 90 ? 180 - degrees : degrees;
    }

    /// <summary>
    /// 計算直線角度 (相對於水平軸)
    /// </summary>
    public static double LineAngle(PointF p1, PointF p2)
    {
        return Math.Atan2(p2.Y - p1.Y, p2.X - p1.X) * 180 / Math.PI;
    }

    /// <summary>
    /// 計算三點夾角 (頂點在中間)
    /// </summary>
    public static double AngleAtVertex(PointF p1, PointF vertex, PointF p2)
    {
        double v1x = p1.X - vertex.X;
        double v1y = p1.Y - vertex.Y;
        double v2x = p2.X - vertex.X;
        double v2y = p2.Y - vertex.Y;

        double dot = v1x * v2x + v1y * v2y;
        double cross = v1x * v2y - v1y * v2x;

        double angle = Math.Atan2(Math.Abs(cross), dot);
        return angle * 180 / Math.PI;
    }

    /// <summary>
    /// 最小平方法擬合圓
    /// </summary>
    public static (PointF center, double radius, double rSquared) FitCircle(List<PointF> points)
    {
        if (points.Count < 3)
            throw new ArgumentException("至少需要 3 個點");

        int n = points.Count;
        double sumX = 0, sumY = 0, sumX2 = 0, sumY2 = 0;
        double sumXY = 0, sumX3 = 0, sumY3 = 0, sumX2Y = 0, sumXY2 = 0;

        foreach (var p in points)
        {
            double x = p.X, y = p.Y;
            double x2 = x * x, y2 = y * y;
            sumX += x; sumY += y;
            sumX2 += x2; sumY2 += y2;
            sumXY += x * y;
            sumX3 += x * x2; sumY3 += y * y2;
            sumX2Y += x2 * y; sumXY2 += x * y2;
        }

        double A = n * sumX2 - sumX * sumX;
        double B = n * sumXY - sumX * sumY;
        double C = n * sumY2 - sumY * sumY;
        double D = 0.5 * (n * sumX3 + n * sumXY2 - sumX * sumX2 - sumX * sumY2);
        double E = 0.5 * (n * sumX2Y + n * sumY3 - sumY * sumX2 - sumY * sumY2);

        double det = A * C - B * B;
        if (Math.Abs(det) < 1e-10)
            throw new InvalidOperationException("無法擬合圓（點共線）");

        double cx = (D * C - B * E) / det;
        double cy = (A * E - B * D) / det;
        double r = Math.Sqrt((sumX2 + sumY2 - 2 * cx * sumX - 2 * cy * sumY) / n + cx * cx + cy * cy);

        // 計算 R²
        double ssRes = 0, ssTot = 0;
        foreach (var p in points)
        {
            double dist = Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
            ssRes += (dist - r) * (dist - r);
            ssTot += (dist - r) * (dist - r); // 簡化計算
        }
        double rSquared = ssTot > 0 ? 1 - ssRes / (points.Count * r * r * 0.01) : 1;
        rSquared = Math.Min(1, Math.Max(0, rSquared));

        return (new PointF((float)cx, (float)cy), r, rSquared);
    }

    /// <summary>
    /// 最小平方法擬合直線 (y = mx + b)
    /// </summary>
    public static (double slope, double intercept, double rSquared) FitLine(List<PointF> points)
    {
        if (points.Count < 2)
            throw new ArgumentException("至少需要 2 個點");

        int n = points.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;

        foreach (var p in points)
        {
            sumX += p.X;
            sumY += p.Y;
            sumXY += p.X * p.Y;
            sumX2 += p.X * p.X;
            sumY2 += p.Y * p.Y;
        }

        double det = n * sumX2 - sumX * sumX;
        if (Math.Abs(det) < 1e-10)
        {
            // 垂直線
            return (double.PositiveInfinity, sumX / n, 1);
        }

        double slope = (n * sumXY - sumX * sumY) / det;
        double intercept = (sumY - slope * sumX) / n;

        // R²
        double meanY = sumY / n;
        double ssTot = sumY2 - n * meanY * meanY;
        double ssRes = 0;
        foreach (var p in points)
        {
            double predicted = slope * p.X + intercept;
            ssRes += (p.Y - predicted) * (p.Y - predicted);
        }
        double rSquared = ssTot > 0 ? 1 - ssRes / ssTot : 1;

        return (slope, intercept, rSquared);
    }

    /// <summary>
    /// RANSAC 圓擬合
    /// </summary>
    public static (PointF center, double radius, double rSquared, List<PointF> inliers)
        FitCircleRansac(List<PointF> points, double threshold = 2.0, int maxIterations = 100)
    {
        if (points.Count < 3)
            throw new ArgumentException("至少需要 3 個點");

        var random = new Random();
        PointF bestCenter = PointF.Empty;
        double bestRadius = 0;
        List<PointF> bestInliers = new();

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 隨機選取 3 點
            var sample = points.OrderBy(_ => random.Next()).Take(3).ToList();

            try
            {
                var (center, radius, _) = FitCircle(sample);

                // 計算 inliers
                var inliers = points.Where(p =>
                {
                    double dist = Math.Abs(Distance(p, center) - radius);
                    return dist < threshold;
                }).ToList();

                if (inliers.Count > bestInliers.Count)
                {
                    bestCenter = center;
                    bestRadius = radius;
                    bestInliers = inliers;
                }
            }
            catch { }
        }

        // 用所有 inliers 重新擬合
        if (bestInliers.Count >= 3)
        {
            var (center, radius, rSquared) = FitCircle(bestInliers);
            return (center, radius, rSquared, bestInliers);
        }

        return (bestCenter, bestRadius, 0, bestInliers);
    }

    /// <summary>
    /// RANSAC 直線擬合
    /// </summary>
    public static (double slope, double intercept, double rSquared, List<PointF> inliers)
        FitLineRansac(List<PointF> points, double threshold = 2.0, int maxIterations = 100)
    {
        if (points.Count < 2)
            throw new ArgumentException("至少需要 2 個點");

        var random = new Random();
        double bestSlope = 0, bestIntercept = 0;
        List<PointF> bestInliers = new();

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 隨機選取 2 點
            var sample = points.OrderBy(_ => random.Next()).Take(2).ToList();
            var p1 = sample[0];
            var p2 = sample[1];

            if (Math.Abs(p2.X - p1.X) < 1e-10) continue;

            double slope = (p2.Y - p1.Y) / (p2.X - p1.X);
            double intercept = p1.Y - slope * p1.X;

            // 計算 inliers
            var inliers = points.Where(p =>
            {
                double dist = Math.Abs(p.Y - (slope * p.X + intercept)) / Math.Sqrt(1 + slope * slope);
                return dist < threshold;
            }).ToList();

            if (inliers.Count > bestInliers.Count)
            {
                bestSlope = slope;
                bestIntercept = intercept;
                bestInliers = inliers;
            }
        }

        // 用所有 inliers 重新擬合
        if (bestInliers.Count >= 2)
        {
            var (slope, intercept, rSquared) = FitLine(bestInliers);
            return (slope, intercept, rSquared, bestInliers);
        }

        return (bestSlope, bestIntercept, 0, bestInliers);
    }
}
