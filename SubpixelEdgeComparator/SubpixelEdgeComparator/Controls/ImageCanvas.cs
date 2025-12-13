using System.Drawing.Drawing2D;

namespace SubpixelEdgeComparator.Controls;

/// <summary>
/// 自訂影像顯示控件，支援縮放、平移和 ROI 線段繪製
/// </summary>
public class ImageCanvas : Control
{
    private Bitmap? _image;
    private float _zoom = 1.0f;
    private PointF _offset = PointF.Empty;
    private bool _isPanning;
    private Point _lastMousePos;
    private bool _isDrawingRoi;
    private PointF? _roiStart;
    private PointF? _roiEnd;
    private PointF? _tempRoiEnd;

    /// <summary>
    /// ROI 線段起點（影像座標）
    /// </summary>
    public PointF? RoiStart => _roiStart;

    /// <summary>
    /// ROI 線段終點（影像座標）
    /// </summary>
    public PointF? RoiEnd => _roiEnd;

    /// <summary>
    /// 是否有有效的 ROI
    /// </summary>
    public bool HasValidRoi => _roiStart.HasValue && _roiEnd.HasValue && GetRoiLength() >= 10;

    /// <summary>
    /// ROI 變更事件
    /// </summary>
    public event EventHandler? RoiChanged;

    /// <summary>
    /// 滑鼠位置變更事件（影像座標）
    /// </summary>
    public event EventHandler<PointF>? MousePositionChanged;

    public ImageCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.DarkGray;
    }

    /// <summary>
    /// 設定要顯示的影像
    /// </summary>
    public void SetImage(Bitmap? image)
    {
        _image?.Dispose();
        _image = image;

        if (image != null)
        {
            // 自動縮放以適應控件
            FitToView();
        }

        ClearRoi();
        Invalidate();
    }

    /// <summary>
    /// 清除 ROI
    /// </summary>
    public void ClearRoi()
    {
        _roiStart = null;
        _roiEnd = null;
        _tempRoiEnd = null;
        RoiChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    /// <summary>
    /// 縮放到適合視窗
    /// </summary>
    public void FitToView()
    {
        if (_image == null) return;

        float zoomX = (float)ClientSize.Width / _image.Width;
        float zoomY = (float)ClientSize.Height / _image.Height;
        _zoom = Math.Min(zoomX, zoomY) * 0.9f;

        // 置中
        _offset = new PointF(
            (ClientSize.Width - _image.Width * _zoom) / 2,
            (ClientSize.Height - _image.Height * _zoom) / 2
        );

        Invalidate();
    }

    /// <summary>
    /// 取得 ROI 線段長度（像素）
    /// </summary>
    public double GetRoiLength()
    {
        if (!_roiStart.HasValue || !_roiEnd.HasValue)
            return 0;

        float dx = _roiEnd.Value.X - _roiStart.Value.X;
        float dy = _roiEnd.Value.Y - _roiStart.Value.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        // 繪製影像
        if (_image != null)
        {
            g.TranslateTransform(_offset.X, _offset.Y);
            g.ScaleTransform(_zoom, _zoom);
            g.DrawImage(_image, 0, 0);
            g.ResetTransform();

            // 繪製 ROI 線段
            DrawRoi(g);
        }
        else
        {
            // 無影像時顯示提示
            using var font = new Font("Microsoft JhengHei", 12);
            using var brush = new SolidBrush(Color.White);
            var text = "請載入影像或產生合成測試影像";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush,
                (ClientSize.Width - size.Width) / 2,
                (ClientSize.Height - size.Height) / 2);
        }
    }

    private void DrawRoi(Graphics g)
    {
        if (!_roiStart.HasValue) return;

        var start = ImageToScreen(_roiStart.Value);
        PointF end;

        if (_isDrawingRoi && _tempRoiEnd.HasValue)
        {
            end = ImageToScreen(_tempRoiEnd.Value);
            // 繪製預覽線段（虛線）
            using var pen = new Pen(Color.Yellow, 2) { DashStyle = DashStyle.Dash };
            g.DrawLine(pen, start, end);
        }
        else if (_roiEnd.HasValue)
        {
            end = ImageToScreen(_roiEnd.Value);
            // 繪製正式 ROI 線段（紅色實線）
            using var pen = new Pen(Color.Red, 2);
            g.DrawLine(pen, start, end);

            // 繪製端點
            float pointSize = 8;
            using var brush = new SolidBrush(Color.Red);
            g.FillEllipse(brush, start.X - pointSize / 2, start.Y - pointSize / 2, pointSize, pointSize);
            g.FillEllipse(brush, end.X - pointSize / 2, end.Y - pointSize / 2, pointSize, pointSize);

            // 繪製座標標籤
            using var font = new Font("Consolas", 9);
            using var textBrush = new SolidBrush(Color.Yellow);
            g.DrawString($"({_roiStart.Value.X:F1}, {_roiStart.Value.Y:F1})", font, textBrush, start.X + 5, start.Y - 20);
            g.DrawString($"({_roiEnd.Value.X:F1}, {_roiEnd.Value.Y:F1})", font, textBrush, end.X + 5, end.Y + 5);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_image == null) return;

        if (e.Button == MouseButtons.Left)
        {
            // 開始繪製 ROI
            var imgPos = ScreenToImage(e.Location);
            if (IsInImageBounds(imgPos))
            {
                _isDrawingRoi = true;
                _roiStart = imgPos;
                _roiEnd = null;
                _tempRoiEnd = null;
                Invalidate();
            }
        }
        else if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
        {
            // 開始平移
            _isPanning = true;
            _lastMousePos = e.Location;
            Cursor = Cursors.Hand;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_image == null) return;

        var imgPos = ScreenToImage(e.Location);
        if (IsInImageBounds(imgPos))
        {
            MousePositionChanged?.Invoke(this, imgPos);
        }

        if (_isDrawingRoi)
        {
            _tempRoiEnd = imgPos;
            Invalidate();
        }
        else if (_isPanning)
        {
            int dx = e.X - _lastMousePos.X;
            int dy = e.Y - _lastMousePos.Y;
            _offset = new PointF(_offset.X + dx, _offset.Y + dy);
            _lastMousePos = e.Location;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isDrawingRoi && e.Button == MouseButtons.Left)
        {
            _isDrawingRoi = false;
            var imgPos = ScreenToImage(e.Location);
            _roiEnd = imgPos;
            _tempRoiEnd = null;

            if (GetRoiLength() < 10)
            {
                // 線段太短，清除
                _roiStart = null;
                _roiEnd = null;
            }

            RoiChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
        else if (_isPanning)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_image == null) return;

        // Ctrl + 滾輪縮放
        if (ModifierKeys.HasFlag(Keys.Control))
        {
            float oldZoom = _zoom;
            float zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
            _zoom *= zoomFactor;
            _zoom = Math.Clamp(_zoom, 0.1f, 10f);

            // 以滑鼠位置為中心縮放
            float mouseX = e.X - _offset.X;
            float mouseY = e.Y - _offset.Y;
            _offset.X -= mouseX * (_zoom / oldZoom - 1);
            _offset.Y -= mouseY * (_zoom / oldZoom - 1);

            Invalidate();
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    private PointF ScreenToImage(Point screenPos)
    {
        return new PointF(
            (screenPos.X - _offset.X) / _zoom,
            (screenPos.Y - _offset.Y) / _zoom
        );
    }

    private PointF ImageToScreen(PointF imagePos)
    {
        return new PointF(
            imagePos.X * _zoom + _offset.X,
            imagePos.Y * _zoom + _offset.Y
        );
    }

    private bool IsInImageBounds(PointF imgPos)
    {
        if (_image == null) return false;
        return imgPos.X >= 0 && imgPos.X < _image.Width &&
               imgPos.Y >= 0 && imgPos.Y < _image.Height;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
