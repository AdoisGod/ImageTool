using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace VisionVerificationToolkit.Detection;

/// <summary>
/// 檢測器介面
/// </summary>
public interface IDetector
{
    /// <summary>檢測器名稱</summary>
    string Name { get; }

    /// <summary>檢測器描述</summary>
    string Description { get; }

    /// <summary>執行檢測</summary>
    List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null);
}

/// <summary>
/// 檢測器基底類別
/// </summary>
public abstract class DetectorBase : IDetector
{
    public abstract string Name { get; }
    public abstract string Description { get; }

    public abstract List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null);

    protected Mat GetRoiImage(Mat image, DrawingRectangleF? roi)
    {
        if (roi == null || roi.Value.IsEmpty)
            return image.Clone();

        var rect = new Rect(
            (int)roi.Value.X,
            (int)roi.Value.Y,
            (int)roi.Value.Width,
            (int)roi.Value.Height);

        // 確保在影像範圍內
        rect.X = Math.Max(0, rect.X);
        rect.Y = Math.Max(0, rect.Y);
        rect.Width = Math.Min(rect.Width, image.Width - rect.X);
        rect.Height = Math.Min(rect.Height, image.Height - rect.Y);

        if (rect.Width <= 0 || rect.Height <= 0)
            return image.Clone();

        return new Mat(image, rect);
    }

    protected System.Drawing.PointF OffsetPoint(System.Drawing.PointF point, DrawingRectangleF? roi)
    {
        if (roi == null) return point;
        return new System.Drawing.PointF(point.X + roi.Value.X, point.Y + roi.Value.Y);
    }

    protected OpenCvSharp.Point OffsetPoint(OpenCvSharp.Point point, DrawingRectangleF? roi)
    {
        if (roi == null) return point;
        return new OpenCvSharp.Point(point.X + (int)roi.Value.X, point.Y + (int)roi.Value.Y);
    }
}
