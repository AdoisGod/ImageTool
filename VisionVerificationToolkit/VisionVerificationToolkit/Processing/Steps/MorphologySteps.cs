using OpenCvSharp;

namespace VisionVerificationToolkit.Processing.Steps;

public enum StructuringElementShape
{
    Rectangle,
    Cross,
    Ellipse
}

/// <summary>形態學步驟基底</summary>
public abstract class MorphologyStepBase : PipelineStepBase
{
    public override string Category => "形態學";

    public int KernelSize { get; set; } = 3;
    public StructuringElementShape Shape { get; set; } = StructuringElementShape.Rectangle;
    public int Iterations { get; set; } = 1;

    protected Mat GetStructuringElement()
    {
        var shape = Shape switch
        {
            StructuringElementShape.Cross => MorphShapes.Cross,
            StructuringElementShape.Ellipse => MorphShapes.Ellipse,
            _ => MorphShapes.Rect
        };
        return Cv2.GetStructuringElement(shape, new OpenCvSharp.Size(KernelSize, KernelSize));
    }

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["KernelSize"] = KernelSize;
        data["Shape"] = (int)Shape;
        data["Iterations"] = Iterations;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("KernelSize", out var k)) KernelSize = Convert.ToInt32(k);
        if (data.TryGetValue("Shape", out var s)) Shape = (StructuringElementShape)Convert.ToInt32(s);
        if (data.TryGetValue("Iterations", out var i)) Iterations = Convert.ToInt32(i);
    }
}

/// <summary>膨脹</summary>
public class DilateStep : MorphologyStepBase
{
    public override string Name => "膨脹";

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = GetStructuringElement();
        Cv2.Dilate(input, output, kernel, iterations: Iterations);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 形狀={Shape}, 迭代={Iterations}";

    public override IPipelineStep Clone() => new DilateStep { KernelSize = KernelSize, Shape = Shape, Iterations = Iterations, IsEnabled = IsEnabled };
}

/// <summary>侵蝕</summary>
public class ErodeStep : MorphologyStepBase
{
    public override string Name => "侵蝕";

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = GetStructuringElement();
        Cv2.Erode(input, output, kernel, iterations: Iterations);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 形狀={Shape}, 迭代={Iterations}";

    public override IPipelineStep Clone() => new ErodeStep { KernelSize = KernelSize, Shape = Shape, Iterations = Iterations, IsEnabled = IsEnabled };
}

/// <summary>開運算</summary>
public class OpeningStep : MorphologyStepBase
{
    public override string Name => "開運算";

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = GetStructuringElement();
        Cv2.MorphologyEx(input, output, MorphTypes.Open, kernel, iterations: Iterations);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 形狀={Shape}, 迭代={Iterations}";

    public override IPipelineStep Clone() => new OpeningStep { KernelSize = KernelSize, Shape = Shape, Iterations = Iterations, IsEnabled = IsEnabled };
}

/// <summary>閉運算</summary>
public class ClosingStep : MorphologyStepBase
{
    public override string Name => "閉運算";

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = GetStructuringElement();
        Cv2.MorphologyEx(input, output, MorphTypes.Close, kernel, iterations: Iterations);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 形狀={Shape}, 迭代={Iterations}";

    public override IPipelineStep Clone() => new ClosingStep { KernelSize = KernelSize, Shape = Shape, Iterations = Iterations, IsEnabled = IsEnabled };
}

/// <summary>形態學梯度</summary>
public class MorphGradientStep : MorphologyStepBase
{
    public override string Name => "形態學梯度";

    public MorphGradientType GradientType { get; set; } = MorphGradientType.Standard;

    public enum MorphGradientType { Standard, Internal, External }

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = GetStructuringElement();

        switch (GradientType)
        {
            case MorphGradientType.Internal:
                using (var eroded = new Mat())
                {
                    Cv2.Erode(input, eroded, kernel);
                    Cv2.Subtract(input, eroded, output);
                }
                break;
            case MorphGradientType.External:
                using (var dilated = new Mat())
                {
                    Cv2.Dilate(input, dilated, kernel);
                    Cv2.Subtract(dilated, input, output);
                }
                break;
            default:
                Cv2.MorphologyEx(input, output, MorphTypes.Gradient, kernel);
                break;
        }
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 類型={GradientType}";

    public override IPipelineStep Clone() => new MorphGradientStep { KernelSize = KernelSize, Shape = Shape, GradientType = GradientType, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["GradientType"] = (int)GradientType;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("GradientType", out var gt)) GradientType = (MorphGradientType)Convert.ToInt32(gt);
    }
}

/// <summary>Top-Hat 變換</summary>
public class TopHatStep : MorphologyStepBase
{
    public override string Name => "Top-Hat";

    public bool IsBlackHat { get; set; } = false;

    public override Mat Process(Mat input)
    {
        var output = new Mat();
        using var kernel = GetStructuringElement();
        var type = IsBlackHat ? MorphTypes.BlackHat : MorphTypes.TopHat;
        Cv2.MorphologyEx(input, output, type, kernel);
        return output;
    }

    public override string GetParameterDescription() => $"核={KernelSize}, 類型={(IsBlackHat ? "Black-Hat" : "White Top-Hat")}";

    public override IPipelineStep Clone() => new TopHatStep { KernelSize = KernelSize, Shape = Shape, IsBlackHat = IsBlackHat, IsEnabled = IsEnabled };

    public override Dictionary<string, object> Serialize()
    {
        var data = base.Serialize();
        data["IsBlackHat"] = IsBlackHat;
        return data;
    }

    public override void Deserialize(Dictionary<string, object> data)
    {
        base.Deserialize(data);
        if (data.TryGetValue("IsBlackHat", out var bh)) IsBlackHat = Convert.ToBoolean(bh);
    }
}
