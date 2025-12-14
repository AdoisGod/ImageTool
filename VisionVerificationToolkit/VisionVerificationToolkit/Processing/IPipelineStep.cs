using OpenCvSharp;

namespace VisionVerificationToolkit.Processing;

/// <summary>
/// 管線步驟介面
/// </summary>
public interface IPipelineStep
{
    /// <summary>步驟名稱</summary>
    string Name { get; }

    /// <summary>步驟類別</summary>
    string Category { get; }

    /// <summary>是否啟用</summary>
    bool IsEnabled { get; set; }

    /// <summary>執行處理</summary>
    Mat Process(Mat input);

    /// <summary>取得參數描述</summary>
    string GetParameterDescription();

    /// <summary>複製步驟</summary>
    IPipelineStep Clone();

    /// <summary>序列化為字典</summary>
    Dictionary<string, object> Serialize();

    /// <summary>從字典反序列化</summary>
    void Deserialize(Dictionary<string, object> data);
}

/// <summary>
/// 管線步驟基底類別
/// </summary>
public abstract class PipelineStepBase : IPipelineStep
{
    public abstract string Name { get; }
    public abstract string Category { get; }
    public bool IsEnabled { get; set; } = true;

    public abstract Mat Process(Mat input);
    public abstract string GetParameterDescription();
    public abstract IPipelineStep Clone();

    public virtual Dictionary<string, object> Serialize()
    {
        return new Dictionary<string, object>
        {
            ["Type"] = GetType().Name,
            ["IsEnabled"] = IsEnabled
        };
    }

    public virtual void Deserialize(Dictionary<string, object> data)
    {
        if (data.TryGetValue("IsEnabled", out var enabled))
            IsEnabled = Convert.ToBoolean(enabled);
    }
}
