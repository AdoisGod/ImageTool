using OpenCvSharp;
using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Tools;

/// <summary>
/// 亞像素邊緣檢測比較工具
/// </summary>
public class SubpixelEdgeTool : ToolBase
{
    private Mat? _image;
    private PointF? _startPoint;
    private PointF? _currentPoint;
    private bool _isDragging;
    private double? _trueEdgePosition;

    public override string Name => "亞像素邊緣";
    public override string Description => "比較四種亞像素邊緣檢測演算法";

    public event EventHandler<SubpixelAnalysisResult>? AnalysisCompleted;

    public void SetImage(Mat image) => _image = image;
    public void SetTrueEdgePosition(double? position) => _trueEdgePosition = position;

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
        if (_isDragging) _currentPoint = imagePoint;
    }

    public override void OnMouseUp(MouseEventArgs e, PointF imagePoint)
    {
        if (_isDragging && _startPoint.HasValue && _image != null)
        {
            _currentPoint = imagePoint;
            _isDragging = false;

            double length = GeometryMath.Distance(_startPoint.Value, _currentPoint.Value);
            if (length >= 10)
            {
                RunAnalysis();
            }
            else
            {
                MessageBox.Show("線段太短（需至少 10 像素）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            _startPoint = null;
            _currentPoint = null;
        }
    }

    private void RunAnalysis()
    {
        if (_image == null || !_startPoint.HasValue || !_currentPoint.HasValue) return;

        try
        {
            // 提取剖面
            var grayProfile = ExtractProfile(_image, _startPoint.Value, _currentPoint.Value, 2);
            if (grayProfile.Length < 5)
            {
                MessageBox.Show("剖面長度不足", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 計算梯度
            var gradientProfile = ComputeGradient(grayProfile);

            // 計算真實邊緣在剖面中的位置
            double? profileTruePosition = null;
            if (_trueEdgePosition.HasValue)
            {
                double dx = _currentPoint.Value.X - _startPoint.Value.X;
                if (Math.Abs(dx) > 1e-6)
                {
                    double t = (_trueEdgePosition.Value - _startPoint.Value.X) / dx;
                    if (t >= 0 && t <= 1)
                    {
                        profileTruePosition = t * (grayProfile.Length - 1);
                    }
                }
            }

            // 執行四種演算法
            var parabolic = SubpixelMethods.ParabolicFit(gradientProfile);
            var gaussian = SubpixelMethods.GaussianFit(gradientProfile);
            var moment = SubpixelMethods.MomentMethod(gradientProfile);
            var sigmoid = SubpixelMethods.SigmoidFit(grayProfile);

            // 計算誤差
            if (profileTruePosition.HasValue)
            {
                if (parabolic.Position.HasValue) parabolic.Error = parabolic.Position.Value - profileTruePosition.Value;
                if (gaussian.Position.HasValue) gaussian.Error = gaussian.Position.Value - profileTruePosition.Value;
                if (moment.Position.HasValue) moment.Error = moment.Position.Value - profileTruePosition.Value;
                if (sigmoid.Position.HasValue) sigmoid.Error = sigmoid.Position.Value - profileTruePosition.Value;
            }

            var results = new List<SubpixelMethods.MethodResult> { parabolic, gaussian, moment, sigmoid };

            // 建立量測結果
            var measureResult = new SubpixelEdgeResult
            {
                Name = "亞像素",
                RoiStart = _startPoint.Value,
                RoiEnd = _currentPoint.Value,
                MethodResults = results
            };

            RaiseMeasurementCompleted(measureResult);

            // 通知分析完成
            var analysisResult = new SubpixelAnalysisResult
            {
                GrayProfile = grayProfile,
                GradientProfile = gradientProfile,
                Results = results,
                TrueEdgePosition = profileTruePosition
            };

            AnalysisCompleted?.Invoke(this, analysisResult);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"分析失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private double[] ExtractProfile(Mat image, PointF p1, PointF p2, int supersample)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);

        int numSamples = (int)(length * supersample);
        if (numSamples < 2) numSamples = 2;

        var profile = new double[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / (numSamples - 1);
            double x = p1.X + t * dx;
            double y = p1.Y + t * dy;

            // 雙線性插值
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = x0 + 1;
            int y1 = y0 + 1;

            x0 = Math.Clamp(x0, 0, image.Cols - 1);
            x1 = Math.Clamp(x1, 0, image.Cols - 1);
            y0 = Math.Clamp(y0, 0, image.Rows - 1);
            y1 = Math.Clamp(y1, 0, image.Rows - 1);

            double xFrac = x - Math.Floor(x);
            double yFrac = y - Math.Floor(y);

            double v00 = image.At<byte>(y0, x0);
            double v10 = image.At<byte>(y0, x1);
            double v01 = image.At<byte>(y1, x0);
            double v11 = image.At<byte>(y1, x1);

            double v0 = v00 * (1 - xFrac) + v10 * xFrac;
            double v1 = v01 * (1 - xFrac) + v11 * xFrac;

            profile[i] = v0 * (1 - yFrac) + v1 * yFrac;
        }

        return profile;
    }

    private double[] ComputeGradient(double[] profile)
    {
        var gradient = new double[profile.Length - 1];
        for (int i = 0; i < gradient.Length; i++)
        {
            gradient[i] = profile[i + 1] - profile[i];
        }
        return gradient;
    }

    public override void OnPaint(Graphics g, Func<PointF, PointF> toScreen)
    {
        if (_isDragging && _startPoint.HasValue && _currentPoint.HasValue)
        {
            var p1 = toScreen(_startPoint.Value);
            var p2 = toScreen(_currentPoint.Value);

            using var pen = new Pen(Color.Red, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            g.DrawLine(pen, p1, p2);

            // 顯示長度
            double length = GeometryMath.Distance(_startPoint.Value, _currentPoint.Value);
            using var font = new Font("Consolas", 9);
            using var brush = new SolidBrush(Color.Yellow);
            var mid = new PointF((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            g.DrawString($"{length:F1}px", font, brush, mid.X + 5, mid.Y - 15);
        }
    }

    public override void Cancel()
    {
        _isDragging = false;
        _startPoint = null;
        _currentPoint = null;
    }
}
