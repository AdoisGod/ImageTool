using OpenCvSharp;

namespace VisionVerificationToolkit.Core;

/// <summary>
/// 影像管理器 - 管理原圖與處理後影像
/// </summary>
public class ImageManager : IDisposable
{
    private Mat? _originalImage;
    private Mat? _processedImage;
    private string? _imagePath;

    public event EventHandler? OriginalImageChanged;
    public event EventHandler? ProcessedImageChanged;

    public Mat? OriginalImage => _originalImage;
    public Mat? ProcessedImage => _processedImage;
    public string? ImagePath => _imagePath;

    public bool HasImage => _originalImage != null && !_originalImage.Empty();
    public int Width => _originalImage?.Width ?? 0;
    public int Height => _originalImage?.Height ?? 0;

    public void LoadImage(string path)
    {
        var newImage = Cv2.ImRead(path, ImreadModes.Grayscale);
        if (newImage.Empty())
        {
            newImage.Dispose();
            throw new InvalidOperationException($"無法載入影像: {path}");
        }

        _originalImage?.Dispose();
        _processedImage?.Dispose();

        _originalImage = newImage;
        _processedImage = _originalImage.Clone();
        _imagePath = path;

        OriginalImageChanged?.Invoke(this, EventArgs.Empty);
        ProcessedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetOriginalImage(Mat image)
    {
        _originalImage?.Dispose();
        _processedImage?.Dispose();

        _originalImage = image.Clone();
        _processedImage = image.Clone();
        _imagePath = null;

        OriginalImageChanged?.Invoke(this, EventArgs.Empty);
        ProcessedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateProcessedImage(Mat image)
    {
        _processedImage?.Dispose();
        _processedImage = image.Clone();
        ProcessedImageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ResetProcessedImage()
    {
        if (_originalImage != null)
        {
            _processedImage?.Dispose();
            _processedImage = _originalImage.Clone();
            ProcessedImageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public Mat GetGrayscaleOriginal()
    {
        if (_originalImage == null) throw new InvalidOperationException("No image loaded");

        if (_originalImage.Channels() == 1)
            return _originalImage.Clone();

        var gray = new Mat();
        Cv2.CvtColor(_originalImage, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    public Mat GetGrayscaleProcessed()
    {
        if (_processedImage == null) throw new InvalidOperationException("No processed image");

        if (_processedImage.Channels() == 1)
            return _processedImage.Clone();

        var gray = new Mat();
        Cv2.CvtColor(_processedImage, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }

    public void SaveProcessedImage(string path)
    {
        if (_processedImage == null) throw new InvalidOperationException("No processed image");
        Cv2.ImWrite(path, _processedImage);
    }

    public void Dispose()
    {
        _originalImage?.Dispose();
        _processedImage?.Dispose();
        _originalImage = null;
        _processedImage = null;
    }
}
