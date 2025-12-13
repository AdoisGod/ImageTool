using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 工具介面
/// </summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    Cursor ToolCursor { get; }

    void OnMouseDown(MouseEventArgs e, PointF imagePoint);
    void OnMouseMove(MouseEventArgs e, PointF imagePoint);
    void OnMouseUp(MouseEventArgs e, PointF imagePoint);
    void OnPaint(Graphics g, Func<PointF, PointF> toScreen);
    void Cancel();

    event EventHandler<MeasurementResult>? MeasurementCompleted;
}

/// <summary>
/// 工具基底類別
/// </summary>
public abstract class ToolBase : ITool
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual Cursor ToolCursor => Cursors.Cross;

    public event EventHandler<MeasurementResult>? MeasurementCompleted;

    protected void RaiseMeasurementCompleted(MeasurementResult result)
    {
        MeasurementCompleted?.Invoke(this, result);
    }

    public abstract void OnMouseDown(MouseEventArgs e, PointF imagePoint);
    public abstract void OnMouseMove(MouseEventArgs e, PointF imagePoint);
    public abstract void OnMouseUp(MouseEventArgs e, PointF imagePoint);
    public abstract void OnPaint(Graphics g, Func<PointF, PointF> toScreen);
    public abstract void Cancel();
}
