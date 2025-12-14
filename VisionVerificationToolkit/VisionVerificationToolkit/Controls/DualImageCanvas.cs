using OpenCvSharp;
using System.Drawing.Drawing2D;
using DrawingSize = System.Drawing.Size;
using DrawingPoint = System.Drawing.Point;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;

namespace VisionVerificationToolkit.Controls;

/// <summary>
/// 雙影像對比控件 - 顯示原圖與處理後影像
/// </summary>
public class DualImageCanvas : Control
{
    private Bitmap? _originalImage;
    private Bitmap? _processedImage;
    private float _zoom = 1.0f;
    private PointF _offset = PointF.Empty;
    private DrawingPoint _lastMousePos;
    private bool _isPanning;
    private bool _syncZoom = true;

    public event EventHandler<DrawingPointF>? MousePositionChanged;

    public bool SyncZoom
    {
        get => _syncZoom;
        set { _syncZoom = value; Invalidate(); }
    }

    public float Zoom => _zoom;

    public DualImageCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(40, 40, 40);
    }

    public void SetOriginalImage(Bitmap bitmap)
    {
        _originalImage?.Dispose();
        _originalImage = bitmap;
        FitToWindow();
        Invalidate();
    }

    public void SetProcessedImage(Bitmap bitmap)
    {
        _processedImage?.Dispose();
        _processedImage = bitmap;
        Invalidate();
    }

    public void SetOriginalImage(Mat mat)
    {
        _originalImage?.Dispose();
        _originalImage = MatToBitmap(mat);
        FitToWindow();
        Invalidate();
    }

    public void SetProcessedImage(Mat mat)
    {
        _processedImage?.Dispose();
        _processedImage = MatToBitmap(mat);
        Invalidate();
    }

    public void ClearImages()
    {
        _originalImage?.Dispose();
        _processedImage?.Dispose();
        _originalImage = null;
        _processedImage = null;
        Invalidate();
    }

    public void FitToWindow()
    {
        var img = _originalImage ?? _processedImage;
        if (img == null || Width == 0 || Height == 0) return;

        int halfWidth = Width / 2 - 20;
        float scaleX = (float)halfWidth / img.Width;
        float scaleY = (float)Height / img.Height;
        _zoom = Math.Min(scaleX, scaleY) * 0.9f;

        _offset = PointF.Empty;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;

        int halfWidth = Width / 2;

        // 繪製分隔線
        using (var pen = new Pen(Color.Gray, 2))
        {
            g.DrawLine(pen, halfWidth, 0, halfWidth, Height);
        }

        // 左側：原圖
        DrawImagePanel(g, _originalImage, 0, halfWidth, "原圖");

        // 右側：處理後
        DrawImagePanel(g, _processedImage, halfWidth, halfWidth, "處理後");
    }

    private void DrawImagePanel(Graphics g, Bitmap? image, int offsetX, int panelWidth, string title)
    {
        // 繪製標題
        using (var font = new Font("Microsoft JhengHei", 10, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.White))
        {
            g.DrawString(title, font, brush, offsetX + 10, 5);
        }

        if (image == null)
        {
            using (var font = new Font("Microsoft JhengHei", 12))
            using (var brush = new SolidBrush(Color.Gray))
            {
                var text = "無影像";
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush, offsetX + (panelWidth - size.Width) / 2, Height / 2);
            }
            return;
        }

        // 計算影像位置
        float imgWidth = image.Width * _zoom;
        float imgHeight = image.Height * _zoom;
        float imgX = offsetX + (panelWidth - imgWidth) / 2 + _offset.X;
        float imgY = 30 + (Height - 30 - imgHeight) / 2 + _offset.Y;

        // 繪製影像
        g.SetClip(new DrawingRectangle(offsetX, 25, panelWidth - 5, Height - 30));
        g.DrawImage(image, imgX, imgY, imgWidth, imgHeight);
        g.ResetClip();
    }

    private DrawingPointF ScreenToImage(DrawingPoint screenPoint)
    {
        var img = _originalImage ?? _processedImage;
        if (img == null) return DrawingPointF.Empty;

        int halfWidth = Width / 2;
        int panelWidth = halfWidth;
        float imgWidth = img.Width * _zoom;
        float imgHeight = img.Height * _zoom;

        // 判斷在哪個面板
        int offsetX = screenPoint.X < halfWidth ? 0 : halfWidth;
        float imgX = offsetX + (panelWidth - imgWidth) / 2 + _offset.X;
        float imgY = 30 + (Height - 30 - imgHeight) / 2 + _offset.Y;

        return new DrawingPointF(
            (screenPoint.X - imgX) / _zoom,
            (screenPoint.Y - imgY) / _zoom);
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

        var img = _originalImage ?? _processedImage;
        if (img != null)
        {
            var imagePos = ScreenToImage(e.Location);
            if (imagePos.X >= 0 && imagePos.X < img.Width && imagePos.Y >= 0 && imagePos.Y < img.Height)
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
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (e.Delta > 0)
            _zoom *= 1.1f;
        else
            _zoom /= 1.1f;

        _zoom = Math.Max(0.1f, Math.Min(10f, _zoom));
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    private static Bitmap MatToBitmap(Mat mat)
    {
        Mat colorMat;
        if (mat.Channels() == 1)
        {
            colorMat = new Mat();
            Cv2.CvtColor(mat, colorMat, ColorConversionCodes.GRAY2BGR);
        }
        else
        {
            colorMat = mat;
        }

        var bitmap = new Bitmap(colorMat.Width, colorMat.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var bmpData = bitmap.LockBits(
            new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            bitmap.PixelFormat);

        unsafe
        {
            byte* dst = (byte*)bmpData.Scan0;
            int stride = bmpData.Stride;
            for (int y = 0; y < colorMat.Height; y++)
            {
                for (int x = 0; x < colorMat.Width; x++)
                {
                    var pixel = colorMat.At<Vec3b>(y, x);
                    int idx = y * stride + x * 3;
                    dst[idx] = pixel.Item0;
                    dst[idx + 1] = pixel.Item1;
                    dst[idx + 2] = pixel.Item2;
                }
            }
        }

        bitmap.UnlockBits(bmpData);

        if (colorMat != mat)
            colorMat.Dispose();

        return bitmap;
    }
}
