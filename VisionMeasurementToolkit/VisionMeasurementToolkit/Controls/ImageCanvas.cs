using System.Drawing.Drawing2D;
using OpenCvSharp;
using VisionMeasurementToolkit.Core;
using VisionMeasurementToolkit.Tools;

namespace VisionMeasurementToolkit.Controls;

/// <summary>
/// 影像顯示控件
/// </summary>
public class ImageCanvas : Control
{
    private Bitmap? _displayImage;
    private Mat? _grayImage;
    private float _zoom = 1.0f;
    private PointF _offset = PointF.Empty;
    private bool _isPanning;
    private System.Drawing.Point _lastMousePos;
    private bool _spacePressed;

    private ITool? _currentTool;
    private readonly List<MeasurementResult> _results = new();
    private MeasurementResult? _selectedResult;

    public event EventHandler<PointF>? MousePositionChanged;
    public event EventHandler<MeasurementResult>? ResultSelected;

    public Mat? GrayImage => _grayImage;
    public IReadOnlyList<MeasurementResult> Results => _results;
    public MeasurementResult? SelectedResult => _selectedResult;
    public double? TrueEdgePosition { get; set; }

    public ImageCanvas()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(45, 45, 48);
    }

    public void SetImage(Bitmap? displayImage, Mat? grayImage)
    {
        _displayImage?.Dispose();
        _grayImage?.Dispose();

        _displayImage = displayImage;
        _grayImage = grayImage?.Clone();

        if (displayImage != null)
        {
            FitToView();
        }

        ClearResults();
        Invalidate();
    }

    public void SetTool(ITool? tool)
    {
        _currentTool?.Cancel();
        _currentTool = tool;

        if (tool is CircleFinderTool circleTool && _grayImage != null)
            circleTool.SetImage(_grayImage);
        else if (tool is LineFinderTool lineTool && _grayImage != null)
            lineTool.SetImage(_grayImage);
        else if (tool is SubpixelEdgeTool subpixelTool && _grayImage != null)
        {
            subpixelTool.SetImage(_grayImage);
            subpixelTool.SetTrueEdgePosition(TrueEdgePosition);
        }

        Cursor = tool?.ToolCursor ?? Cursors.Default;
        Invalidate();
    }

    public void AddResult(MeasurementResult result)
    {
        _results.Add(result);
        Invalidate();
    }

    public void RemoveResult(MeasurementResult result)
    {
        _results.Remove(result);
        if (_selectedResult == result)
            _selectedResult = null;
        Invalidate();
    }

    public void ClearResults()
    {
        _results.Clear();
        _selectedResult = null;
        Invalidate();
    }

    public void SelectResult(MeasurementResult? result)
    {
        _selectedResult = result;
        ResultSelected?.Invoke(this, result!);
        Invalidate();
    }

    public void FitToView()
    {
        if (_displayImage == null) return;

        float zoomX = (float)ClientSize.Width / _displayImage.Width;
        float zoomY = (float)ClientSize.Height / _displayImage.Height;
        _zoom = Math.Min(zoomX, zoomY) * 0.9f;

        _offset = new PointF(
            (ClientSize.Width - _displayImage.Width * _zoom) / 2,
            (ClientSize.Height - _displayImage.Height * _zoom) / 2);

        Invalidate();
    }

    private PointF ScreenToImage(System.Drawing.Point screenPos)
    {
        return new PointF(
            (screenPos.X - _offset.X) / _zoom,
            (screenPos.Y - _offset.Y) / _zoom);
    }

    private PointF ImageToScreen(PointF imagePos)
    {
        return new PointF(
            imagePos.X * _zoom + _offset.X,
            imagePos.Y * _zoom + _offset.Y);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        if (_displayImage != null)
        {
            g.TranslateTransform(_offset.X, _offset.Y);
            g.ScaleTransform(_zoom, _zoom);
            g.DrawImage(_displayImage, 0, 0);
            g.ResetTransform();

            // 繪製所有量測結果
            foreach (var result in _results)
            {
                result.Draw(g, ImageToScreen, result == _selectedResult);
            }

            // 繪製當前工具的預覽
            _currentTool?.OnPaint(g, ImageToScreen);
        }
        else
        {
            using var font = new Font("Microsoft JhengHei", 14);
            using var brush = new SolidBrush(Color.Gray);
            var text = "請載入影像 (Ctrl+O) 或拖放檔案";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush,
                (ClientSize.Width - size.Width) / 2,
                (ClientSize.Height - size.Height) / 2);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (_displayImage == null) return;

        var imgPos = ScreenToImage(e.Location);

        if ((e.Button == MouseButtons.Middle) || (_spacePressed && e.Button == MouseButtons.Left))
        {
            _isPanning = true;
            _lastMousePos = e.Location;
            Cursor = Cursors.Hand;
        }
        else if (e.Button == MouseButtons.Left && _currentTool != null)
        {
            _currentTool.OnMouseDown(e, imgPos);
            Invalidate();
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_displayImage == null) return;

        var imgPos = ScreenToImage(e.Location);
        MousePositionChanged?.Invoke(this, imgPos);

        if (_isPanning)
        {
            int dx = e.X - _lastMousePos.X;
            int dy = e.Y - _lastMousePos.Y;
            _offset = new PointF(_offset.X + dx, _offset.Y + dy);
            _lastMousePos = e.Location;
            Invalidate();
        }
        else if (_currentTool != null)
        {
            _currentTool.OnMouseMove(e, imgPos);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_isPanning)
        {
            _isPanning = false;
            Cursor = _currentTool?.ToolCursor ?? Cursors.Default;
        }
        else if (_displayImage != null && _currentTool != null)
        {
            var imgPos = ScreenToImage(e.Location);
            _currentTool.OnMouseUp(e, imgPos);
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        if (_displayImage == null) return;

        float oldZoom = _zoom;
        float zoomFactor = e.Delta > 0 ? 1.1f : 0.9f;
        _zoom *= zoomFactor;
        _zoom = Math.Clamp(_zoom, 0.1f, 20f);

        float mouseX = e.X - _offset.X;
        float mouseY = e.Y - _offset.Y;
        _offset.X -= mouseX * (_zoom / oldZoom - 1);
        _offset.Y -= mouseY * (_zoom / oldZoom - 1);

        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyCode == Keys.Space)
        {
            _spacePressed = true;
            Cursor = Cursors.Hand;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            _currentTool?.Cancel();
            Invalidate();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.KeyCode == Keys.Space)
        {
            _spacePressed = false;
            Cursor = _currentTool?.ToolCursor ?? Cursors.Default;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnDragEnter(DragEventArgs drgevent)
    {
        base.OnDragEnter(drgevent);
        if (drgevent.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            drgevent.Effect = DragDropEffects.Copy;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _displayImage?.Dispose();
            _grayImage?.Dispose();
        }
        base.Dispose(disposing);
    }
}
