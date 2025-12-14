namespace VisionVerificationToolkit.Core;

/// <summary>
/// 校正管理器 - 管理像素解析度
/// </summary>
public class CalibrationManager
{
    private double _resolution = 1.0; // mm/pixel

    public event EventHandler? ResolutionChanged;

    public double Resolution
    {
        get => _resolution;
        private set
        {
            if (value <= 0)
                throw new ArgumentException("Resolution must be positive");
            _resolution = value;
            IsCalibrated = true;
            ResolutionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsCalibrated { get; private set; }

    /// <summary>手動設定解析度 (mm/pixel)</summary>
    public void SetResolution(double mmPerPixel)
    {
        Resolution = mmPerPixel;
    }

    /// <summary>從 pixel/mm 設定解析度</summary>
    public void SetResolutionFromPixelPerMm(double pixelPerMm)
    {
        if (pixelPerMm <= 0)
            throw new ArgumentException("pixel/mm must be positive");
        Resolution = 1.0 / pixelPerMm;
    }

    /// <summary>使用標準圓校正</summary>
    public void CalibrateWithCircle(double measuredDiameterPx, double actualDiameterMm)
    {
        if (measuredDiameterPx <= 0 || actualDiameterMm <= 0)
            throw new ArgumentException("Diameters must be positive");
        Resolution = actualDiameterMm / measuredDiameterPx;
    }

    /// <summary>使用標準距離校正</summary>
    public void CalibrateWithDistance(double measuredDistancePx, double actualDistanceMm)
    {
        if (measuredDistancePx <= 0 || actualDistanceMm <= 0)
            throw new ArgumentException("Distances must be positive");
        Resolution = actualDistanceMm / measuredDistancePx;
    }

    /// <summary>像素轉毫米</summary>
    public double PixelsToMm(double pixels) => pixels * _resolution;

    /// <summary>毫米轉像素</summary>
    public double MmToPixels(double mm) => mm / _resolution;

    public void Reset()
    {
        _resolution = 1.0;
        IsCalibrated = false;
        ResolutionChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetResolutionString()
    {
        if (!IsCalibrated)
            return "未校正 (1 px = 1 mm)";
        return $"{_resolution:F6} mm/px ({1.0 / _resolution:F2} px/mm)";
    }
}
