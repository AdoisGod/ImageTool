using VisionVerificationToolkit.Processing.Steps;

namespace VisionVerificationToolkit.Processing;

/// <summary>
/// 步驟工廠 - 建立管線步驟實例
/// </summary>
public static class StepFactory
{
    public static readonly Dictionary<string, Func<IPipelineStep>> StepCreators = new()
    {
        // 濾波
        ["GaussianBlurStep"] = () => new GaussianBlurStep(),
        ["MedianBlurStep"] = () => new MedianBlurStep(),
        ["BilateralFilterStep"] = () => new BilateralFilterStep(),
        ["MeanBlurStep"] = () => new MeanBlurStep(),
        ["BoxFilterStep"] = () => new BoxFilterStep(),

        // 二值化
        ["ThresholdStep"] = () => new ThresholdStep(),
        ["OtsuThresholdStep"] = () => new OtsuThresholdStep(),
        ["AdaptiveThresholdStep"] = () => new AdaptiveThresholdStep(),
        ["DoubleThresholdStep"] = () => new DoubleThresholdStep(),

        // 形態學
        ["DilateStep"] = () => new DilateStep(),
        ["ErodeStep"] = () => new ErodeStep(),
        ["OpeningStep"] = () => new OpeningStep(),
        ["ClosingStep"] = () => new ClosingStep(),
        ["MorphGradientStep"] = () => new MorphGradientStep(),
        ["TopHatStep"] = () => new TopHatStep(),

        // 邊緣檢測
        ["CannyStep"] = () => new CannyStep(),
        ["SobelStep"] = () => new SobelStep(),
        ["LaplacianStep"] = () => new LaplacianStep(),
        ["MorphEdgeStep"] = () => new MorphEdgeStep(),
        ["GenericEdgeStep"] = () => new GenericEdgeStep(),

        // 增強
        ["HistogramEqualizeStep"] = () => new HistogramEqualizeStep(),
        ["CLAHEStep"] = () => new CLAHEStep(),
        ["SharpenStep"] = () => new SharpenStep(),
        ["ContrastStretchStep"] = () => new ContrastStretchStep(),
        ["GammaCorrectionStep"] = () => new GammaCorrectionStep(),
        ["BrightnessStep"] = () => new BrightnessStep(),
        ["InvertStep"] = () => new InvertStep(),
    };

    public static readonly Dictionary<string, List<(string Name, string TypeName)>> StepsByCategory = new()
    {
        ["濾波"] = new()
        {
            ("高斯濾波", "GaussianBlurStep"),
            ("中值濾波", "MedianBlurStep"),
            ("雙邊濾波", "BilateralFilterStep"),
            ("均值濾波", "MeanBlurStep"),
            ("方框濾波", "BoxFilterStep"),
        },
        ["二值化"] = new()
        {
            ("固定閾值", "ThresholdStep"),
            ("Otsu", "OtsuThresholdStep"),
            ("自適應閾值", "AdaptiveThresholdStep"),
            ("雙閾值", "DoubleThresholdStep"),
        },
        ["形態學"] = new()
        {
            ("膨脹", "DilateStep"),
            ("侵蝕", "ErodeStep"),
            ("開運算", "OpeningStep"),
            ("閉運算", "ClosingStep"),
            ("形態學梯度", "MorphGradientStep"),
            ("Top-Hat", "TopHatStep"),
        },
        ["邊緣檢測"] = new()
        {
            ("Canny", "CannyStep"),
            ("Sobel", "SobelStep"),
            ("Laplacian", "LaplacianStep"),
            ("形態學邊緣", "MorphEdgeStep"),
            ("通用邊緣", "GenericEdgeStep"),
        },
        ["增強"] = new()
        {
            ("直方圖等化", "HistogramEqualizeStep"),
            ("CLAHE", "CLAHEStep"),
            ("銳化", "SharpenStep"),
            ("對比度拉伸", "ContrastStretchStep"),
            ("Gamma校正", "GammaCorrectionStep"),
            ("亮度調整", "BrightnessStep"),
            ("反轉", "InvertStep"),
        },
    };

    public static IPipelineStep? Create(string typeName)
    {
        if (StepCreators.TryGetValue(typeName, out var creator))
            return creator();
        return null;
    }

    public static IEnumerable<string> GetAllCategories() => StepsByCategory.Keys;

    public static IEnumerable<(string Name, string TypeName)> GetStepsByCategory(string category)
    {
        if (StepsByCategory.TryGetValue(category, out var steps))
            return steps;
        return Enumerable.Empty<(string, string)>();
    }
}
