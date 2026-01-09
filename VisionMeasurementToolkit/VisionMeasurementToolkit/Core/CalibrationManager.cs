namespace VisionMeasurementToolkit.Core;

/// <summary>
/// 像素解析度校正管理器
/// </summary>
public class CalibrationManager
{
    /// <summary>
    /// 解析度 (mm/pixel)
    /// </summary>
    public double Resolution { get; private set; } = 1.0;

    /// <summary>
    /// 是否已校正
    /// </summary>
    public bool IsCalibrated { get; private set; } = false;

    /// <summary>
    /// 校正方式描述
    /// </summary>
    public string CalibrationMethod { get; private set; } = "未校正";

    /// <summary>
    /// 解析度變更事件
    /// </summary>
    public event EventHandler? ResolutionChanged;

    /// <summary>
    /// 手動設定解析度 (mm/pixel)
    /// </summary>
    public void SetResolution(double mmPerPixel)
    {
        if (mmPerPixel <= 0)
            throw new ArgumentException("解析度必須大於 0");

        Resolution = mmPerPixel;
        IsCalibrated = true;
        CalibrationMethod = $"手動設定: {mmPerPixel:F4} mm/pixel";
        ResolutionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 設定解析度 (pixel/mm)
    /// </summary>
    public void SetResolutionPixelPerMm(double pixelPerMm)
    {
        if (pixelPerMm <= 0)
            throw new ArgumentException("解析度必須大於 0");

        Resolution = 1.0 / pixelPerMm;
        IsCalibrated = true;
        CalibrationMethod = $"手動設定: {pixelPerMm:F2} pixel/mm";
        ResolutionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 使用標準圓校正
    /// </summary>
    public void CalibrateWithCircle(double measuredDiameterPx, double actualDiameterMm)
    {
        if (measuredDiameterPx <= 0 || actualDiameterMm <= 0)
            throw new ArgumentException("直徑必須大於 0");

        Resolution = actualDiameterMm / measuredDiameterPx;
        IsCalibrated = true;
        CalibrationMethod = $"標準圓校正: {measuredDiameterPx:F2}px = {actualDiameterMm:F3}mm";
        ResolutionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 使用標準距離校正
    /// </summary>
    public void CalibrateWithDistance(double measuredDistancePx, double actualDistanceMm)
    {
        if (measuredDistancePx <= 0 || actualDistanceMm <= 0)
            throw new ArgumentException("距離必須大於 0");

        Resolution = actualDistanceMm / measuredDistancePx;
        IsCalibrated = true;
        CalibrationMethod = $"標準距離校正: {measuredDistancePx:F2}px = {actualDistanceMm:F3}mm";
        ResolutionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 像素轉毫米
    /// </summary>
    public double PixelsToMm(double pixels) => pixels * Resolution;

    /// <summary>
    /// 毫米轉像素
    /// </summary>
    public double MmToPixels(double mm) => mm / Resolution;

    /// <summary>
    /// 重置校正
    /// </summary>
    public void Reset()
    {
        Resolution = 1.0;
        IsCalibrated = false;
        CalibrationMethod = "未校正";
        ResolutionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 取得解析度字串
    /// </summary>
    public string GetResolutionString()
    {
        if (!IsCalibrated)
            return "未校正 (1 px = 1 單位)";
        return $"{Resolution:F4} mm/pixel ({1 / Resolution:F2} pixel/mm)";
    }
}
