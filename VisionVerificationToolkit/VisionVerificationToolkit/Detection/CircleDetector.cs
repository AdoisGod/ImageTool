using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using MathNet.Numerics.LinearAlgebra;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingPointF = System.Drawing.PointF;

namespace VisionVerificationToolkit.Detection;

public enum CircleDetectionMethod
{
    Hough,
    EdgeFitting
}

/// <summary>
/// 圓形檢測器
/// </summary>
public class CircleDetector : DetectorBase
{
    public override string Name => "找圓";
    public override string Description => "檢測圓形物體";

    // 霍夫圓參數
    public double Dp { get; set; } = 1.0;
    public double MinDist { get; set; } = 50;
    public double Param1 { get; set; } = 100;
    public double Param2 { get; set; } = 30;
    public int MinRadius { get; set; } = 10;
    public int MaxRadius { get; set; } = 500;

    // 邊緣擬合參數
    public double CannyThreshold1 { get; set; } = 50;
    public double CannyThreshold2 { get; set; } = 150;
    public int MinFitPoints { get; set; } = 50;
    public bool UseRansac { get; set; } = true;
    public double RansacThreshold { get; set; } = 3.0;

    public CircleDetectionMethod Method { get; set; } = CircleDetectionMethod.Hough;

    public override List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null)
    {
        return Method switch
        {
            CircleDetectionMethod.EdgeFitting => DetectByEdgeFitting(image, roi),
            _ => DetectByHough(image, roi)
        };
    }

    private List<IGeometryObject> DetectByHough(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var blurred = new Mat();
        Cv2.GaussianBlur(roiImage, blurred, new OpenCvSharp.Size(5, 5), 1.5);

        var circles = Cv2.HoughCircles(
            blurred,
            HoughModes.Gradient,
            Dp,
            MinDist,
            Param1,
            Param2,
            MinRadius,
            MaxRadius);

        foreach (var c in circles)
        {
            var center = OffsetPoint(new DrawingPointF(c.Center.X, c.Center.Y), roi);
            var circle = new CircleObject(center, c.Radius);
            results.Add(circle);
        }

        return results;
    }

    private List<IGeometryObject> DetectByEdgeFitting(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var edges = new Mat();
        Cv2.Canny(roiImage, edges, CannyThreshold1, CannyThreshold2);

        // 找邊緣點
        var edgePoints = new List<OpenCvSharp.Point>();
        for (int y = 0; y < edges.Rows; y++)
        {
            for (int x = 0; x < edges.Cols; x++)
            {
                if (edges.At<byte>(y, x) > 0)
                {
                    edgePoints.Add(new OpenCvSharp.Point(x, y));
                }
            }
        }

        if (edgePoints.Count < MinFitPoints)
            return results;

        CircleObject? circle;
        if (UseRansac)
        {
            circle = FitCircleRansac(edgePoints, roi);
        }
        else
        {
            circle = FitCircleLeastSquares(edgePoints, roi);
        }

        if (circle != null)
        {
            circle.EdgePoints = edgePoints.Select(p => OffsetPoint(new DrawingPointF(p.X, p.Y), roi)).ToList();
            results.Add(circle);
        }

        return results;
    }

    private CircleObject? FitCircleLeastSquares(List<OpenCvSharp.Point> points, DrawingRectangleF? roi)
    {
        if (points.Count < 3) return null;

        // 使用代數最小二乘法擬合圓
        // 圓方程: (x-a)² + (y-b)² = r²
        // 展開: x² + y² - 2ax - 2by + (a² + b² - r²) = 0
        // 令 A=-2a, B=-2b, C=a²+b²-r²
        // 則: x² + y² + Ax + By + C = 0

        int n = points.Count;
        var A = Matrix<double>.Build.Dense(n, 3);
        var b = Vector<double>.Build.Dense(n);

        for (int i = 0; i < n; i++)
        {
            double x = points[i].X;
            double y = points[i].Y;
            A[i, 0] = x;
            A[i, 1] = y;
            A[i, 2] = 1;
            b[i] = -(x * x + y * y);
        }

        try
        {
            var solution = A.Solve(b);
            double centerX = -solution[0] / 2;
            double centerY = -solution[1] / 2;
            double radius = Math.Sqrt(centerX * centerX + centerY * centerY - solution[2]);

            if (radius > 0 && radius < 10000)
            {
                var center = OffsetPoint(new DrawingPointF((float)centerX, (float)centerY), roi);

                // 計算 R²
                double ssRes = 0, ssTot = 0;
                double meanDist = points.Average(p =>
                    Math.Sqrt(Math.Pow(p.X - centerX, 2) + Math.Pow(p.Y - centerY, 2)));

                foreach (var p in points)
                {
                    double dist = Math.Sqrt(Math.Pow(p.X - centerX, 2) + Math.Pow(p.Y - centerY, 2));
                    ssRes += Math.Pow(dist - radius, 2);
                    ssTot += Math.Pow(dist - meanDist, 2);
                }

                double rSquared = ssTot > 0 ? 1 - ssRes / ssTot : 0;

                return new CircleObject(center, radius) { RSquared = rSquared };
            }
        }
        catch { }

        return null;
    }

    private CircleObject? FitCircleRansac(List<OpenCvSharp.Point> points, DrawingRectangleF? roi)
    {
        if (points.Count < 3) return null;

        var random = new Random();
        int maxIterations = 500;
        int bestInliers = 0;
        CircleObject? bestCircle = null;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 隨機選3點
            var sample = points.OrderBy(_ => random.Next()).Take(3).ToList();
            var circle = FitCircleFrom3Points(sample[0], sample[1], sample[2], roi);

            if (circle == null) continue;

            // 計算 inliers
            int inliers = 0;
            foreach (var p in points)
            {
                double dist = Math.Abs(Math.Sqrt(Math.Pow(p.X - (circle.CenterX - (roi?.X ?? 0)), 2) +
                                                  Math.Pow(p.Y - (circle.CenterY - (roi?.Y ?? 0)), 2)) - circle.Radius);
                if (dist < RansacThreshold)
                    inliers++;
            }

            if (inliers > bestInliers)
            {
                bestInliers = inliers;
                bestCircle = circle;
            }
        }

        // 使用 inliers 重新擬合
        if (bestCircle != null && bestInliers > MinFitPoints)
        {
            var inlierPoints = points.Where(p =>
            {
                double dist = Math.Abs(Math.Sqrt(Math.Pow(p.X - (bestCircle.CenterX - (roi?.X ?? 0)), 2) +
                                                  Math.Pow(p.Y - (bestCircle.CenterY - (roi?.Y ?? 0)), 2)) - bestCircle.Radius);
                return dist < RansacThreshold;
            }).ToList();

            bestCircle = FitCircleLeastSquares(inlierPoints, roi);
        }

        return bestCircle;
    }

    private CircleObject? FitCircleFrom3Points(OpenCvSharp.Point p1, OpenCvSharp.Point p2, OpenCvSharp.Point p3, DrawingRectangleF? roi)
    {
        double ax = p1.X, ay = p1.Y;
        double bx = p2.X, by = p2.Y;
        double cx = p3.X, cy = p3.Y;

        double d = 2 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));
        if (Math.Abs(d) < 1e-10) return null;

        double ux = ((ax * ax + ay * ay) * (by - cy) + (bx * bx + by * by) * (cy - ay) + (cx * cx + cy * cy) * (ay - by)) / d;
        double uy = ((ax * ax + ay * ay) * (cx - bx) + (bx * bx + by * by) * (ax - cx) + (cx * cx + cy * cy) * (bx - ax)) / d;

        double radius = Math.Sqrt(Math.Pow(ax - ux, 2) + Math.Pow(ay - uy, 2));

        if (radius > 0 && radius < 10000)
        {
            var center = OffsetPoint(new DrawingPointF((float)ux, (float)uy), roi);
            return new CircleObject(center, radius);
        }

        return null;
    }
}
