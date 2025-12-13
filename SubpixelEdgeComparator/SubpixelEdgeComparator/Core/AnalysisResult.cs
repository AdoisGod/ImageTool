namespace SubpixelEdgeComparator.Core;

/// <summary>
/// 儲存單一亞像素檢測方法的分析結果
/// </summary>
public class AnalysisResult
{
    /// <summary>
    /// 演算法名稱
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// 檢測到的邊緣位置（像素），null 表示檢測失敗
    /// </summary>
    public double? Position { get; set; }

    /// <summary>
    /// 與真實位置的誤差（僅合成影像時有效）
    /// </summary>
    public double? Error { get; set; }

    /// <summary>
    /// 計算耗時（毫秒）
    /// </summary>
    public double ElapsedMs { get; set; }

    /// <summary>
    /// 擬合品質（R²），僅擬合類演算法有效
    /// </summary>
    public double? RSquared { get; set; }

    /// <summary>
    /// 是否成功完成分析
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 錯誤訊息（如果失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 合成影像的參數設定
/// </summary>
public class SyntheticImageParams
{
    /// <summary>
    /// 影像寬度（像素）
    /// </summary>
    public int Width { get; set; } = 200;

    /// <summary>
    /// 影像高度（像素）
    /// </summary>
    public int Height { get; set; } = 200;

    /// <summary>
    /// 真實邊緣位置（像素）
    /// </summary>
    public double EdgePosition { get; set; } = 100.0;

    /// <summary>
    /// 左側灰階值 (0-255)
    /// </summary>
    public int LeftGray { get; set; } = 50;

    /// <summary>
    /// 右側灰階值 (0-255)
    /// </summary>
    public int RightGray { get; set; } = 200;

    /// <summary>
    /// 模糊程度 (高斯標準差)
    /// </summary>
    public double BlurSigma { get; set; } = 1.5;

    /// <summary>
    /// 是否加入雜訊
    /// </summary>
    public bool AddNoise { get; set; } = false;

    /// <summary>
    /// 雜訊標準差
    /// </summary>
    public double NoiseSigma { get; set; } = 5.0;
}

/// <summary>
/// 完整分析結果集合
/// </summary>
public class FullAnalysisResult
{
    /// <summary>
    /// 灰階剖面數據
    /// </summary>
    public double[] GrayProfile { get; set; } = Array.Empty<double>();

    /// <summary>
    /// 梯度剖面數據
    /// </summary>
    public double[] GradientProfile { get; set; } = Array.Empty<double>();

    /// <summary>
    /// 各演算法的分析結果
    /// </summary>
    public List<AnalysisResult> Results { get; set; } = new();

    /// <summary>
    /// 真實邊緣位置（僅合成影像時有效）
    /// </summary>
    public double? TrueEdgePosition { get; set; }
}
