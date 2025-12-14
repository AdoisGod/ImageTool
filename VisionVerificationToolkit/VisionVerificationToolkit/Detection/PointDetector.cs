using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingPointF = System.Drawing.PointF;

namespace VisionVerificationToolkit.Detection;

public enum PointDetectionMethod
{
    Harris,
    ShiTomasi,
    BlobCentroid
}

/// <summary>
/// 特徵點檢測器
/// </summary>
public class PointDetector : DetectorBase
{
    public override string Name => "找點";
    public override string Description => "檢測特徵點或角點";

    public PointDetectionMethod Method { get; set; } = PointDetectionMethod.ShiTomasi;

    // Harris 參數
    public int HarrisBlockSize { get; set; } = 2;
    public int HarrisKSize { get; set; } = 3;
    public double HarrisK { get; set; } = 0.04;
    public double HarrisThreshold { get; set; } = 0.01;

    // Shi-Tomasi 參數
    public int MaxCorners { get; set; } = 100;
    public double QualityLevel { get; set; } = 0.01;
    public double MinDistance { get; set; } = 10;

    // 斑點質心參數
    public double MinArea { get; set; } = 10;
    public double MaxArea { get; set; } = 10000;
    public double MinCircularity { get; set; } = 0.5;

    public override List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null)
    {
        return Method switch
        {
            PointDetectionMethod.Harris => DetectHarris(image, roi),
            PointDetectionMethod.BlobCentroid => DetectBlobCentroid(image, roi),
            _ => DetectShiTomasi(image, roi)
        };
    }

    private List<IGeometryObject> DetectHarris(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var harris = new Mat();
        using var normalized = new Mat();

        Cv2.CornerHarris(roiImage, harris, HarrisBlockSize, HarrisKSize, HarrisK);
        Cv2.Normalize(harris, normalized, 0, 255, NormTypes.MinMax, MatType.CV_32FC1);

        float threshold = (float)(HarrisThreshold * 255);

        for (int y = 1; y < normalized.Rows - 1; y++)
        {
            for (int x = 1; x < normalized.Cols - 1; x++)
            {
                float val = normalized.At<float>(y, x);
                if (val > threshold)
                {
                    // 非極大值抑制
                    bool isMax = true;
                    for (int dy = -1; dy <= 1 && isMax; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (normalized.At<float>(y + dy, x + dx) >= val)
                            {
                                isMax = false;
                                break;
                            }
                        }
                    }

                    if (isMax)
                    {
                        var point = new PointObject(OffsetPoint(new DrawingPointF(x, y), roi))
                        {
                            Quality = val / 255.0,
                            Source = "Harris"
                        };
                        results.Add(point);
                    }
                }
            }
        }

        return results;
    }

    private List<IGeometryObject> DetectShiTomasi(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);

        var corners = Cv2.GoodFeaturesToTrack(
            roiImage,
            MaxCorners,
            QualityLevel,
            MinDistance);

        foreach (var corner in corners)
        {
            var point = new PointObject(OffsetPoint(new DrawingPointF(corner.X, corner.Y), roi))
            {
                Quality = 1.0,
                Source = "Shi-Tomasi"
            };
            results.Add(point);
        }

        return results;
    }

    private List<IGeometryObject> DetectBlobCentroid(Mat image, DrawingRectangleF? roi)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);
        using var binary = new Mat();

        // 二值化
        Cv2.Threshold(roiImage, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // 找輪廓
        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < MinArea || area > MaxArea) continue;

            double perimeter = Cv2.ArcLength(contour, true);
            double circularity = perimeter > 0 ? 4 * Math.PI * area / (perimeter * perimeter) : 0;

            if (circularity < MinCircularity) continue;

            // 計算質心
            var moments = Cv2.Moments(contour);
            if (moments.M00 > 0)
            {
                double cx = moments.M10 / moments.M00;
                double cy = moments.M01 / moments.M00;

                var point = new PointObject(OffsetPoint(new DrawingPointF((float)cx, (float)cy), roi))
                {
                    Quality = circularity,
                    Source = "BlobCentroid"
                };
                results.Add(point);
            }
        }

        return results;
    }
}
