using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace VisionVerificationToolkit.Detection;

/// <summary>
/// 輪廓檢測器
/// </summary>
public class ContourDetector : DetectorBase
{
    public override string Name => "找輪廓";
    public override string Description => "檢測連通輪廓";

    public RetrievalModes RetrievalMode { get; set; } = RetrievalModes.External;
    public ContourApproximationModes ApproximationMode { get; set; } = ContourApproximationModes.ApproxSimple;

    public double MinArea { get; set; } = 100;
    public double MaxArea { get; set; } = double.MaxValue;
    public double MinCircularity { get; set; } = 0;
    public double MaxCircularity { get; set; } = 1;
    public double MinAspectRatio { get; set; } = 0;
    public double MaxAspectRatio { get; set; } = double.MaxValue;

    public bool AutoThreshold { get; set; } = true;
    public double ManualThreshold { get; set; } = 127;

    public override List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var binary = new Mat();

        // 二值化
        if (AutoThreshold)
        {
            Cv2.Threshold(roiImage, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        }
        else
        {
            Cv2.Threshold(roiImage, binary, ManualThreshold, 255, ThresholdTypes.Binary);
        }

        // 找輪廓
        Cv2.FindContours(binary, out var contours, out _, RetrievalMode, ApproximationMode);

        foreach (var contour in contours)
        {
            // 面積過濾
            double area = Cv2.ContourArea(contour);
            if (area < MinArea || area > MaxArea) continue;

            // 圓度過濾
            double perimeter = Cv2.ArcLength(contour, true);
            double circularity = perimeter > 0 ? 4 * Math.PI * area / (perimeter * perimeter) : 0;
            if (circularity < MinCircularity || circularity > MaxCircularity) continue;

            // 長寬比過濾
            var rect = Cv2.BoundingRect(contour);
            double aspectRatio = rect.Height > 0 ? (double)rect.Width / rect.Height : 0;
            if (aspectRatio < MinAspectRatio || aspectRatio > MaxAspectRatio) continue;

            // 偏移座標
            var offsetContour = contour.Select(p => OffsetPoint(p, roi)).ToArray();
            var contourObj = new ContourObject(offsetContour);
            results.Add(contourObj);
        }

        return results;
    }
}
