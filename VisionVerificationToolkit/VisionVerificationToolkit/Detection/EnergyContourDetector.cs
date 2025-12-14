using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingPointF = System.Drawing.PointF;

namespace VisionVerificationToolkit.Detection;

/// <summary>
/// 能量最小化輪廓檢測器
/// 使用動態規劃尋找全局最優的封閉輪廓
/// </summary>
public class EnergyContourDetector : DetectorBase
{
    public override string Name => "能量輪廓";
    public override string Description => "能量最小化輪廓檢測";

    /// <summary>數據項權重 - 控制邊緣強度的影響</summary>
    public double DataWeight { get; set; } = 1.0;

    /// <summary>平滑項權重 - 控制輪廓平滑度</summary>
    public double SmoothWeight { get; set; } = 0.5;

    /// <summary>角度懲罰 - 懲罰急轉彎</summary>
    public double AnglePenalty { get; set; } = 2.0;

    /// <summary>搜尋範圍 (像素)</summary>
    public int SearchRange { get; set; } = 10;

    /// <summary>角度解析度 (將360度分成多少份)</summary>
    public int AngleResolution { get; set; } = 72;

    /// <summary>封閉懲罰 - 鼓勵輪廓封閉</summary>
    public double ClosurePenalty { get; set; } = 10.0;

    /// <summary>最小輪廓長度</summary>
    public int MinContourLength { get; set; } = 50;

    /// <summary>迭代次數</summary>
    public int Iterations { get; set; } = 3;

    /// <summary>是否使用自動種子點</summary>
    public bool AutoSeedPoint { get; set; } = true;

    /// <summary>手動種子點 (當AutoSeedPoint=false時使用)</summary>
    public DrawingPointF ManualSeedPoint { get; set; }

    public override List<IGeometryObject> Detect(Mat image, DrawingRectangleF? roi = null)
    {
        var results = new List<IGeometryObject>();

        using var roiImage = GetRoiImage(image, roi);

        // 計算梯度
        using var gradX = new Mat();
        using var gradY = new Mat();
        using var gradMag = new Mat();

        Cv2.Sobel(roiImage, gradX, MatType.CV_64F, 1, 0, 3);
        Cv2.Sobel(roiImage, gradY, MatType.CV_64F, 0, 1, 3);
        Cv2.Magnitude(gradX, gradY, gradMag);

        // 正規化梯度
        double minVal, maxVal;
        Cv2.MinMaxLoc(gradMag, out minVal, out maxVal);
        using var normalizedGrad = new Mat();
        gradMag.ConvertTo(normalizedGrad, MatType.CV_64F, 1.0 / (maxVal + 1e-10));

        // 決定種子點
        Point seedPoint;
        if (AutoSeedPoint)
        {
            seedPoint = FindBestSeedPoint(normalizedGrad, roiImage);
        }
        else
        {
            seedPoint = new Point((int)ManualSeedPoint.X, (int)ManualSeedPoint.Y);
            if (roi.HasValue)
            {
                seedPoint.X -= (int)roi.Value.X;
                seedPoint.Y -= (int)roi.Value.Y;
            }
        }

        // 使用能量最小化方法追蹤輪廓
        var contourPoints = TraceContour(normalizedGrad, gradX, gradY, seedPoint);

        if (contourPoints.Count >= MinContourLength)
        {
            // 偏移回原始座標
            var offsetContour = contourPoints.Select(p => OffsetPoint(p, roi)).ToArray();
            var contourObj = new ContourObject(offsetContour, "EnergyContour");
            results.Add(contourObj);
        }

        return results;
    }

    private Point FindBestSeedPoint(Mat gradMag, Mat grayImage)
    {
        // 找到梯度較強的區域作為種子點
        // 使用二值化 + 找輪廓的方式找到邊緣區域
        using var binary = new Mat();
        using var grad8u = new Mat();
        gradMag.ConvertTo(grad8u, MatType.CV_8U, 255);

        // 使用Otsu找閾值
        double thresh = Cv2.Threshold(grad8u, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // 找輪廓
        Cv2.FindContours(binary, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        if (contours.Length == 0)
        {
            // 如果沒找到輪廓，使用影像中心
            return new Point(grayImage.Width / 2, grayImage.Height / 2);
        }

        // 找到最大的輪廓
        double maxArea = 0;
        Point[] largestContour = contours[0];
        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area > maxArea)
            {
                maxArea = area;
                largestContour = contour;
            }
        }

        // 返回輪廓上梯度最大的點
        double maxGrad = 0;
        Point bestPoint = largestContour[0];
        foreach (var pt in largestContour)
        {
            if (pt.X >= 0 && pt.X < gradMag.Width && pt.Y >= 0 && pt.Y < gradMag.Height)
            {
                double g = gradMag.At<double>(pt.Y, pt.X);
                if (g > maxGrad)
                {
                    maxGrad = g;
                    bestPoint = pt;
                }
            }
        }

        return bestPoint;
    }

    private List<Point> TraceContour(Mat gradMag, Mat gradX, Mat gradY, Point seedPoint)
    {
        var contourPoints = new List<Point>();

        int width = gradMag.Width;
        int height = gradMag.Height;

        // 初始化追蹤
        var currentPoint = seedPoint;
        var startPoint = seedPoint;

        // 使用visited map避免重複
        var visited = new bool[height, width];

        double prevAngle = 0;
        bool firstStep = true;

        for (int iter = 0; iter < Iterations; iter++)
        {
            contourPoints.Clear();
            currentPoint = seedPoint;
            Array.Clear(visited, 0, visited.Length);
            firstStep = true;
            prevAngle = 0;

            int maxSteps = width * height / 4; // 限制最大步數

            for (int step = 0; step < maxSteps; step++)
            {
                // 標記為已訪問
                if (currentPoint.Y >= 0 && currentPoint.Y < height &&
                    currentPoint.X >= 0 && currentPoint.X < width)
                {
                    visited[currentPoint.Y, currentPoint.X] = true;
                }

                contourPoints.Add(currentPoint);

                // 尋找下一個最佳點
                var nextPoint = FindNextPoint(gradMag, gradX, gradY, currentPoint, prevAngle, firstStep, visited);

                if (nextPoint == null)
                {
                    break;
                }

                // 檢查是否回到起點附近（封閉）
                if (!firstStep && contourPoints.Count > MinContourLength)
                {
                    double dx = nextPoint.Value.X - startPoint.X;
                    double dy = nextPoint.Value.Y - startPoint.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    if (dist < SearchRange * 2)
                    {
                        // 成功封閉
                        contourPoints.Add(startPoint);
                        break;
                    }
                }

                // 更新角度和位置
                double newAngle = Math.Atan2(nextPoint.Value.Y - currentPoint.Y,
                                            nextPoint.Value.X - currentPoint.X);
                prevAngle = newAngle;
                firstStep = false;
                currentPoint = nextPoint.Value;
            }

            // 迭代優化：使用snake方法微調
            if (iter < Iterations - 1 && contourPoints.Count > 3)
            {
                RefineContour(contourPoints, gradMag);
            }
        }

        return contourPoints;
    }

    private Point? FindNextPoint(Mat gradMag, Mat gradX, Mat gradY, Point current,
                                  double prevAngle, bool firstStep, bool[,] visited)
    {
        int width = gradMag.Width;
        int height = gradMag.Height;

        double bestEnergy = double.MaxValue;
        Point? bestPoint = null;

        // 搜尋鄰域
        for (int dy = -SearchRange; dy <= SearchRange; dy++)
        {
            for (int dx = -SearchRange; dx <= SearchRange; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = current.X + dx;
                int ny = current.Y + dy;

                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                if (visited[ny, nx]) continue;

                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > SearchRange) continue;

                // 計算能量
                double energy = ComputeEnergy(gradMag, gradX, gradY, current,
                                             new Point(nx, ny), prevAngle, firstStep, dist);

                if (energy < bestEnergy)
                {
                    bestEnergy = energy;
                    bestPoint = new Point(nx, ny);
                }
            }
        }

        return bestPoint;
    }

    private double ComputeEnergy(Mat gradMag, Mat gradX, Mat gradY, Point current,
                                  Point next, double prevAngle, bool firstStep, double dist)
    {
        // 數據項：負梯度強度（邊緣越強能量越低）
        double grad = gradMag.At<double>(next.Y, next.X);
        double dataEnergy = -DataWeight * grad;

        // 平滑項：角度變化懲罰
        double smoothEnergy = 0;
        if (!firstStep)
        {
            double currentAngle = Math.Atan2(next.Y - current.Y, next.X - current.X);
            double angleDiff = Math.Abs(currentAngle - prevAngle);
            if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;
            smoothEnergy = SmoothWeight * (angleDiff / Math.PI);
        }

        // 角度懲罰：急轉彎懲罰
        double anglePenaltyEnergy = 0;
        if (!firstStep)
        {
            double currentAngle = Math.Atan2(next.Y - current.Y, next.X - current.X);
            double angleDiff = Math.Abs(currentAngle - prevAngle);
            if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;
            if (angleDiff > Math.PI / 2)
            {
                anglePenaltyEnergy = AnglePenalty * (angleDiff - Math.PI / 2) / (Math.PI / 2);
            }
        }

        // 梯度方向項：優先沿著梯度垂直方向移動
        double gx = gradX.At<double>(next.Y, next.X);
        double gy = gradY.At<double>(next.Y, next.X);
        double gradDir = Math.Atan2(gy, gx);
        double moveDir = Math.Atan2(next.Y - current.Y, next.X - current.X);
        double perpDiff = Math.Abs(Math.Abs(gradDir - moveDir) - Math.PI / 2);
        if (perpDiff > Math.PI / 2) perpDiff = Math.PI - perpDiff;
        double directionEnergy = 0.3 * perpDiff / (Math.PI / 2);

        return dataEnergy + smoothEnergy + anglePenaltyEnergy + directionEnergy;
    }

    private void RefineContour(List<Point> contour, Mat gradMag)
    {
        int n = contour.Count;
        int width = gradMag.Width;
        int height = gradMag.Height;

        // Simple snake-like refinement
        for (int iter = 0; iter < 5; iter++)
        {
            for (int i = 1; i < n - 1; i++)
            {
                var prev = contour[i - 1];
                var curr = contour[i];
                var next = contour[i + 1];

                double bestEnergy = double.MaxValue;
                Point bestPoint = curr;

                // 在小鄰域內搜尋
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = curr.X + dx;
                        int ny = curr.Y + dy;

                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;

                        // 計算彈性能量
                        double d1 = Math.Sqrt(Math.Pow(nx - prev.X, 2) + Math.Pow(ny - prev.Y, 2));
                        double d2 = Math.Sqrt(Math.Pow(next.X - nx, 2) + Math.Pow(next.Y - ny, 2));
                        double elasticEnergy = Math.Abs(d1 - d2);

                        // 計算彎曲能量
                        double ax = prev.X - 2 * nx + next.X;
                        double ay = prev.Y - 2 * ny + next.Y;
                        double bendEnergy = ax * ax + ay * ay;

                        // 數據能量
                        double grad = gradMag.At<double>(ny, nx);
                        double dataEnergy = -grad;

                        double totalEnergy = 0.1 * elasticEnergy + 0.1 * bendEnergy + dataEnergy;

                        if (totalEnergy < bestEnergy)
                        {
                            bestEnergy = totalEnergy;
                            bestPoint = new Point(nx, ny);
                        }
                    }
                }

                contour[i] = bestPoint;
            }
        }
    }
}
