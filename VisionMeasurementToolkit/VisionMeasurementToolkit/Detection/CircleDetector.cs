using OpenCvSharp;
using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Detection;

/// <summary>
/// 圓形檢測器
/// </summary>
public class CircleDetector
{
    /// <summary>
    /// 霍夫圓檢測參數
    /// </summary>
    public class HoughParams
    {
        public double Dp { get; set; } = 1.0;
        public double MinDist { get; set; } = 50;
        public double Param1 { get; set; } = 100;
        public double Param2 { get; set; } = 30;
        public int MinRadius { get; set; } = 0;
        public int MaxRadius { get; set; } = 0;
    }

    /// <summary>
    /// 邊緣擬合參數
    /// </summary>
    public class EdgeFitParams
    {
        public double CannyThreshold1 { get; set; } = 50;
        public double CannyThreshold2 { get; set; } = 150;
        public bool UseRansac { get; set; } = true;
        public double RansacThreshold { get; set; } = 2.0;
    }

    /// <summary>
    /// 使用霍夫變換檢測圓
    /// </summary>
    public static List<CircleResult> DetectHough(Mat grayImage, Rectangle roi, HoughParams? param = null)
    {
        param ??= new HoughParams();
        var results = new List<CircleResult>();

        // 裁切 ROI
        using var roiMat = new Mat(grayImage, new Rect(roi.X, roi.Y, roi.Width, roi.Height));

        // 模糊降噪
        using var blurred = new Mat();
        Cv2.GaussianBlur(roiMat, blurred, new OpenCvSharp.Size(9, 9), 2);

        // 霍夫圓檢測
        double minDist = param.MinDist > 0 ? param.MinDist : roi.Height / 8.0;
        var circles = Cv2.HoughCircles(
            blurred,
            HoughModes.Gradient,
            param.Dp,
            minDist,
            param.Param1,
            param.Param2,
            param.MinRadius,
            param.MaxRadius
        );

        foreach (var circle in circles)
        {
            results.Add(new CircleResult
            {
                Name = $"圓{results.Count + 1}",
                Center = new PointF(circle.Center.X + roi.X, circle.Center.Y + roi.Y),
                RadiusPixels = circle.Radius,
                RSquared = null // 霍夫方法無 R²
            });
        }

        return results;
    }

    /// <summary>
    /// 使用邊緣擬合檢測圓
    /// </summary>
    public static CircleResult? DetectEdgeFit(Mat grayImage, Rectangle roi, EdgeFitParams? param = null)
    {
        param ??= new EdgeFitParams();

        // 裁切 ROI
        using var roiMat = new Mat(grayImage, new Rect(roi.X, roi.Y, roi.Width, roi.Height));

        // Canny 邊緣檢測
        using var edges = new Mat();
        Cv2.Canny(roiMat, edges, param.CannyThreshold1, param.CannyThreshold2);

        // 提取邊緣點
        var edgePoints = new List<PointF>();
        for (int y = 0; y < edges.Rows; y++)
        {
            for (int x = 0; x < edges.Cols; x++)
            {
                if (edges.At<byte>(y, x) > 0)
                {
                    edgePoints.Add(new PointF(x + roi.X, y + roi.Y));
                }
            }
        }

        if (edgePoints.Count < 10)
            return null;

        try
        {
            if (param.UseRansac)
            {
                var (center, radius, rSquared, _) = GeometryMath.FitCircleRansac(
                    edgePoints, param.RansacThreshold);

                return new CircleResult
                {
                    Name = "圓1",
                    Center = center,
                    RadiusPixels = radius,
                    RSquared = rSquared
                };
            }
            else
            {
                var (center, radius, rSquared) = GeometryMath.FitCircle(edgePoints);

                return new CircleResult
                {
                    Name = "圓1",
                    Center = center,
                    RadiusPixels = radius,
                    RSquared = rSquared
                };
            }
        }
        catch
        {
            return null;
        }
    }
}
