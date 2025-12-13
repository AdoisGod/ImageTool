using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 角度量測工具（三點定義）
/// </summary>
public class AngleTool : ToolBase
{
    private PointF? _point1;
    private PointF? _vertex;
    private PointF? _tempPoint;
    private int _clickCount;

    public override string Name => "角度";
    public override string Description => "三點定義角度（端點-頂點-端點）";

    public override void OnMouseDown(MouseEventArgs e, PointF imagePoint)
    {
        if (e.Button == MouseButtons.Left)
        {
            switch (_clickCount)
            {
                case 0:
                    _point1 = imagePoint;
                    _clickCount = 1;
                    break;
                case 1:
                    _vertex = imagePoint;
                    _clickCount = 2;
                    break;
                case 2:
                    CalculateAngle(_point1!.Value, _vertex!.Value, imagePoint);
                    _point1 = null;
                    _vertex = null;
                    _clickCount = 0;
                    break;
            }
        }
    }

    public override void OnMouseMove(MouseEventArgs e, PointF imagePoint)
    {
        if (_clickCount > 0)
        {
            _tempPoint = imagePoint;
        }
    }

    public override void OnMouseUp(MouseEventArgs e, PointF imagePoint)
    {
    }

    private void CalculateAngle(PointF p1, PointF vertex, PointF p2)
    {
        double angle = GeometryMath.AngleAtVertex(p1, vertex, p2);

        var result = new AngleResult
        {
            Name = "角度",
            Point1 = p1,
            Vertex = vertex,
            Point2 = p2,
            Angle = angle
        };

        RaiseMeasurementCompleted(result);
    }

    public override void OnPaint(Graphics g, Func<PointF, PointF> toScreen)
    {
        using var pen = new Pen(Color.Magenta, 2);
        using var brush = new SolidBrush(Color.Red);

        if (_point1.HasValue)
        {
            var p1 = toScreen(_point1.Value);
            g.FillEllipse(brush, p1.X - 5, p1.Y - 5, 10, 10);

            if (_clickCount == 1 && _tempPoint.HasValue)
            {
                var temp = toScreen(_tempPoint.Value);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, p1, temp);
            }
        }

        if (_vertex.HasValue)
        {
            var v = toScreen(_vertex.Value);
            g.FillEllipse(Brushes.Yellow, v.X - 6, v.Y - 6, 12, 12);

            if (_point1.HasValue)
            {
                var p1 = toScreen(_point1.Value);
                g.DrawLine(pen, p1, v);
            }

            if (_clickCount == 2 && _tempPoint.HasValue)
            {
                var temp = toScreen(_tempPoint.Value);
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                g.DrawLine(pen, v, temp);

                // 顯示角度預覽
                if (_point1.HasValue)
                {
                    double angle = GeometryMath.AngleAtVertex(_point1.Value, _vertex.Value, _tempPoint.Value);
                    using var font = new Font("Consolas", 9);
                    using var textBrush = new SolidBrush(Color.Yellow);
                    g.DrawString($"{angle:F1}°", font, textBrush, v.X + 15, v.Y - 25);
                }
            }
        }
    }

    public override void Cancel()
    {
        _point1 = null;
        _vertex = null;
        _tempPoint = null;
        _clickCount = 0;
    }
}
