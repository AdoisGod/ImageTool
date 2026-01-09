using System.Drawing;
using OpenCvSharp;

namespace VisionVerificationToolkit.Objects;

/// <summary>
/// 輪廓物件
/// </summary>
public class ContourObject : GeometryObjectBase
{
    public override string ObjectType => "Contour";

    public List<PointF> Points { get; set; } = new();
    public double Area { get; private set; }
    public double Perimeter { get; private set; }
    public PointF Centroid { get; private set; }
    public RectangleF BoundingRect { get; private set; }
    public double Circularity { get; private set; }
    public double Convexity { get; private set; }
    public double AspectRatio { get; private set; }

    public ContourObject(OpenCvSharp.Point[] contour, string? name = null) : base(name)
    {
        Points = contour.Select(p => new PointF(p.X, p.Y)).ToList();
        DisplayColor = Color.Magenta;
        CalculateProperties(contour);
    }

    public ContourObject(List<PointF> points, string? name = null) : base(name)
    {
        Points = points;
        DisplayColor = Color.Magenta;
        var contour = points.Select(p => new OpenCvSharp.Point((int)p.X, (int)p.Y)).ToArray();
        CalculateProperties(contour);
    }

    private void CalculateProperties(OpenCvSharp.Point[] contour)
    {
        if (contour.Length < 3)
        {
            Area = 0;
            Perimeter = 0;
            Circularity = 0;
            return;
        }

        Area = Math.Abs(Cv2.ContourArea(contour));
        Perimeter = Cv2.ArcLength(contour, true);

        var moments = Cv2.Moments(contour);
        if (moments.M00 > 0)
        {
            Centroid = new PointF((float)(moments.M10 / moments.M00), (float)(moments.M01 / moments.M00));
        }

        var rect = Cv2.BoundingRect(contour);
        BoundingRect = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);

        AspectRatio = rect.Height > 0 ? (double)rect.Width / rect.Height : 0;

        // Circularity: 4π × Area / Perimeter²
        if (Perimeter > 0)
        {
            Circularity = 4 * Math.PI * Area / (Perimeter * Perimeter);
        }

        // Convexity: Convex Hull Area / Area
        var hull = Cv2.ConvexHull(contour);
        double hullArea = Math.Abs(Cv2.ContourArea(hull));
        Convexity = hullArea > 0 ? Area / hullArea : 0;
    }

    public override RectangleF GetBoundingBox() => BoundingRect;

    public override bool HitTest(PointF point, float tolerance = 5f)
    {
        // 簡單的邊界框檢測
        var expanded = new RectangleF(
            BoundingRect.X - tolerance,
            BoundingRect.Y - tolerance,
            BoundingRect.Width + tolerance * 2,
            BoundingRect.Height + tolerance * 2);
        return expanded.Contains(point);
    }

    public override string GetSummary() => $"面積={Area:F1}, 周長={Perimeter:F1}, 圓度={Circularity:F3}";

    public override Dictionary<string, object> GetDetails() => new()
    {
        ["Points"] = Points.Count,
        ["Area"] = Area,
        ["Perimeter"] = Perimeter,
        ["CentroidX"] = Centroid.X,
        ["CentroidY"] = Centroid.Y,
        ["BoundingWidth"] = BoundingRect.Width,
        ["BoundingHeight"] = BoundingRect.Height,
        ["AspectRatio"] = AspectRatio,
        ["Circularity"] = Circularity,
        ["Convexity"] = Convexity
    };
}
