using OpenCvSharp;

namespace VisionVerificationToolkit.Processing.Steps;

/// <summary>Canny 邊緣檢測</summary>
public class CannyStep : PipelineStepBase
{
    public override string Name => "Canny";
    public override string Category => "邊緣檢測";

    public double Threshold1 { get; set; } = 50;
    public double Threshold2 { get; set; } = 150;
    public int ApertureSize { get; set; } = 3;
    public bool L2Gradient { get; set; } = false;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.Canny(input, output, Threshold1, Threshold2, ApertureSize, L2Gradient);
        return output;
    }

    public override string GetParameterDescription() => $"低={Threshold1:F0}, 高={Threshold2:F0}, 核={ApertureSize}";

    public override IPipelineStep Clone() => new CannyStep { Threshold1 = Threshold1, Threshold2 = Threshold2, ApertureSize = ApertureSize, L2Gradient = L2Gradient, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Threshold1"] = Threshold1;
        data["Threshold2"] = Threshold2;
        data["ApertureSize"] = ApertureSize;
        data["L2Gradient"] = L2Gradient;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Threshold1", out var t1)) Threshold1 = Convert.ToDouble(t1);
        if (data.TryGetValue("Threshold2", out var t2)) Threshold2 = Convert.ToDouble(t2);
        if (data.TryGetValue("ApertureSize", out var a)) ApertureSize = Convert.ToInt32(a);
        if (data.TryGetValue("L2Gradient", out var l2)) L2Gradient = Convert.ToBoolean(l2);
    }
}

/// <summary>Sobel 邊緣檢測</summary>
public class SobelStep : PipelineStepBase
{
    public override string Name => "Sobel";
    public override string Category => "邊緣檢測";

    public SobelDirection Direction { get; set; } = SobelDirection.Both;
    public int KernelSize { get; set; } = 3;

    public enum SobelDirection { X, Y, Both }

    public override Mat Process(Mat input)
    {
        var output = new Mat();

        switch (Direction)
        {
            case SobelDirection.X:
                Cv2.Sobel(input, output, MatType.CV_8U, 1, 0, KernelSize);
                break;
            case SobelDirection.Y:
                Cv2.Sobel(input, output, MatType.CV_8U, 0, 1, KernelSize);
                break;
            default:
                using (var gradX = new Mat())
                using (var gradY = new Mat())
                using (var absX = new Mat())
                using (var absY = new Mat())
                {
                    Cv2.Sobel(input, gradX, MatType.CV_16S, 1, 0, KernelSize);
                    Cv2.Sobel(input, gradY, MatType.CV_16S, 0, 1, KernelSize);
                    Cv2.ConvertScaleAbs(gradX, absX);
                    Cv2.ConvertScaleAbs(gradY, absY);
                    Cv2.AddWeighted(absX, 0.5, absY, 0.5, 0, output);
                }
                break;
        }
        return output;
    }

    public override string GetParameterDescription() => $"方向={Direction}, 核={KernelSize}";

    public override IPipelineStep Clone() => new SobelStep { Direction = Direction, KernelSize = KernelSize, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Direction"] = (int)Direction;
        data["KernelSize"] = KernelSize;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Direction", out var d)) Direction = (SobelDirection)Convert.ToInt32(d);
        if (data.TryGetValue("KernelSize", out var k)) KernelSize = Convert.ToInt32(k);
    }
}

/// <summary>Laplacian 邊緣檢測</summary>
public class LaplacianStep : PipelineStepBase
{
    public override string Name => "Laplacian";
    public override string Category => "邊緣檢測";

    public int KernelSize { get; set; } = 3;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var temp = new Mat();
        Cv2.Laplacian(input, temp, MatType.CV_16S, KernelSize);
        Cv2.ConvertScaleAbs(temp, output);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}";

    public override IPipelineStep Clone() => new LaplacianStep { KernelSize = KernelSize, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["KernelSize"] = KernelSize;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("KernelSize", out var k)) KernelSize = Convert.ToInt32(k);
    }
}

/// <summary>形態學邊緣</summary>
public class MorphEdgeStep : PipelineStepBase
{
    public override string Name => "形態學邊緣";
    public override string Category => "邊緣檢測";

    public int KernelSize { get; set; } = 3;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(KernelSize, KernelSize));
        Cv2.MorphologyEx(input, output, MorphTypes.Gradient, kernel);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}";

    public override IPipelineStep Clone() => new MorphEdgeStep { KernelSize = KernelSize, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["KernelSize"] = KernelSize;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("KernelSize", out var k)) KernelSize = Convert.ToInt32(k);
    }
}

/// <summary>通用邊緣檢測 (灰度差閾值)</summary>
public class GenericEdgeStep : PipelineStepBase
{
    public override string Name => "通用邊緣";
    public override string Category => "邊緣檢測";

    public int GrayThreshold { get; set; } = 30;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
        using var gradient = new Mat();
        Cv2.MorphologyEx(input, gradient, MorphTypes.Gradient, kernel);
        Cv2.Threshold(gradient, output, GrayThreshold, 255, ThresholdTypes.Binary);
        return output;
    }

    public override string GetParameterDescription() => $"灰度差閾值={GrayThreshold}";

    public override IPipelineStep Clone() => new GenericEdgeStep { GrayThreshold = GrayThreshold, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["GrayThreshold"] = GrayThreshold;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("GrayThreshold", out var gt)) GrayThreshold = Convert.ToInt32(gt);
    }
}
