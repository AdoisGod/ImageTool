using OpenCvSharp;

namespace VisionVerificationToolkit.Processing.Steps;

/// <summary>直方圖等化</summary>
public class HistogramEqualizeStep : PipelineStepBase
{
    public override string Name => "直方圖等化";
    public override string Category => "增強";

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.EqualizeHist(input, output);
        return output;
    }

    public override string GetParameterDescription() => "無參數";

    public override IPipelineStep Clone() => new HistogramEqualizeStep { IsEnabled = IsEnabled };
}

/// <summary>CLAHE 自適應直方圖等化</summary>
public class CLAHEStep : PipelineStepBase
{
    public override string Name => "CLAHE";
    public override string Category => "增強";

    public double ClipLimit { get; set; } = 2.0;
    public int TileSize { get; set; } = 8;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var clahe = Cv2.CreateCLAHE(ClipLimit, new OpenCvSharp.Size(TileSize, TileSize));
        clahe.Apply(input, output);
        return output;
    }

    public override string GetParameterDescription() => $"clipLimit={ClipLimit:F1}, tile={TileSize}";

    public override IPipelineStep Clone() => new CLAHEStep { ClipLimit = ClipLimit, TileSize = TileSize, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["ClipLimit"] = ClipLimit;
        data["TileSize"] = TileSize;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("ClipLimit", out var cl)) ClipLimit = Convert.ToDouble(cl);
        if (data.TryGetValue("TileSize", out var ts)) TileSize = Convert.ToInt32(ts);
    }
}

/// <summary>銳化</summary>
public class SharpenStep : PipelineStepBase
{
    public override string Name => "銳化";
    public override string Category => "增強";

    public double Strength { get; set; } = 1.0;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var blurred = new Mat();
        Cv2.GaussianBlur(input, blurred, new OpenCvSharp.Size(0, 0), 3);
        Cv2.AddWeighted(input, 1.0 + Strength, blurred, -Strength, 0, output);
        return output;
    }

    public override string GetParameterDescription() => $"強度={Strength:F1}";

    public override IPipelineStep Clone() => new SharpenStep { Strength = Strength, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Strength"] = Strength;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Strength", out var s)) Strength = Convert.ToDouble(s);
    }
}

/// <summary>對比度拉伸</summary>
public class ContrastStretchStep : PipelineStepBase
{
    public override string Name => "對比度拉伸";
    public override string Category => "增強";

    public double Alpha { get; set; } = 1.5;
    public double Beta { get; set; } = 0;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        input.ConvertTo(output, -1, Alpha, Beta);
        return output;
    }

    public override string GetParameterDescription() => $"α={Alpha:F2}, β={Beta:F0}";

    public override IPipelineStep Clone() => new ContrastStretchStep { Alpha = Alpha, Beta = Beta, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Alpha"] = Alpha;
        data["Beta"] = Beta;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Alpha", out var a)) Alpha = Convert.ToDouble(a);
        if (data.TryGetValue("Beta", out var b)) Beta = Convert.ToDouble(b);
    }
}

/// <summary>Gamma 校正</summary>
public class GammaCorrectionStep : PipelineStepBase
{
    public override string Name => "Gamma校正";
    public override string Category => "增強";

    public double Gamma { get; set; } = 1.0;

    private byte[]? _lookupTable;

    public override Mat Process(Mat input)
    {
        if (_lookupTable == null || Math.Abs(Gamma - _lastGamma) > 0.001)
        {
            BuildLookupTable();
        }

        var output = new Mat();
        using var lut = new Mat(1, 256, MatType.CV_8U, _lookupTable);
        Cv2.LUT(input, lut, output);
        return output;
    }

    private double _lastGamma;

    private void BuildLookupTable()
    {
        _lookupTable = new byte[256];
        double invGamma = 1.0 / Gamma;
        for (int i = 0; i < 256; i++)
        {
            _lookupTable[i] = (byte)Math.Min(255, Math.Max(0, Math.Pow(i / 255.0, invGamma) * 255));
        }
        _lastGamma = Gamma;
    }

    public override string GetParameterDescription() => $"γ={Gamma:F2}";

    public override IPipelineStep Clone() => new GammaCorrectionStep { Gamma = Gamma, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Gamma"] = Gamma;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Gamma", out var g)) Gamma = Convert.ToDouble(g);
    }
}

/// <summary>亮度調整</summary>
public class BrightnessStep : PipelineStepBase
{
    public override string Name => "亮度調整";
    public override string Category => "增強";

    public int Offset { get; set; } = 0;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        input.ConvertTo(output, -1, 1, Offset);
        return output;
    }

    public override string GetParameterDescription() => $"偏移={Offset:+0;-0;0}";

    public override IPipelineStep Clone() => new BrightnessStep { Offset = Offset, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Offset"] = Offset;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Offset", out var o)) Offset = Convert.ToInt32(o);
    }
}

/// <summary>反轉</summary>
public class InvertStep : PipelineStepBase
{
    public override string Name => "反轉";
    public override string Category => "增強";

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.BitwiseNot(input, output);
        return output;
    }

    public override string GetParameterDescription() => "無參數";

    public override IPipelineStep Clone() => new InvertStep { IsEnabled = IsEnabled };
}
