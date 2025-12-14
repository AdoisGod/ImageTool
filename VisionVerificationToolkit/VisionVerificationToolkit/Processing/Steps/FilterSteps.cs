using OpenCvSharp;

namespace VisionVerificationToolkit.Processing.Steps;

/// <summary>高斯濾波</summary>
public class GaussianBlurStep : PipelineStepBase
{
    public override string Name => "高斯濾波";
    public override string Category => "濾波";

    public int KernelSize { get; set; } = 5;
    public double SigmaX { get; set; } = 1.5;
    public double SigmaY { get; set; } = 1.5;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.GaussianBlur(input, output, new OpenCvSharp.Size(KernelSize, KernelSize), SigmaX, SigmaY);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, σX={SigmaX:F1}, σY={SigmaY:F1}";

    public override IPipelineStep Clone() => new GaussianBlurStep { KernelSize = KernelSize, SigmaX = SigmaX, SigmaY = SigmaY, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["KernelSize"] = KernelSize;
        data["SigmaX"] = SigmaX;
        data["SigmaY"] = SigmaY;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("KernelSize", out var k)) KernelSize = Convert.ToInt32(k);
        if (data.TryGetValue("SigmaX", out var sx)) SigmaX = Convert.ToDouble(sx);
        if (data.TryGetValue("SigmaY", out var sy)) SigmaY = Convert.ToDouble(sy);
    }
}

/// <summary>中值濾波</summary>
public class MedianBlurStep : PipelineStepBase
{
    public override string Name => "中值濾波";
    public override string Category => "濾波";

    public int KernelSize { get; set; } = 5;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.MedianBlur(input, output, KernelSize);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}";

    public override IPipelineStep Clone() => new MedianBlurStep { KernelSize = KernelSize, IsEnabled = IsEnabled };

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

/// <summary>雙邊濾波</summary>
public class BilateralFilterStep : PipelineStepBase
{
    public override string Name => "雙邊濾波";
    public override string Category => "濾波";

    public int D { get; set; } = 9;
    public double SigmaColor { get; set; } = 75;
    public double SigmaSpace { get; set; } = 75;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.BilateralFilter(input, output, D, SigmaColor, SigmaSpace);
        return output;
    }

    public override string GetParameterDescription() => $"d={D}, σC={SigmaColor:F0}, σS={SigmaSpace:F0}";

    public override IPipelineStep Clone() => new BilateralFilterStep { D = D, SigmaColor = SigmaColor, SigmaSpace = SigmaSpace, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["D"] = D;
        data["SigmaColor"] = SigmaColor;
        data["SigmaSpace"] = SigmaSpace;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("D", out var d)) D = Convert.ToInt32(d);
        if (data.TryGetValue("SigmaColor", out var sc)) SigmaColor = Convert.ToDouble(sc);
        if (data.TryGetValue("SigmaSpace", out var ss)) SigmaSpace = Convert.ToDouble(ss);
    }
}

/// <summary>均值濾波</summary>
public class MeanBlurStep : PipelineStepBase
{
    public override string Name => "均值濾波";
    public override string Category => "濾波";

    public int KernelSize { get; set; } = 5;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.Blur(input, output, new OpenCvSharp.Size(KernelSize, KernelSize));
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}";

    public override IPipelineStep Clone() => new MeanBlurStep { KernelSize = KernelSize, IsEnabled = IsEnabled };

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

/// <summary>方框濾波</summary>
public class BoxFilterStep : PipelineStepBase
{
    public override string Name => "方框濾波";
    public override string Category => "濾波";

    public int KernelSize { get; set; } = 5;
    public bool Normalize { get; set; } = true;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.BoxFilter(input, output, -1, new OpenCvSharp.Size(KernelSize, KernelSize), normalize: Normalize);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 正規化={Normalize}";

    public override IPipelineStep Clone() => new BoxFilterStep { KernelSize = KernelSize, Normalize = Normalize, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["KernelSize"] = KernelSize;
        data["Normalize"] = Normalize;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("KernelSize", out var k)) KernelSize = Convert.ToInt32(k);
        if (data.TryGetValue("Normalize", out var n)) Normalize = Convert.ToBoolean(n);
    }
}
