using OpenCvSharp;

namespace VisionVerificationToolkit.Processing;

/// <summary>
/// 影像品質分析結果
/// </summary>
public class ImageQualityResult
{
    public double Sharpness { get; set; }
    public double Contrast { get; set; }
    public double Brightness { get; set; }
    public double NoiseLevel { get; set; }
    public double DynamicRange { get; set; }

    public string SharpnessLevel => Sharpness < 100 ? "模糊" : Sharpness < 300 ? "普通" : "清晰";
    public string ContrastLevel => Contrast < 30 ? "低" : Contrast < 60 ? "中" : "高";
    public string BrightnessLevel => Brightness < 50 ? "過暗" : Brightness > 200 ? "過亮" : "正常";
    public string NoiseDescription => NoiseLevel < 0.1 ? "低" : NoiseLevel < 0.3 ? "中" : "高";
    public string DynamicRangeLevel => DynamicRange < 100 ? "窄" : "寬";

    public Color SharpnessColor => Sharpness < 100 ? Color.Red : Sharpness < 300 ? Color.Orange : Color.Green;
    public Color ContrastColor => Contrast < 30 ? Color.Red : Contrast < 60 ? Color.Orange : Color.Green;
    public Color BrightnessColor => (Brightness < 50 || Brightness > 200) ? Color.Red : Color.Green;
    public Color NoiseColor => NoiseLevel < 0.1 ? Color.Green : NoiseLevel < 0.3 ? Color.Orange : Color.Red;
    public Color DynamicRangeColor => DynamicRange < 100 ? Color.Orange : Color.Green;

    public string GetOverallAssessment()
    {
        var suggestions = new List<string>();

        if (Sharpness < 100)
            suggestions.Add("影像模糊，建議銳化處理");
        if (Contrast < 30)
            suggestions.Add("對比度低，建議直方圖等化或CLAHE");
        if (Brightness < 50)
            suggestions.Add("影像過暗，建議調整亮度");
        if (Brightness > 200)
            suggestions.Add("影像過亮，建議調整亮度");
        if (NoiseLevel > 0.3)
            suggestions.Add("雜訊較高，建議濾波處理");
        if (DynamicRange < 100)
            suggestions.Add("動態範圍窄，建議對比度拉伸");

        if (suggestions.Count == 0)
            return "良好 - 影像品質適合進行檢測";

        return string.Join("；", suggestions);
    }
}

/// <summary>
/// 影像品質分析器
/// </summary>
public static class ImageQualityAnalyzer
{
    /// <summary>
    /// 分析影像品質
    /// </summary>
    public static ImageQualityResult Analyze(Mat image)
    {
        Mat gray;
        if (image.Channels() == 1)
            gray = image;
        else
        {
            gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }

        var result = new ImageQualityResult
        {
            Sharpness = CalculateSharpness(gray),
            Contrast = CalculateContrast(gray),
            Brightness = CalculateBrightness(gray),
            NoiseLevel = CalculateNoiseLevel(gray),
            DynamicRange = CalculateDynamicRange(gray)
        };

        if (gray != image)
            gray.Dispose();

        return result;
    }

    /// <summary>
    /// 計算銳利度 - 使用 Laplacian 變異數
    /// </summary>
    private static double CalculateSharpness(Mat gray)
    {
        using var laplacian = new Mat();
        Cv2.Laplacian(gray, laplacian, MatType.CV_64F);

        Cv2.MeanStdDev(laplacian, out var mean, out var stddev);
        return stddev.Val0 * stddev.Val0; // 變異數
    }

    /// <summary>
    /// 計算對比度 - 灰階標準差
    /// </summary>
    private static double CalculateContrast(Mat gray)
    {
        Cv2.MeanStdDev(gray, out _, out var stddev);
        return stddev.Val0;
    }

    /// <summary>
    /// 計算亮度 - 平均灰階值
    /// </summary>
    private static double CalculateBrightness(Mat gray)
    {
        return Cv2.Mean(gray).Val0;
    }

    /// <summary>
    /// 計算雜訊程度 - 高頻能量比例
    /// </summary>
    private static double CalculateNoiseLevel(Mat gray)
    {
        // 使用高通濾波估計雜訊
        using var blurred = new Mat();
        using var highFreq = new Mat();

        Cv2.GaussianBlur(gray, blurred, new OpenCvSharp.Size(5, 5), 0);
        Cv2.Absdiff(gray, blurred, highFreq);

        var totalEnergy = Cv2.Sum(gray).Val0;
        var highFreqEnergy = Cv2.Sum(highFreq).Val0;

        if (totalEnergy < 1) return 0;
        return highFreqEnergy / totalEnergy;
    }

    /// <summary>
    /// 計算動態範圍
    /// </summary>
    private static double CalculateDynamicRange(Mat gray)
    {
        Cv2.MinMaxLoc(gray, out double min, out double max);
        return max - min;
    }
}
