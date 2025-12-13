using OpenCvSharp;
using VisionMeasurementToolkit.Core;
using VisionMeasurementToolkit.Detection;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 找線工具
/// </summary>
public class LineFinderTool : ToolBase
{
    private Mat? _image;
    private PointF? _startPoint;
    private PointF? _currentPoint;
    private bool _isDragging;

    public override string Name => "找線";
    public override string Description => "沿搜尋線檢測邊緣並擬合直線";

    public LineDetector.CaliperParams CaliperParams { get; } = new();

    public void SetImage(Mat image)
    {
        _image = image;
    }

    public override void OnMouseDown(MouseEventArgs e, PointF imagePoint)
    {
        if (e.Button == MouseButtons.Left)
        {
            _startPoint = imagePoint;
            _isDragging = true;
        }
    }

    public override void OnMouseMove(MouseEventArgs e, PointF imagePoint)
    {
        if (_isDragging)
        {
            _currentPoint = imagePoint;
        }
    }

    public override void OnMouseUp(MouseEventArgs e, PointF imagePoint)
    {
        if (_isDragging && _startPoint.HasValue && _image != null)
        {
            _currentPoint = imagePoint;
            _isDragging = false;

            double length = GeometryMath.Distance(_startPoint.Value, _currentPoint.Value);
            if (length > 20)
            {
                DetectLine();
            }

            _startPoint = null;
            _currentPoint = null;
        }
    }

    private void DetectLine()
    {
        if (_image == null || !_startPoint.HasValue || !_currentPoint.HasValue) return;

        try
        {
            var result = LineDetector.DetectLine(_image, _startPoint.Value, _currentPoint.Value, CaliperParams);

            if (result != null)
            {
                RaiseMeasurementCompleted(result);
            }
            else
            {
                MessageBox.Show("有效邊緣點不足，無法擬合直線", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"檢測失敗：{ex.Message}", "錯誤",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public override void OnPaint(Graphics g, Func<PointF, PointF> toScreen)
    {
        if (_isDragging && _startPoint.HasValue && _currentPoint.HasValue)
        {
            var p1 = toScreen(_startPoint.Value);
            var p2 = toScreen(_currentPoint.Value);

            using var pen = new Pen(Color.Yellow, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawLine(pen, p1, p2);

            // 繪製垂直方向指示
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len > 10)
            {
                double perpX = -dy / len * 15;
                double perpY = dx / len * 15;
                var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                using var perpPen = new Pen(Color.Lime, 1);
                g.DrawLine(perpPen, mid.X - (float)perpX, mid.Y - (float)perpY,
                    mid.X + (float)perpX, mid.Y + (float)perpY);
            }
        }
    }

    public override void Cancel()
    {
        _isDragging = false;
        _startPoint = null;
        _currentPoint = null;
    }
}
