using OpenCvSharp;

namespace VisionMeasurementToolkit.Utils;

/// <summary>
/// 測試影像生成器
/// </summary>
public static class TestImageGenerator
{
    /// <summary>
    /// 生成包含圓形的測試影像
    /// </summary>
    public static Mat GenerateCircleImage(int width, int height, int centerX, int centerY, int radius, int bgColor = 50, int fgColor = 200)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, new Scalar(bgColor));
        Cv2.Circle(image, new OpenCvSharp.Point(centerX, centerY), radius, new Scalar(fgColor), -1, LineTypes.AntiAlias);
        return image;
    }

    /// <summary>
    /// 生成包含直線的測試影像
    /// </summary>
    public static Mat GenerateLineImage(int width, int height, OpenCvSharp.Point p1, OpenCvSharp.Point p2, int bgColor = 50, int fgColor = 200)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, new Scalar(bgColor));
        Cv2.Line(image, p1, p2, new Scalar(fgColor), 3, LineTypes.AntiAlias);
        return image;
    }

    /// <summary>
    /// 生成綜合測試影像（包含圓和線）
    /// </summary>
    public static Mat GenerateCompositeTestImage(int width = 800, int height = 600)
    {
        var image = new Mat(height, width, MatType.CV_8UC1, new Scalar(30));

        // 繪製多個圓
        Cv2.Circle(image, new OpenCvSharp.Point(200, 200), 80, new Scalar(180), -1, LineTypes.AntiAlias);
        Cv2.Circle(image, new OpenCvSharp.Point(600, 200), 60, new Scalar(200), -1, LineTypes.AntiAlias);
        Cv2.Circle(image, new OpenCvSharp.Point(400, 450), 100, new Scalar(160), -1, LineTypes.AntiAlias);

        // 繪製直線
        Cv2.Line(image, new OpenCvSharp.Point(50, 350), new OpenCvSharp.Point(350, 320), new Scalar(220), 3, LineTypes.AntiAlias);
        Cv2.Line(image, new OpenCvSharp.Point(450, 100), new OpenCvSharp.Point(750, 150), new Scalar(210), 3, LineTypes.AntiAlias);

        // 加入一些雜訊
        var noise = new Mat(height, width, MatType.CV_8UC1);
        Cv2.Randn(noise, new Scalar(0), new Scalar(10));
        Cv2.Add(image, noise, image);

        return image;
    }

    /// <summary>
    /// 儲存測試影像
    /// </summary>
    public static void SaveTestImage(string path)
    {
        using var image = GenerateCompositeTestImage();
        Cv2.ImWrite(path, image);
    }
}
