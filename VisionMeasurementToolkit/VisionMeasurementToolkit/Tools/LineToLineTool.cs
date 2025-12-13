using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 線到線距離/角度量測工具
/// </summary>
public class LineToLineTool : ToolBase
{
    private LineResult? _line1;
    private LineResult? _line2;

    public override string Name => "線到線";
    public override string Description => "計算兩條直線的距離或夾角";

    public void SetLines(LineResult line1, LineResult line2)
    {
        _line1 = line1;
        _line2 = line2;
    }

    public LineToLineResult? Calculate()
    {
        if (_line1 == null || _line2 == null)
            return null;

        // 計算兩線夾角
        double angle = GeometryMath.AngleBetweenLines(
            _line1.StartPoint, _line1.EndPoint,
            _line2.StartPoint, _line2.EndPoint);

        var result = new LineToLineResult
        {
            Name = "線距",
            Line1 = _line1,
            Line2 = _line2,
            AngleBetween = angle
        };

        // 判斷是否平行（夾角 < 5°）
        if (angle < 5)
        {
            result.IsParallel = true;
            // 計算垂直距離（取 line1 中點到 line2 的距離）
            var midPoint = new PointF(
                (_line1.StartPoint.X + _line1.EndPoint.X) / 2,
                (_line1.StartPoint.Y + _line1.EndPoint.Y) / 2);
            result.PerpendicularDistance = GeometryMath.PointToLineDistance(
                midPoint, _line2.StartPoint, _line2.EndPoint);
        }
        else
        {
            result.IsParallel = false;
            result.IntersectionPoint = GeometryMath.LineIntersection(
                _line1.StartPoint, _line1.EndPoint,
                _line2.StartPoint, _line2.EndPoint);
        }

        return result;
    }

    public override void OnMouseDown(MouseEventArgs e, PointF imagePoint) { }
    public override void OnMouseMove(MouseEventArgs e, PointF imagePoint) { }
    public override void OnMouseUp(MouseEventArgs e, PointF imagePoint) { }
    public override void OnPaint(Graphics g, Func<PointF, PointF> toScreen) { }
    public override void Cancel() { _line1 = null; _line2 = null; }
}
