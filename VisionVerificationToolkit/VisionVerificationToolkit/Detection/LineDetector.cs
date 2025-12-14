using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using MathNet.Numerics.LinearAlgebra;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingPointF = System.Drawing.PointF;

namespace VisionVerificationToolkit.Detection;

public enum LineDetectionMethod
{
    Caliper,
    Hough,
    EdgeFitting
}

public enum EdgePolarity
{
    DarkToLight,
    LightToDark,
    Both
}

/// <summary>
/// 直線檢測器
/// </summary>
public class LineDetector : DetectorBase
{
    public override string Name => "找線";
    public override string Description => "檢測直線邊緣";

    public LineDetectionMethod Method { get; set; } = LineDetectionMethod.Caliper;

    // 卡尺法參數
    public int CaliperCount { get; set; } = 20;
    public int ProfileLength { get; set; } = 30;
    public int EdgeThreshold { get; set; } = 30;
    public EdgePolarity Polarity { get; set; } = EdgePolarity.Both;

    // 霍夫線參數
    public double HoughThreshold { get; set; } = 50;
    public double MinLineLength { get; set; } = 50;
    public double MaxLineGap { get; set; } = 10;

    // 搜尋線 (由使用者繪製)
    public DrawingPointF SearchStart { get; set; }
    public DrawingPointF SearchEnd { get; set; }

    public override List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null)
    {
        return Method switch
        {
            LineDetectionMethod.Hough => DetectByHough(image, roi),
            LineDetectionMethod.EdgeFitting => DetectByEdgeFitting(image, roi),
            _ => DetectByCaliper(image, roi)
        };
    }

    private List<IGeometryObject> DetectByCaliper(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        // 計算搜尋線的方向和垂直方向
        double dx = SearchEnd.X - SearchStart.X;
        double dy = SearchEnd.Y - SearchStart.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1) return results;

        // 單位向量
        double ux = dx / length;
        double uy = dy / length;

        // 垂直方向
        double vx = -uy;
        double vy = ux;

        var edgePoints = new List<DrawingPointF>();

        for (int i = 0; i < CaliperCount; i++)
        {
            // 計算卡尺中心點
            double t = (double)i / (CaliperCount - 1);
            double cx = SearchStart.X + dx * t;
            double cy = SearchStart.Y + dy * t;

            // 沿垂直方向提取剖面
            var profile = new List<double>();
            var positions = new List<DrawingPointF>();

            for (int j = -ProfileLength / 2; j <= ProfileLength / 2; j++)
            {
                int px = (int)(cx + vx * j);
                int py = (int)(cy + vy * j);

                if (px >= 0 && px < image.Cols && py >= 0 && py < image.Rows)
                {
                    profile.Add(image.At<byte>(py, px));
                    positions.Add(new DrawingPointF(px, py));
                }
            }

            // 計算梯度找邊緣
            var edgePoint = FindEdgeInProfile(profile, positions);
            if (edgePoint.HasValue)
            {
                edgePoints.Add(edgePoint.Value);
            }
        }

        // 擬合直線
        if (edgePoints.Count >= 3)
        {
            var line = FitLineLeastSquares(edgePoints);
            if (line != null)
            {
                line.EdgePoints = edgePoints;
                results.Add(line);
            }
        }

        return results;
    }

    private DrawingPointF? FindEdgeInProfile(List<double> profile, List<DrawingPointF> positions)
    {
        if (profile.Count < 3) return null;

        // 計算梯度
        var gradients = new List<double>();
        for (int i = 1; i < profile.Count - 1; i++)
        {
            double grad = (profile[i + 1] - profile[i - 1]) / 2;
            gradients.Add(grad);
        }

        // 根據極性找邊緣
        int bestIdx = -1;
        double bestValue = 0;

        for (int i = 0; i < gradients.Count; i++)
        {
            double grad = gradients[i];
            bool valid = Polarity switch
            {
                EdgePolarity.DarkToLight => grad > EdgeThreshold,
                EdgePolarity.LightToDark => grad < -EdgeThreshold,
                _ => Math.Abs(grad) > EdgeThreshold
            };

            if (valid && Math.Abs(grad) > Math.Abs(bestValue))
            {
                bestValue = grad;
                bestIdx = i + 1; // 因為梯度是從 index 1 開始
            }
        }

        if (bestIdx >= 0 && bestIdx < positions.Count)
        {
            // 亞像素精度 (拋物線擬合)
            if (bestIdx > 0 && bestIdx < gradients.Count - 1)
            {
                double g0 = Math.Abs(gradients[bestIdx - 1]);
                double g1 = Math.Abs(gradients[bestIdx]);
                double g2 = Math.Abs(gradients[bestIdx + 1]);

                double offset = (g0 - g2) / (2 * (g0 - 2 * g1 + g2));
                if (Math.Abs(offset) < 1)
                {
                    float x = positions[bestIdx].X + (positions[bestIdx + 1].X - positions[bestIdx - 1].X) / 2 * (float)offset;
                    float y = positions[bestIdx].Y + (positions[bestIdx + 1].Y - positions[bestIdx - 1].Y) / 2 * (float)offset;
                    return new DrawingPointF(x, y);
                }
            }

            return positions[bestIdx];
        }

        return null;
    }

    private LineObject? FitLineLeastSquares(List<DrawingPointF> points)
    {
        if (points.Count < 2) return null;

        // 使用 PCA 找主方向
        double meanX = points.Average(p => p.X);
        double meanY = points.Average(p => p.Y);

        double cxx = 0, cxy = 0, cyy = 0;
        foreach (var p in points)
        {
            double dx = p.X - meanX;
            double dy = p.Y - meanY;
            cxx += dx * dx;
            cxy += dx * dy;
            cyy += dy * dy;
        }

        // 特徵值分解找主方向
        double trace = cxx + cyy;
        double det = cxx * cyy - cxy * cxy;
        double eigenValue1 = trace / 2 + Math.Sqrt(trace * trace / 4 - det);

        double vx, vy;
        if (Math.Abs(cxy) > 1e-10)
        {
            vx = eigenValue1 - cyy;
            vy = cxy;
        }
        else
        {
            vx = 1;
            vy = 0;
        }

        double norm = Math.Sqrt(vx * vx + vy * vy);
        vx /= norm;
        vy /= norm;

        // 計算線段端點 (投影到主方向)
        double minT = double.MaxValue, maxT = double.MinValue;

        foreach (var p in points)
        {
            double t = (p.X - meanX) * vx + (p.Y - meanY) * vy;
            minT = Math.Min(minT, t);
            maxT = Math.Max(maxT, t);
        }

        var start = new DrawingPointF((float)(meanX + vx * minT), (float)(meanY + vy * minT));
        var end = new DrawingPointF((float)(meanX + vx * maxT), (float)(meanY + vy * maxT));

        var line = new LineObject(start, end);

        // 計算 R²
        double ssRes = 0, ssTot = 0;
        foreach (var p in points)
        {
            double dist = line.DistanceToPoint(p);
            ssRes += dist * dist;

            double distFromMean = Math.Sqrt(Math.Pow(p.X - meanX, 2) + Math.Pow(p.Y - meanY, 2));
            ssTot += distFromMean * distFromMean;
        }

        line.RSquared = ssTot > 0 ? 1 - ssRes / ssTot : 0;

        return line;
    }

    private List<IGeometryObject> DetectByHough(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var edges = new Mat();
        Cv2.Canny(roiImage, edges, 50, 150);

        var lines = Cv2.HoughLinesP(edges, 1, Math.PI / 180, (int)HoughThreshold, MinLineLength, MaxLineGap);

        foreach (var l in lines)
        {
            var start = OffsetPoint(new DrawingPointF(l.P1.X, l.P1.Y), roi);
            var end = OffsetPoint(new DrawingPointF(l.P2.X, l.P2.Y), roi);
            results.Add(new LineObject(start, end));
        }

        return results;
    }

    private List<IGeometryObject> DetectByEdgeFitting(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var edges = new Mat();
        Cv2.Canny(roiImage, edges, 50, 150);

        // 收集邊緣點
        var edgePoints = new List<DrawingPointF>();
        for (int y = 0; y < edges.Rows; y++)
        {
            for (int x = 0; x < edges.Cols; x++)
            {
                if (edges.At<byte>(y, x) > 0)
                {
                    edgePoints.Add(OffsetPoint(new DrawingPointF(x, y), roi));
                }
            }
        }

        if (edgePoints.Count >= 10)
        {
            var line = FitLineLeastSquares(edgePoints);
            if (line != null)
            {
                line.EdgePoints = edgePoints;
                results.Add(line);
            }
        }

        return results;
    }
}
