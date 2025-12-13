using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 點到點距離量測工具
/// </summary>
public class PointToPointTool : ToolBase
{
    private PointF? _point1;
    private PointF? _tempPoint;
    private int _clickCount;

    public override string Name => "點到點";
    public override string Description => "量測兩點之間的距離";

    public override void OnMouseDown(MouseEventArgs e, PointF imagePoint)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_clickCount == 0)
            {
                _point1 = imagePoint;
                _clickCount = 1;
            }
            else
            {
                // 完成量測
                var point2 = imagePoint;
                CalculateDistance(_point1!.Value, point2);
                _point1 = null;
                _clickCount = 0;
            }
        }
    }

    public override void OnMouseMove(MouseEventArgs e, PointF imagePoint)
    {
        if (_clickCount == 1)
        {
            _tempPoint = imagePoint;
        }
    }

    public override void OnMouseUp(MouseEventArgs e, PointF imagePoint)
    {
        // 不需要特別處理
    }

    private void CalculateDistance(PointF p1, PointF p2)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;

        var result = new PointToPointResult
        {
            Name = "距離",
            Point1 = p1,
            Point2 = p2,
            HorizontalDistance = Math.Abs(dx),
            VerticalDistance = Math.Abs(dy),
            Distance = distance,
            AngleDegrees = angle
        };

        RaiseMeasurementCompleted(result);
    }

    public override void OnPaint(Graphics g, Func<PointF, PointF> toScreen)
    {
        if (_point1.HasValue)
        {
            var p1 = toScreen(_point1.Value);

            // 繪製第一個點
            using var brush = new SolidBrush(Color.Red);
            g.FillEllipse(brush, p1.X - 5, p1.Y - 5, 10, 10);

            // 如果正在移動，繪製預覽線
            if (_tempPoint.HasValue)
            {
                var p2 = toScreen(_tempPoint.Value);
                using var pen = new Pen(Color.Orange, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                g.DrawLine(pen, p1, p2);

                // 顯示距離預覽
                double dist = GeometryMath.Distance(_point1.Value, _tempPoint.Value);
                using var font = new Font("Consolas", 9);
                using var textBrush = new SolidBrush(Color.Yellow);
                var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                g.DrawString($"{dist:F1}px", font, textBrush, mid.X + 5, mid.Y - 15);
            }
        }
    }

    public override void Cancel()
    {
        _point1 = null;
        _tempPoint = null;
        _clickCount = 0;
    }
}
