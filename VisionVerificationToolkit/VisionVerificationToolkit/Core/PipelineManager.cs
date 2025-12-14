using OpenCvSharp;
using VisionVerificationToolkit.Processing;
using System.Text.Json;

namespace VisionVerificationToolkit.Core;

/// <summary>
/// 管線管理器 - 管理處理管線
/// </summary>
public class PipelineManager
{
    private readonly List<IPipelineStep> _steps = new();

    public event EventHandler? PipelineChanged;
    public event EventHandler<PipelineStepEventArgs>? StepExecuted;

    public IReadOnlyList<IPipelineStep> Steps => _steps.AsReadOnly();
    public int Count => _steps.Count;

    public void AddStep(IPipelineStep step)
    {
        _steps.Add(step);
        PipelineChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InsertStep(int index, IPipelineStep step)
    {
        _steps.Insert(index, step);
        PipelineChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveStep(int index)
    {
        if (index >= 0 && index < _steps.Count)
        {
            _steps.RemoveAt(index);
            PipelineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RemoveStep(IPipelineStep step)
    {
        if (_steps.Remove(step))
        {
            PipelineChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void MoveStep(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= _steps.Count) return;
        if (toIndex < 0 || toIndex >= _steps.Count) return;
        if (fromIndex == toIndex) return;

        var step = _steps[fromIndex];
        _steps.RemoveAt(fromIndex);
        _steps.Insert(toIndex, step);
        PipelineChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _steps.Clear();
        PipelineChanged?.Invoke(this, EventArgs.Empty);
    }

    public Mat Execute(Mat input)
    {
        var current = input.Clone();

        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            if (!step.IsEnabled) continue;

            try
            {
                var startTime = DateTime.Now;
                var result = step.Process(current);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                if (result != current)
                {
                    current.Dispose();
                    current = result;
                }

                StepExecuted?.Invoke(this, new PipelineStepEventArgs(step, i, elapsed, true));
            }
            catch (Exception ex)
            {
                StepExecuted?.Invoke(this, new PipelineStepEventArgs(step, i, 0, false, ex.Message));
            }
        }

        return current;
    }

    public void SaveToFile(string path)
    {
        var data = _steps.Select(s => s.Serialize()).ToList();
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public void LoadFromFile(string path, Func<string, IPipelineStep?> stepFactory)
    {
        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);

        if (data == null) return;

        _steps.Clear();

        foreach (var stepData in data)
        {
            if (!stepData.TryGetValue("Type", out var typeObj)) continue;
            var typeName = typeObj?.ToString();
            if (string.IsNullOrEmpty(typeName)) continue;

            var step = stepFactory(typeName);
            if (step != null)
            {
                // Convert JsonElement to proper types
                var converted = new Dictionary<string, object>();
                foreach (var kvp in stepData)
                {
                    if (kvp.Value is JsonElement je)
                    {
                        converted[kvp.Key] = je.ValueKind switch
                        {
                            JsonValueKind.Number => je.TryGetInt32(out var i) ? i : je.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.String => je.GetString() ?? "",
                            _ => kvp.Value
                        };
                    }
                    else
                    {
                        converted[kvp.Key] = kvp.Value;
                    }
                }
                step.Deserialize(converted);
                _steps.Add(step);
            }
        }

        PipelineChanged?.Invoke(this, EventArgs.Empty);
    }

    public IPipelineStep? GetStep(int index)
    {
        if (index >= 0 && index < _steps.Count)
            return _steps[index];
        return null;
    }
}

public class PipelineStepEventArgs : EventArgs
{
    public IPipelineStep Step { get; }
    public int Index { get; }
    public double ElapsedMs { get; }
    public bool Success { get; }
    public string? ErrorMessage { get; }

    public PipelineStepEventArgs(IPipelineStep step, int index, double elapsedMs, bool success, string? errorMessage = null)
    {
        Step = step;
        Index = index;
        ElapsedMs = elapsedMs;
        Success = success;
        ErrorMessage = errorMessage;
    }
}
