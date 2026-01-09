using OpenCvSharp;
using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Detection;

/// <summary>
/// 直線檢測器（卡尺法）
/// </summary>
public class LineDetector
{
    /// <summary>
    /// 邊緣極性
    /// </summary>
    public enum EdgePolarity
    {
        DarkToLight,
        LightToDark,
        Both
    }

    /// <summary>
    /// 卡尺參數
    /// </summary>
    public class CaliperParams
    {
        public int NumSamples { get; set; } = 10;
        public int ProfileLength { get; set; } = 50;
        public EdgePolarity Polarity { get; set; } = EdgePolarity.Both;
        public double EdgeThreshold { get; set; } = 30;
        public bool UseRansac { get; set; } = true;
        public double RansacThreshold { get; set; } = 2.0;
    }

    /// <summary>
    /// 使用卡尺法檢測直線
    /// </summary>
    public static LineResult? DetectLine(Mat grayImage, PointF searchStart, PointF searchEnd, CaliperParams? param = null)
    {
        param ??= new CaliperParams();

        // 計算搜尋方向
        double dx = searchEnd.X - searchStart.X;
        double dy = searchEnd.Y - searchStart.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 10) return null;

        // 單位向量
        double ux = dx / length;
        double uy = dy / length;

        // 垂直方向（用於剖面）
        double perpX = -uy;
        double perpY = ux;

        var edgePoints = new List<PointF>();

        // 沿搜尋線採樣
        for (int i = 0; i < param.NumSamples; i++)
        {
            double t = (double)i / (param.NumSamples - 1);
            float cx = (float)(searchStart.X + t * dx);
            float cy = (float)(searchStart.Y + t * dy);

            // 提取垂直剖面
            var profile = ExtractProfile(grayImage, cx, cy, perpX, perpY, param.ProfileLength);
            if (profile == null) continue;

            // 計算梯度
            var gradient = ComputeGradient(profile);

            // 找邊緣
            var edgePos = FindEdge(gradient, param.Polarity, param.EdgeThreshold);
            if (edgePos.HasValue)
            {
                // 轉換回影像座標
                double offset = edgePos.Value - param.ProfileLength / 2.0;
                float ex = (float)(cx + offset * perpX);
                float ey = (float)(cy + offset * perpY);

                if (ex >= 0 && ex < grayImage.Cols && ey >= 0 && ey < grayImage.Rows)
                {
                    edgePoints.Add(new PointF(ex, ey));
                }
            }
        }

        if (edgePoints.Count < 3)
            return null;

        try
        {
            double slope, intercept, rSquared;
            List<PointF> inliers;

            if (param.UseRansac)
            {
                (slope, intercept, rSquared, inliers) = GeometryMath.FitLineRansac(
                    edgePoints, param.RansacThreshold);
            }
            else
            {
                (slope, intercept, rSquared) = GeometryMath.FitLine(edgePoints);
                inliers = edgePoints;
            }

            // 計算起終點
            float minX = inliers.Min(p => p.X);
            float maxX = inliers.Max(p => p.X);

            PointF startPt, endPt;
            if (double.IsInfinity(slope))
            {
                // 垂直線
                float avgX = (float)inliers.Average(p => p.X);
                startPt = new PointF(avgX, inliers.Min(p => p.Y));
                endPt = new PointF(avgX, inliers.Max(p => p.Y));
            }
            else
            {
                startPt = new PointF(minX, (float)(slope * minX + intercept));
                endPt = new PointF(maxX, (float)(slope * maxX + intercept));
            }

            double lineLength = GeometryMath.Distance(startPt, endPt);
            double angle = GeometryMath.LineAngle(startPt, endPt);

            return new LineResult
            {
                Name = "線1",
                StartPoint = startPt,
                EndPoint = endPt,
                Slope = slope,
                Intercept = intercept,
                AngleDegrees = angle,
                LengthPixels = lineLength,
                RSquared = rSquared,
                EdgePoints = inliers
            };
        }
        catch
        {
            return null;
        }
    }

    private static double[]? ExtractProfile(Mat image, float cx, float cy, double perpX, double perpY, int length)
    {
        var profile = new double[length];
        int halfLen = length / 2;

        for (int i = 0; i < length; i++)
        {
            int offset = i - halfLen;
            float x = cx + (float)(offset * perpX);
            float y = cy + (float)(offset * perpY);

            int ix = (int)Math.Round(x);
            int iy = (int)Math.Round(y);

            if (ix < 0 || ix >= image.Cols || iy < 0 || iy >= image.Rows)
                return null;

            profile[i] = image.At<byte>(iy, ix);
        }

        return profile;
    }

    private static double[] ComputeGradient(double[] profile)
    {
        var gradient = new double[profile.Length - 1];
        for (int i = 0; i < gradient.Length; i++)
        {
            gradient[i] = profile[i + 1] - profile[i];
        }
        return gradient;
    }

    private static double? FindEdge(double[] gradient, EdgePolarity polarity, double threshold)
    {
        double maxVal = 0;
        int maxIdx = -1;

        for (int i = 1; i < gradient.Length - 1; i++)
        {
            double val = gradient[i];

            bool match = polarity switch
            {
                EdgePolarity.DarkToLight => val > threshold,
                EdgePolarity.LightToDark => val < -threshold,
                EdgePolarity.Both => Math.Abs(val) > threshold,
                _ => false
            };

            if (match && Math.Abs(val) > Math.Abs(maxVal))
            {
                maxVal = val;
                maxIdx = i;
            }
        }

        if (maxIdx < 0) return null;

        // 亞像素定位（拋物線擬合）
        if (maxIdx > 0 && maxIdx < gradient.Length - 1)
        {
            double y0 = Math.Abs(gradient[maxIdx - 1]);
            double y1 = Math.Abs(gradient[maxIdx]);
            double y2 = Math.Abs(gradient[maxIdx + 1]);

            double denom = 2 * (2 * y1 - y0 - y2);
            if (Math.Abs(denom) > 1e-10)
            {
                double offset = (y0 - y2) / denom;
                return maxIdx + offset;
            }
        }

        return maxIdx;
    }
}
