using OpenCvSharp;
using SubpixelEdgeComparator.Utils;

namespace SubpixelEdgeComparator.Core;

/// <summary>
/// 影像處理核心類別
/// </summary>
public class ImageProcessor : IDisposable
{
    private Mat? _currentImage;
    private double[,]? _imageData;
    private bool _disposed;

    /// <summary>
    /// 目前載入的影像
    /// </summary>
    public Mat? CurrentImage => _currentImage;

    /// <summary>
    /// 影像是否已載入
    /// </summary>
    public bool HasImage => _currentImage != null && !_currentImage.Empty();

    /// <summary>
    /// 載入影像並轉換為灰階
    /// </summary>
    public Mat LoadImage(string path)
    {
        _currentImage?.Dispose();

        var img = Cv2.ImRead(path, ImreadModes.Grayscale);
        if (img.Empty())
        {
            throw new InvalidOperationException($"無法載入影像：{path}");
        }

        _currentImage = img;
        UpdateImageData();
        return _currentImage;
    }

    /// <summary>
    /// 設定影像（用於合成影像）
    /// </summary>
    public void SetImage(Mat image)
    {
        _currentImage?.Dispose();
        _currentImage = image.Clone();
        UpdateImageData();
    }

    /// <summary>
    /// 產生合成測試影像
    /// </summary>
    public Mat GenerateSyntheticImage(SyntheticImageParams param)
    {
        var image = new Mat(param.Height, param.Width, MatType.CV_8UC1);
        var random = new Random();

        for (int y = 0; y < param.Height; y++)
        {
            for (int x = 0; x < param.Width; x++)
            {
                // 使用 erf 函數產生平滑邊緣
                double z = (x - param.EdgePosition) / (param.BlurSigma * Math.Sqrt(2));
                double ratio = (1 + MathHelper.Erf(z)) / 2.0;
                double grayValue = param.LeftGray + (param.RightGray - param.LeftGray) * ratio;

                // 加入雜訊
                if (param.AddNoise)
                {
                    double noise = GenerateGaussianNoise(random, 0, param.NoiseSigma);
                    grayValue += noise;
                }

                // 限制範圍
                grayValue = Math.Clamp(grayValue, 0, 255);
                image.Set(y, x, (byte)grayValue);
            }
        }

        _currentImage?.Dispose();
        _currentImage = image;
        UpdateImageData();
        return _currentImage;
    }

    /// <summary>
    /// 沿 ROI 線段提取灰階剖面
    /// </summary>
    public double[] ExtractProfile(Point2d p1, Point2d p2, int supersample = 2)
    {
        if (_imageData == null)
            throw new InvalidOperationException("尚未載入影像");

        // 計算線段長度
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1)
            return Array.Empty<double>();

        // 超取樣點數
        int numSamples = (int)(length * supersample);
        if (numSamples < 2) numSamples = 2;

        double[] profile = new double[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / (numSamples - 1);
            double x = p1.X + t * dx;
            double y = p1.Y + t * dy;

            // 雙線性插值
            profile[i] = MathHelper.BilinearInterpolate(_imageData, x, y);
        }

        return profile;
    }

    /// <summary>
    /// 計算梯度
    /// </summary>
    public double[] ComputeGradient(double[] profile)
    {
        if (profile.Length < 2)
            return Array.Empty<double>();

        double[] gradient = new double[profile.Length - 1];

        for (int i = 0; i < gradient.Length; i++)
        {
            gradient[i] = profile[i + 1] - profile[i];
        }

        return gradient;
    }

    /// <summary>
    /// 執行完整分析
    /// </summary>
    public FullAnalysisResult Analyze(Point2d p1, Point2d p2, double? trueEdgePosition = null, int supersample = 2)
    {
        var result = new FullAnalysisResult();

        // 提取剖面
        result.GrayProfile = ExtractProfile(p1, p2, supersample);
        if (result.GrayProfile.Length < 5)
        {
            throw new InvalidOperationException("剖面長度不足，無法進行分析");
        }

        // 計算梯度
        result.GradientProfile = ComputeGradient(result.GrayProfile);

        // 計算線段長度（用於將剖面位置轉換回實際位置）
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        double scale = length / (result.GrayProfile.Length - 1);

        // 計算真實邊緣在剖面中的位置（如果有的話）
        double? profileTruePosition = null;
        if (trueEdgePosition.HasValue)
        {
            // 假設邊緣是垂直的，計算投影位置
            double t = (trueEdgePosition.Value - p1.X) / dx;
            if (t >= 0 && t <= 1)
            {
                profileTruePosition = t * (result.GrayProfile.Length - 1);
            }
            result.TrueEdgePosition = trueEdgePosition;
        }

        // 執行四種演算法
        var parabolic = SubpixelMethods.ParabolicFit(result.GradientProfile);
        var gaussian = SubpixelMethods.GaussianFit(result.GradientProfile);
        var moment = SubpixelMethods.MomentMethod(result.GradientProfile);
        var sigmoid = SubpixelMethods.SigmoidFit(result.GrayProfile);

        // 計算誤差（如果有真實位置）
        if (profileTruePosition.HasValue)
        {
            if (parabolic.Position.HasValue)
                parabolic.Error = parabolic.Position.Value - profileTruePosition.Value;
            if (gaussian.Position.HasValue)
                gaussian.Error = gaussian.Position.Value - profileTruePosition.Value;
            if (moment.Position.HasValue)
                moment.Error = moment.Position.Value - profileTruePosition.Value;
            if (sigmoid.Position.HasValue)
                sigmoid.Error = sigmoid.Position.Value - profileTruePosition.Value;
        }

        result.Results.Add(parabolic);
        result.Results.Add(gaussian);
        result.Results.Add(moment);
        result.Results.Add(sigmoid);

        return result;
    }

    /// <summary>
    /// 將 Mat 轉換為 Bitmap（供 UI 顯示）
    /// </summary>
    public System.Drawing.Bitmap? ToBitmap()
    {
        if (_currentImage == null || _currentImage.Empty())
            return null;

        // 轉換為彩色以便顯示
        using var colorImage = new Mat();
        Cv2.CvtColor(_currentImage, colorImage, ColorConversionCodes.GRAY2BGR);

        return OpenCvSharp.Extensions.BitmapConverter.ToBitmap(colorImage);
    }

    private void UpdateImageData()
    {
        if (_currentImage == null || _currentImage.Empty())
        {
            _imageData = null;
            return;
        }

        int height = _currentImage.Rows;
        int width = _currentImage.Cols;
        _imageData = new double[height, width];

        // 將影像資料複製到陣列
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _imageData[y, x] = _currentImage.At<byte>(y, x);
            }
        }
    }

    private static double GenerateGaussianNoise(Random random, double mean, double stdDev)
    {
        // Box-Muller 變換
        double u1 = 1.0 - random.NextDouble();
        double u2 = 1.0 - random.NextDouble();
        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * randStdNormal;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentImage?.Dispose();
            }
            _disposed = true;
        }
    }
}
