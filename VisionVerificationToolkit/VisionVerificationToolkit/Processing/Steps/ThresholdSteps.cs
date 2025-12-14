using OpenCvSharp;

namespace VisionVerificationToolkit.Processing.Steps;

/// <summary>固定閾值二值化</summary>
public class ThresholdStep : PipelineStepBase
{
    public override string Name => "固定閾值";
    public override string Category => "二值化";

    public double ThresholdValue { get; set; } = 127;
    public ThresholdTypes ThresholdType { get; set; } = ThresholdTypes.Binary;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.Threshold(input, output, ThresholdValue, 255, ThresholdType);
        return output;
    }

    public override string GetParameterDescription() => $"閾值={ThresholdValue:F0}, 類型={ThresholdType}";

    public override IPipelineStep Clone() => new ThresholdStep { ThresholdValue = ThresholdValue, ThresholdType = ThresholdType, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["ThresholdValue"] = ThresholdValue;
        data["ThresholdType"] = (int)ThresholdType;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("ThresholdValue", out var tv)) ThresholdValue = Convert.ToDouble(tv);
        if (data.TryGetValue("ThresholdType", out var tt)) ThresholdType = (ThresholdTypes)Convert.ToInt32(tt);
    }
}

/// <summary>Otsu 自動二值化</summary>
public class OtsuThresholdStep : PipelineStepBase
{
    public override string Name => "Otsu";
    public override string Category => "二值化";

    public double CalculatedThreshold { get; private set; }

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        CalculatedThreshold = Cv2.Threshold(input, output, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
        return output;
    }

    public override string GetParameterDescription() => $"自動閾值={CalculatedThreshold:F0}";

    public override IPipelineStep Clone() => new OtsuThresholdStep { IsEnabled = IsEnabled };
}

/// <summary>自適應閾值</summary>
public class AdaptiveThresholdStep : PipelineStepBase
{
    public override string Name => "自適應閾值";
    public override string Category => "二值化";

    public AdaptiveThresholdTypes Method { get; set; } = AdaptiveThresholdTypes.GaussianC;
    public int BlockSize { get; set; } = 11;
    public double C { get; set; } = 2;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.AdaptiveThreshold(input, output, 255, Method, ThresholdTypes.Binary, BlockSize, C);
        return output;
    }

    public override string GetParameterDescription() => $"方法={Method}, 區塊={BlockSize}, C={C}";

    public override IPipelineStep Clone() => new AdaptiveThresholdStep { Method = Method, BlockSize = BlockSize, C = C, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["Method"] = (int)Method;
        data["BlockSize"] = BlockSize;
        data["C"] = C;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("Method", out var m)) Method = (AdaptiveThresholdTypes)Convert.ToInt32(m);
        if (data.TryGetValue("BlockSize", out var bs)) BlockSize = Convert.ToInt32(bs);
        if (data.TryGetValue("C", out var c)) C = Convert.ToDouble(c);
    }
}

/// <summary>雙閾值</summary>
public class DoubleThresholdStep : PipelineStepBase
{
    public override string Name => "雙閾值";
    public override string Category => "二值化";

    public double LowThreshold { get; set; } = 50;
    public double HighThreshold { get; set; } = 200;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        Cv2.InRange(input, new Scalar(LowThreshold), new Scalar(HighThreshold), output);
        return output;
    }

    public override string GetParameterDescription() => $"低={LowThreshold:F0}, 高={HighThreshold:F0}";

    public override IPipelineStep Clone() => new DoubleThresholdStep { LowThreshold = LowThreshold, HighThreshold = HighThreshold, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["LowThreshold"] = LowThreshold;
        data["HighThreshold"] = HighThreshold;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("LowThreshold", out var lt)) LowThreshold = Convert.ToDouble(lt);
        if (data.TryGetValue("HighThreshold", out var ht)) HighThreshold = Convert.ToDouble(ht);
    }
}
