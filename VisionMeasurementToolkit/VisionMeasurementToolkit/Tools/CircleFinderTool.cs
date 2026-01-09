using OpenCvSharp;
using VisionMeasurementToolkit.Core;
using VisionMeasurementToolkit.Detection;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 找圓工具
/// </summary>
public class CircleFinderTool : ToolBase
{
    public enum DetectionMethod { Hough, EdgeFit }

    private Mat? _image;
    private PointF? _startPoint;
    private PointF? _currentPoint;
    private bool _isDragging;

    public override string Name => "找圓";
    public override string Description => "在 ROI 區域內檢測圓形";

    public DetectionMethod Method { get; set; } = DetectionMethod.EdgeFit;
    public CircleDetector.HoughParams HoughParams { get; } = new();
    public CircleDetector.EdgeFitParams EdgeFitParams { get; } = new();

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

            // 計算 ROI
            var roi = GetRoiRectangle();
            if (roi.Width > 10 && roi.Height > 10)
            {
                DetectCircle(roi);
            }

            _startPoint = null;
            _currentPoint = null;
        }
    }

    private Rectangle GetRoiRectangle()
    {
        if (!_startPoint.HasValue || !_currentPoint.HasValue)
            return Rectangle.Empty;

        int x = (int)Math.Min(_startPoint.Value.X, _currentPoint.Value.X);
        int y = (int)Math.Min(_startPoint.Value.Y, _currentPoint.Value.Y);
        int w = (int)Math.Abs(_currentPoint.Value.X - _startPoint.Value.X);
        int h = (int)Math.Abs(_currentPoint.Value.Y - _startPoint.Value.Y);

        return new Rectangle(x, y, w, h);
    }

    private void DetectCircle(Rectangle roi)
    {
        if (_image == null) return;

        try
        {
            CircleResult? result = null;

            if (Method == DetectionMethod.Hough)
            {
                var results = CircleDetector.DetectHough(_image, roi, HoughParams);
                if (results.Count > 0)
                    result = results[0];
            }
            else
            {
                result = CircleDetector.DetectEdgeFit(_image, roi, EdgeFitParams);
            }

            if (result != null)
            {
                RaiseMeasurementCompleted(result);
            }
            else
            {
                MessageBox.Show("未檢測到圓形，請調整 ROI 或參數", "提示",
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

            using var pen = new Pen(Color.Cyan, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            float x = Math.Min(p1.X, p2.X);
            float y = Math.Min(p1.Y, p2.Y);
            float w = Math.Abs(p2.X - p1.X);
            float h = Math.Abs(p2.Y - p1.Y);
            g.DrawRectangle(pen, x, y, w, h);
        }
    }

    public override void Cancel()
    {
        _isDragging = false;
        _startPoint = null;
        _currentPoint = null;
    }
}
