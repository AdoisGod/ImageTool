using OpenCvSharp;
using VisionVerificationToolkit.Objects;
using System.Drawing.Drawing2D;
using DrawingSize = System.Drawing.Size;
using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace VisionVerificationToolkit.Controls;

/// <summary>
/// 影像顯示控件 - 支援縮放、平移、物件繪製
/// </summary>
public class ImageCanvas : Control
{
    private Bitmap? _image;
    private Mat? _mat;
    private float _zoom = 1.0f;
    private PointF _offset = PointF.Empty;
    private DrawingPoint _lastMousePos;
    private bool _isPanning;
    private bool _isDrawing;
    private DrawingPointF _drawStart;
    private DrawingPointF _drawEnd;

    private readonly List<IGeometryObject> _objects = new();
    private IGeometryObject? _selectedObject;

    public event EventHandler<DrawingPointF>? MousePositionChanged;
    public event EventHandler<DrawingRectangleF>? RoiSelected;
    public event EventHandler<(DrawingPointF Start, DrawingPointF End)>? LineDrawn;
    public event EventHandler<IGeometryObject>? ObjectSelected;

    public enum DrawMode { None, Rectangle, Line, Point }
    public DrawMode CurrentDrawMode { get; set; } = DrawMode.None;

    public IReadOnlyList<IGeometryObject> Objects => _objects.AsReadOnly();
    public IGeometryObject? SelectedObject => _selectedObject;
    public float Zoom => _zoom;

    public ImageCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(40, 40, 40);
    }

    public void SetImage(Bitmap bitmap, Mat? mat = null)
    {
        _image?.Dispose();
        _image = bitmap;
        _mat = mat;
        FitToWindow();
        Invalidate();
    }

    public void SetImage(Mat mat)
    {
        _mat?.Dispose();
        _mat = mat.Clone();

        using var colorMat = new Mat();
        if (mat.Channels() == 1)
            Cv2.CvtColor(mat, colorMat, ColorConversionCodes.GRAY2BGR);
        else
            mat.CopyTo(colorMat);

        _image?.Dispose();
        _image = MatToBitmap(colorMat);
        FitToWindow();
        Invalidate();
    }

    public Mat? GetMat() => _mat;

    public void ClearImage()
    {
        _image?.Dispose();
        _image = null;
        _mat?.Dispose();
        _mat = null;
        _objects.Clear();
        Invalidate();
    }

    public void FitToWindow()
    {
        if (_image == null || Width == 0 || Height == 0) return;

        float scaleX = (float)Width / _image.Width;
        float scaleY = (float)Height / _image.Height;
        _zoom = Math.Min(scaleX, scaleY) * 0.95f;

        _offset.X = (Width - _image.Width * _zoom) / 2;
        _offset.Y = (Height - _image.Height * _zoom) / 2;

        Invalidate();
    }

    public void SetZoom(float zoom)
    {
        _zoom = Math.Max(0.1f, Math.Min(10f, zoom));
        Invalidate();
    }

    public void AddObject(IGeometryObject obj)
    {
        _objects.Add(obj);
        Invalidate();
    }

    public void RemoveObject(IGeometryObject obj)
    {
        _objects.Remove(obj);
        if (_selectedObject == obj) _selectedObject = null;
        Invalidate();
    }

    public void ClearObjects()
    {
        _objects.Clear();
        _selectedObject = null;
        Invalidate();
    }

    public void SelectObject(IGeometryObject? obj)
    {
        if (_selectedObject != null)
            _selectedObject.IsSelected = false;

        _selectedObject = obj;

        if (obj != null)
        {
            obj.IsSelected = true;
            ObjectSelected?.Invoke(this, obj);
        }

        Invalidate();
    }

    private DrawingPointF ScreenToImage(DrawingPoint screenPoint)
    {
        return new DrawingPointF(
            (screenPoint.X - _offset.X) / _zoom,
            (screenPoint.Y - _offset.Y) / _zoom);
    }

    private DrawingPoint ImageToScreen(DrawingPointF imagePoint)
    {
        return new DrawingPoint(
            (int)(imagePoint.X * _zoom + _offset.X),
            (int)(imagePoint.Y * _zoom + _offset.Y));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        // 繪製影像
        if (_image != null)
        {
            g.TranslateTransform(_offset.X, _offset.Y);
            g.ScaleTransform(_zoom, _zoom);
            g.DrawImage(_image, 0, 0);
            g.ResetTransform();

            // 繪製物件
            foreach (var obj in _objects.Where(o => o.IsVisible))
            {
                DrawObject(g, obj);
            }

            // 繪製正在繪製的形狀
            if (_isDrawing)
            {
                var pen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash };
                var start = ImageToScreen(_drawStart);
                var end = ImageToScreen(_drawEnd);

                switch (CurrentDrawMode)
                {
                    case DrawMode.Rectangle:
                        var rect = GetRectangle(start, end);
                        g.DrawRectangle(pen, rect);
                        break;
                    case DrawMode.Line:
                        g.DrawLine(pen, start, end);
                        break;
                }
                pen.Dispose();
            }
        }
        else
        {
            // 無影像時顯示提示
            using var font = new Font("Microsoft JhengHei", 14);
            using var brush = new SolidBrush(Color.Gray);
            var text = "拖放影像至此或點擊載入";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (Width - size.Width) / 2, (Height - size.Height) / 2);
        }
    }

    private void DrawObject(Graphics g, IGeometryObject obj)
    {
        var color = obj.IsSelected ? Color.Cyan : obj.DisplayColor;
        using var pen = new Pen(color, obj.IsSelected ? 2 : 1);
        using var brush = new SolidBrush(Color.FromArgb(100, color));

        switch (obj)
        {
            case PointObject point:
                var p = ImageToScreen(point.Position);
                g.FillEllipse(brush, p.X - 5, p.Y - 5, 10, 10);
                g.DrawEllipse(pen, p.X - 5, p.Y - 5, 10, 10);
                break;

            case LineObject line:
                var start = ImageToScreen(line.StartPoint);
                var end = ImageToScreen(line.EndPoint);
                g.DrawLine(pen, start, end);

                // 繪製邊緣點
                if (line.EdgePoints.Count > 0)
                {
                    using var edgePen = new Pen(Color.FromArgb(150, color), 1);
                    foreach (var ep in line.EdgePoints)
                    {
                        var ep2 = ImageToScreen(ep);
                        g.DrawEllipse(edgePen, ep2.X - 2, ep2.Y - 2, 4, 4);
                    }
                }
                break;

            case CircleObject circle:
                var center = ImageToScreen(circle.Center);
                int radius = (int)(circle.Radius * _zoom);
                g.DrawEllipse(pen, center.X - radius, center.Y - radius, radius * 2, radius * 2);

                // 繪製圓心
                g.FillEllipse(brush, center.X - 3, center.Y - 3, 6, 6);
                break;

            case ContourObject contour:
                if (contour.Points.Count > 2)
                {
                    var points = contour.Points.Select(pt => ImageToScreen(pt)).ToArray();
                    g.DrawPolygon(pen, points);
                }
                break;

            case MeasurementResult measurement:
                DrawMeasurement(g, measurement, pen);
                break;
        }

        // 繪製名稱
        if (obj.IsSelected)
        {
            var bbox = obj.GetBoundingBox();
            var textPos = ImageToScreen(new DrawingPointF(bbox.X, bbox.Y - 15));
            using var font = new Font("Microsoft JhengHei", 9);
            g.DrawString(obj.Name, font, Brushes.White, textPos);
        }
    }

    private void DrawMeasurement(Graphics g, MeasurementResult measurement, Pen pen)
    {
        if (measurement.AnnotationPoints.Count < 2) return;

        var points = measurement.AnnotationPoints.Select(p => ImageToScreen(p)).ToArray();

        switch (measurement.Type)
        {
            case MeasurementType.Distance:
                g.DrawLine(pen, points[0], points[1]);

                // 繪製尺寸標註
                var mid = new DrawingPoint((points[0].X + points[1].X) / 2, (points[0].Y + points[1].Y) / 2 - 15);
                using (var font = new Font("Consolas", 9))
                {
                    g.DrawString($"{measurement.Value:F2} px", font, Brushes.Orange, mid);
                }
                break;

            case MeasurementType.Angle:
                if (points.Length >= 3)
                {
                    g.DrawLine(pen, points[1], points[0]);
                    g.DrawLine(pen, points[1], points[2]);

                    // 繪製角度弧
                    using (var font = new Font("Consolas", 9))
                    {
                        g.DrawString($"{measurement.Value:F2}°", font, Brushes.Orange, points[1].X + 10, points[1].Y - 10);
                    }
                }
                break;
        }
    }

    private DrawingRectangle GetRectangle(DrawingPoint p1, DrawingPoint p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p2.X - p1.X);
        int h = Math.Abs(p2.Y - p1.Y);
        return new DrawingRectangle(x, y, w, h);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && ModifierKeys == Keys.Space))
        {
            _isPanning = true;
            _lastMousePos = e.Location;
            Cursor = Cursors.SizeAll;
        }
        else if (e.Button == MouseButtons.Left && _image != null)
        {
            var imagePos = ScreenToImage(e.Location);

            if (CurrentDrawMode != DrawMode.None)
            {
                _isDrawing = true;
                _drawStart = imagePos;
                _drawEnd = imagePos;
            }
            else
            {
                // 點選物件
                var hitObj = _objects.FirstOrDefault(o => o.IsVisible && o.HitTest(imagePos, 5 / _zoom));
                SelectObject(hitObj);
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_isPanning)
        {
            _offset.X += e.X - _lastMousePos.X;
            _offset.Y += e.Y - _lastMousePos.Y;
            _lastMousePos = e.Location;
            Invalidate();
        }
        else if (_isDrawing)
        {
            _drawEnd = ScreenToImage(e.Location);
            Invalidate();
        }

        if (_image != null)
        {
            var imagePos = ScreenToImage(e.Location);
            if (imagePos.X >= 0 && imagePos.X < _image.Width && imagePos.Y >= 0 && imagePos.Y < _image.Height)
            {
                MousePositionChanged?.Invoke(this, imagePos);
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
        }
        else if (_isDrawing)
        {
            _isDrawing = false;
            _drawEnd = ScreenToImage(e.Location);

            if (CurrentDrawMode == DrawMode.Rectangle)
            {
                var rect = new DrawingRectangleF(
                    Math.Min(_drawStart.X, _drawEnd.X),
                    Math.Min(_drawStart.Y, _drawEnd.Y),
                    Math.Abs(_drawEnd.X - _drawStart.X),
                    Math.Abs(_drawEnd.Y - _drawStart.Y));

                if (rect.Width > 5 && rect.Height > 5)
                {
                    RoiSelected?.Invoke(this, rect);
                }
            }
            else if (CurrentDrawMode == DrawMode.Line)
            {
                double length = Math.Sqrt(Math.Pow(_drawEnd.X - _drawStart.X, 2) + Math.Pow(_drawEnd.Y - _drawStart.Y, 2));
                if (length > 5)
                {
                    LineDrawn?.Invoke(this, (_drawStart, _drawEnd));
                }
            }

            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_image == null) return;

        var mousePos = ScreenToImage(e.Location);
        float oldZoom = _zoom;

        if (e.Delta > 0)
            _zoom *= 1.1f;
        else
            _zoom /= 1.1f;

        _zoom = Math.Max(0.1f, Math.Min(10f, _zoom));

        // 以滑鼠位置為中心縮放
        _offset.X = e.X - mousePos.X * _zoom;
        _offset.Y = e.Y - mousePos.Y * _zoom;

        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    private static Bitmap MatToBitmap(Mat mat)
    {
        var bitmap = new Bitmap(mat.Width, mat.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var bmpData = bitmap.LockBits(
            new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            bitmap.PixelFormat);

        int stride = bmpData.Stride;
        int width = mat.Width;
        int height = mat.Height;

        unsafe
        {
            byte* dst = (byte*)bmpData.Scan0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = mat.At<Vec3b>(y, x);
                    int idx = y * stride + x * 3;
                    dst[idx] = pixel.Item0;
                    dst[idx + 1] = pixel.Item1;
                    dst[idx + 2] = pixel.Item2;
                }
            }
        }

        bitmap.UnlockBits(bmpData);
        return bitmap;
    }
}
