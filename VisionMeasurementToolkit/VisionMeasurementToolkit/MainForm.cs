using OpenCvSharp;
using VisionMeasurementToolkit.Controls;
using VisionMeasurementToolkit.Core;
using VisionMeasurementToolkit.Dialogs;
using VisionMeasurementToolkit.Tools;
using VisionMeasurementToolkit.Utils;
using DrawingSize = System.Drawing.Size;

namespace VisionMeasurementToolkit;

public partial class MainForm : Form
{
    private readonly CalibrationManager _calibration = new();
    private ImageCanvas _imageCanvas = null!;
    private DataGridView _resultGrid = null!;
    private TextBox _detailsBox = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripStatusLabel _mouseLabel = null!;
    private ToolStripStatusLabel _resolutionLabel = null!;
    private ToolStripStatusLabel _toolLabel = null!;

    private string? _currentImagePath;
    private ITool? _currentTool;

    // 工具實例
    private readonly CircleFinderTool _circleFinderTool = new();
    private readonly LineFinderTool _lineFinderTool = new();
    private readonly PointToPointTool _pointToPointTool = new();
    private readonly AngleTool _angleTool = new();
    private readonly SubpixelEdgeTool _subpixelEdgeTool = new();

    public MainForm()
    {
        InitializeComponent();
        SetupTools();
        SetupShortcuts();
        UpdateResolutionDisplay();
    }

    private void InitializeComponent()
    {
        Text = "Vision Measurement Toolkit - 影像量測工具集";
        Size = new DrawingSize(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new DrawingSize(1100, 700);

        CreateMenuStrip();
        CreateToolStrip();
        CreateStatusStrip();
        CreateMainLayout();
    }

    private void CreateMenuStrip()
    {
        var menuStrip = new MenuStrip();

        // 檔案選單
        var fileMenu = new ToolStripMenuItem("檔案(&F)");
        var openItem = new ToolStripMenuItem("開啟(&O)", null, (s, e) => LoadImage()) { ShortcutKeys = Keys.Control | Keys.O };
        fileMenu.DropDownItems.Add(openItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("合成測試影像", null, (s, e) => GenerateSyntheticImage());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("匯出 CSV", null, (s, e) => ExportCsv());
        fileMenu.DropDownItems.Add("匯出報告 (HTML)", null, (s, e) => ExportHtml());
        fileMenu.DropDownItems.Add("儲存截圖", null, (s, e) => SaveScreenshot());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("結束(&X)", null, (s, e) => Close());
        menuStrip.Items.Add(fileMenu);

        // 工具選單
        var toolMenu = new ToolStripMenuItem("工具(&T)");
        var circleItem = new ToolStripMenuItem("找圓 (&1)", null, (s, e) => SelectTool(_circleFinderTool)) { ShortcutKeys = Keys.D1 };
        var lineItem = new ToolStripMenuItem("找線 (&2)", null, (s, e) => SelectTool(_lineFinderTool)) { ShortcutKeys = Keys.D2 };
        var p2pItem = new ToolStripMenuItem("點到點 (&3)", null, (s, e) => SelectTool(_pointToPointTool)) { ShortcutKeys = Keys.D3 };
        var angleItem = new ToolStripMenuItem("角度 (&4)", null, (s, e) => SelectTool(_angleTool)) { ShortcutKeys = Keys.D4 };
        var subpixelItem = new ToolStripMenuItem("亞像素邊緣分析 (&5)", null, (s, e) => SelectTool(_subpixelEdgeTool)) { ShortcutKeys = Keys.D5 };
        toolMenu.DropDownItems.Add(circleItem);
        toolMenu.DropDownItems.Add(lineItem);
        toolMenu.DropDownItems.Add(p2pItem);
        toolMenu.DropDownItems.Add(angleItem);
        toolMenu.DropDownItems.Add(new ToolStripSeparator());
        toolMenu.DropDownItems.Add(subpixelItem);
        toolMenu.DropDownItems.Add(new ToolStripSeparator());
        toolMenu.DropDownItems.Add("清除所有結果", null, (s, e) => ClearAllResults());
        menuStrip.Items.Add(toolMenu);

        // 校正選單
        var calibMenu = new ToolStripMenuItem("校正(&C)");
        calibMenu.DropDownItems.Add("設定解析度...", null, (s, e) => SetResolution());
        calibMenu.DropDownItems.Add("使用標準圓校正...", null, (s, e) => CalibrateWithCircle());
        calibMenu.DropDownItems.Add(new ToolStripSeparator());
        calibMenu.DropDownItems.Add("重置校正", null, (s, e) => { _calibration.Reset(); UpdateResolutionDisplay(); });
        menuStrip.Items.Add(calibMenu);

        // 說明選單
        var helpMenu = new ToolStripMenuItem("說明(&H)");
        helpMenu.DropDownItems.Add("關於", null, (s, e) => ShowAbout());
        menuStrip.Items.Add(helpMenu);

        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);
    }

    private void CreateToolStrip()
    {
        var toolStrip = new ToolStrip();

        toolStrip.Items.Add(new ToolStripButton("開啟", null, (s, e) => LoadImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripButton("合成影像", null, (s, e) => GenerateSyntheticImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripSeparator());

        toolStrip.Items.Add(new ToolStripButton("找圓", null, (s, e) => SelectTool(_circleFinderTool)) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripButton("找線", null, (s, e) => SelectTool(_lineFinderTool)) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripButton("點到點", null, (s, e) => SelectTool(_pointToPointTool)) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripButton("角度", null, (s, e) => SelectTool(_angleTool)) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("亞像素邊緣", null, (s, e) => SelectTool(_subpixelEdgeTool)) { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font(Font, FontStyle.Bold) });

        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("校正", null, (s, e) => SetResolution()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("清除結果", null, (s, e) => ClearAllResults()) { DisplayStyle = ToolStripItemDisplayStyle.Text });

        Controls.Add(toolStrip);
    }

    private void CreateStatusStrip()
    {
        var statusStrip = new StatusStrip();

        _toolLabel = new ToolStripStatusLabel("工具: 無") { AutoSize = false, Width = 150 };
        _mouseLabel = new ToolStripStatusLabel("座標: -") { AutoSize = false, Width = 150 };
        _resolutionLabel = new ToolStripStatusLabel("解析度: 未校正") { AutoSize = false, Width = 200 };
        _statusLabel = new ToolStripStatusLabel("就緒") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

        statusStrip.Items.AddRange(new ToolStripItem[] { _toolLabel, _mouseLabel, _resolutionLabel, _statusLabel });
        Controls.Add(statusStrip);
    }

    private void CreateMainLayout()
    {
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 180,
            FixedPanel = FixedPanel.Panel1
        };

        // 左側：工具面板
        var toolPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10),
            AutoScroll = true
        };

        var btnSize = new DrawingSize(140, 35);
        toolPanel.Controls.Add(CreateToolButton("找圓 (1)", () => SelectTool(_circleFinderTool), btnSize));
        toolPanel.Controls.Add(CreateToolButton("找線 (2)", () => SelectTool(_lineFinderTool), btnSize));
        toolPanel.Controls.Add(CreateToolButton("點到點 (3)", () => SelectTool(_pointToPointTool), btnSize));
        toolPanel.Controls.Add(CreateToolButton("角度 (4)", () => SelectTool(_angleTool), btnSize));
        toolPanel.Controls.Add(new Label { Text = "─────────", AutoSize = true, ForeColor = Color.Gray });
        toolPanel.Controls.Add(CreateToolButton("亞像素邊緣 (5)", () => SelectTool(_subpixelEdgeTool), btnSize, Color.DarkBlue));
        toolPanel.Controls.Add(new Label { Text = "", Height = 10 });
        toolPanel.Controls.Add(CreateToolButton("校正設定", SetResolution, btnSize));

        mainSplit.Panel1.Controls.Add(toolPanel);

        // 右側分割
        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 750
        };

        // 影像區
        _imageCanvas = new ImageCanvas { Dock = DockStyle.Fill, AllowDrop = true };
        _imageCanvas.MousePositionChanged += (s, pos) =>
        {
            _mouseLabel.Text = $"座標: ({pos.X:F1}, {pos.Y:F1})";
        };
        _imageCanvas.DragDrop += ImageCanvas_DragDrop;
        _imageCanvas.DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };

        rightSplit.Panel1.Controls.Add(_imageCanvas);

        // 結果面板
        var resultPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        resultPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        resultPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        // 結果列表
        _resultGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White
        };
        _resultGrid.Columns.Add("Type", "類型");
        _resultGrid.Columns.Add("Name", "名稱");
        _resultGrid.Columns.Add("Summary", "摘要");
        _resultGrid.Columns["Type"]!.Width = 60;
        _resultGrid.Columns["Name"]!.Width = 70;
        _resultGrid.SelectionChanged += ResultGrid_SelectionChanged;
        _resultGrid.KeyDown += ResultGrid_KeyDown;

        var resultGroup = new GroupBox { Text = "量測結果", Dock = DockStyle.Fill, Padding = new Padding(5) };
        resultGroup.Controls.Add(_resultGrid);
        resultPanel.Controls.Add(resultGroup, 0, 0);

        // 詳細資訊
        _detailsBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            Font = new Font("Consolas", 9.5f),
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White
        };
        var detailGroup = new GroupBox { Text = "詳細資訊", Dock = DockStyle.Fill, Padding = new Padding(5) };
        detailGroup.Controls.Add(_detailsBox);
        resultPanel.Controls.Add(detailGroup, 0, 1);

        rightSplit.Panel2.Controls.Add(resultPanel);

        mainSplit.Panel2.Controls.Add(rightSplit);
        Controls.Add(mainSplit);
    }

    private Button CreateToolButton(string text, Action onClick, DrawingSize size, Color? foreColor = null)
    {
        var btn = new Button
        {
            Text = text,
            Size = size,
            FlatStyle = FlatStyle.Flat
        };
        if (foreColor.HasValue)
            btn.ForeColor = foreColor.Value;
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private void SetupTools()
    {
        _circleFinderTool.MeasurementCompleted += OnMeasurementCompleted;
        _lineFinderTool.MeasurementCompleted += OnMeasurementCompleted;
        _pointToPointTool.MeasurementCompleted += OnMeasurementCompleted;
        _angleTool.MeasurementCompleted += OnMeasurementCompleted;
        _subpixelEdgeTool.MeasurementCompleted += OnMeasurementCompleted;
        _subpixelEdgeTool.AnalysisCompleted += OnSubpixelAnalysisCompleted;
    }

    private void SetupShortcuts()
    {
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && _imageCanvas.SelectedResult != null)
            {
                DeleteSelectedResult();
            }
        };
    }

    private void OnMeasurementCompleted(object? sender, MeasurementResult result)
    {
        _imageCanvas.AddResult(result);
        UpdateResultGrid();
        _statusLabel.Text = $"完成: {result.GetSummary(_calibration.Resolution)}";
    }

    private void OnSubpixelAnalysisCompleted(object? sender, SubpixelAnalysisResult result)
    {
        // 顯示亞像素分析結果
        ShowSubpixelAnalysisResult(result);
    }

    private void SelectTool(ITool tool)
    {
        _currentTool = tool;
        _imageCanvas.SetTool(tool);
        _toolLabel.Text = $"工具: {tool.Name}";
        _statusLabel.Text = tool.Description;
    }

    private void LoadImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "選擇影像",
            Filter = "影像檔案|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|所有檔案|*.*"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadImageFile(dialog.FileName);
        }
    }

    private void LoadImageFile(string path)
    {
        try
        {
            var mat = Cv2.ImRead(path, ImreadModes.Grayscale);
            if (mat.Empty())
            {
                MessageBox.Show("無法載入影像", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var colorMat = new Mat();
            Cv2.CvtColor(mat, colorMat, ColorConversionCodes.GRAY2BGR);
            var bitmap = MatToBitmap(colorMat);

            _imageCanvas.SetImage(bitmap, mat);
            _currentImagePath = path;
            _statusLabel.Text = $"已載入: {Path.GetFileName(path)} ({mat.Width}x{mat.Height})";

            mat.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GenerateSyntheticImage()
    {
        using var dialog = new SyntheticImageDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var mat = TestImageGenerator.GenerateSyntheticEdgeImage(
                    dialog.Parameters.Width,
                    dialog.Parameters.Height,
                    dialog.Parameters.EdgePosition,
                    dialog.Parameters.LeftGray,
                    dialog.Parameters.RightGray,
                    dialog.Parameters.BlurSigma,
                    dialog.Parameters.AddNoise,
                    dialog.Parameters.NoiseSigma);

                using var colorMat = new Mat();
                Cv2.CvtColor(mat, colorMat, ColorConversionCodes.GRAY2BGR);
                var bitmap = MatToBitmap(colorMat);

                _imageCanvas.SetImage(bitmap, mat);
                _imageCanvas.TrueEdgePosition = dialog.Parameters.EdgePosition;
                _currentImagePath = null;
                _statusLabel.Text = $"合成影像 ({dialog.Parameters.Width}x{dialog.Parameters.Height})，真實邊緣: {dialog.Parameters.EdgePosition:F2}px";

                mat.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"產生失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private static Bitmap MatToBitmap(Mat mat)
    {
        var bitmap = new Bitmap(mat.Width, mat.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        var bmpData = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
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

    private void ImageCanvas_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            LoadImageFile(files[0]);
        }
    }

    private void UpdateResultGrid()
    {
        _resultGrid.Rows.Clear();
        foreach (var result in _imageCanvas.Results)
        {
            _resultGrid.Rows.Add(result.ResultType, result.Name, result.GetSummary(_calibration.Resolution));
        }
    }

    private void ResultGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_resultGrid.SelectedRows.Count > 0)
        {
            int idx = _resultGrid.SelectedRows[0].Index;
            if (idx >= 0 && idx < _imageCanvas.Results.Count)
            {
                var result = _imageCanvas.Results[idx];
                _imageCanvas.SelectResult(result);
                ShowResultDetails(result);
            }
        }
    }

    private void ResultGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelectedResult();
        }
    }

    private void DeleteSelectedResult()
    {
        if (_imageCanvas.SelectedResult != null)
        {
            _imageCanvas.RemoveResult(_imageCanvas.SelectedResult);
            UpdateResultGrid();
            _detailsBox.Clear();
        }
    }

    private void ShowResultDetails(MeasurementResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"類型: {result.ResultType}");
        sb.AppendLine($"名稱: {result.Name}");
        sb.AppendLine($"時間: {result.Timestamp:HH:mm:ss}");
        sb.AppendLine();

        if (result is CircleResult circle)
        {
            sb.AppendLine($"圓心 X: {circle.Center.X:F3} px ({_calibration.PixelsToMm(circle.Center.X):F4} mm)");
            sb.AppendLine($"圓心 Y: {circle.Center.Y:F3} px ({_calibration.PixelsToMm(circle.Center.Y):F4} mm)");
            sb.AppendLine($"半徑: {circle.RadiusPixels:F3} px ({circle.RadiusMm(_calibration.Resolution):F4} mm)");
            sb.AppendLine($"直徑: {circle.DiameterPixels:F3} px ({circle.DiameterMm(_calibration.Resolution):F4} mm)");
            if (circle.RSquared.HasValue)
                sb.AppendLine($"擬合品質: R² = {circle.RSquared.Value:F4}");
        }
        else if (result is LineResult line)
        {
            sb.AppendLine($"起點: ({line.StartPoint.X:F2}, {line.StartPoint.Y:F2}) px");
            sb.AppendLine($"終點: ({line.EndPoint.X:F2}, {line.EndPoint.Y:F2}) px");
            sb.AppendLine($"角度: {line.AngleDegrees:F3}°");
            sb.AppendLine($"長度: {line.LengthPixels:F3} px ({line.LengthMm(_calibration.Resolution):F4} mm)");
            if (line.RSquared.HasValue)
                sb.AppendLine($"擬合品質: R² = {line.RSquared.Value:F4}");
            sb.AppendLine($"邊緣點數: {line.EdgePoints.Count}");
        }
        else if (result is PointToPointResult p2p)
        {
            sb.AppendLine($"點 1: ({p2p.Point1.X:F2}, {p2p.Point1.Y:F2}) px");
            sb.AppendLine($"點 2: ({p2p.Point2.X:F2}, {p2p.Point2.Y:F2}) px");
            sb.AppendLine($"水平距離: {p2p.HorizontalDistance:F3} px");
            sb.AppendLine($"垂直距離: {p2p.VerticalDistance:F3} px");
            sb.AppendLine($"直線距離: {p2p.Distance:F3} px ({_calibration.PixelsToMm(p2p.Distance):F4} mm)");
            sb.AppendLine($"角度: {p2p.AngleDegrees:F3}°");
        }
        else if (result is AngleResult angle)
        {
            sb.AppendLine($"端點 1: ({angle.Point1.X:F2}, {angle.Point1.Y:F2}) px");
            sb.AppendLine($"頂點: ({angle.Vertex.X:F2}, {angle.Vertex.Y:F2}) px");
            sb.AppendLine($"端點 2: ({angle.Point2.X:F2}, {angle.Point2.Y:F2}) px");
            sb.AppendLine($"夾角: {angle.Angle:F3}°");
            sb.AppendLine($"補角: {angle.SupplementaryAngle:F3}°");
        }
        else if (result is SubpixelEdgeResult subpixel)
        {
            sb.AppendLine($"ROI: ({subpixel.RoiStart.X:F1},{subpixel.RoiStart.Y:F1}) → ({subpixel.RoiEnd.X:F1},{subpixel.RoiEnd.Y:F1})");
            sb.AppendLine();
            sb.AppendLine("═══ 亞像素邊緣檢測比較 ═══");
            sb.AppendLine();
            foreach (var r in subpixel.MethodResults)
            {
                string pos = r.Position.HasValue ? $"{r.Position.Value:F3}" : "FAILED";
                string err = r.Error.HasValue ? $"{(r.Error.Value >= 0 ? "+" : "")}{r.Error.Value:F3}" : "-";
                string rsq = r.RSquared.HasValue ? $"R²={r.RSquared.Value:F3}" : "";
                sb.AppendLine($"【{r.MethodName,-10}】");
                sb.AppendLine($"  位置: {pos} px");
                sb.AppendLine($"  誤差: {err} px");
                sb.AppendLine($"  耗時: {r.ElapsedMs:F2} ms");
                if (!string.IsNullOrEmpty(rsq))
                    sb.AppendLine($"  品質: {rsq}");
                sb.AppendLine();
            }
        }

        _detailsBox.Text = sb.ToString();
    }

    private void ShowSubpixelAnalysisResult(SubpixelAnalysisResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine("    亞像素邊緣檢測分析結果");
        sb.AppendLine("═══════════════════════════════════");
        sb.AppendLine();

        if (result.TrueEdgePosition.HasValue)
        {
            sb.AppendLine($"真實邊緣位置: {result.TrueEdgePosition.Value:F3} px");
            sb.AppendLine();
        }

        sb.AppendLine($"{"方法",-12} {"位置(px)",10} {"誤差(px)",10} {"耗時(ms)",10} {"品質",12}");
        sb.AppendLine(new string('-', 56));

        foreach (var r in result.Results)
        {
            string pos = r.Position.HasValue ? $"{r.Position.Value:F3}" : "FAILED";
            string err = r.Error.HasValue ? $"{(r.Error.Value >= 0 ? "+" : "")}{r.Error.Value:F3}" : "-";
            string elapsed = $"{r.ElapsedMs:F2}";
            string quality = r.RSquared.HasValue ? $"R²={r.RSquared.Value:F3}" : "-";

            sb.AppendLine($"{r.MethodName,-12} {pos,10} {err,10} {elapsed,10} {quality,12}");
        }

        sb.AppendLine();
        sb.AppendLine($"剖面取樣點數: {result.GrayProfile.Length}");
        sb.AppendLine($"梯度點數: {result.GradientProfile.Length}");

        _detailsBox.Text = sb.ToString();
    }

    private void ClearAllResults()
    {
        _imageCanvas.ClearResults();
        UpdateResultGrid();
        _detailsBox.Clear();
        _statusLabel.Text = "已清除所有結果";
    }

    private void SetResolution()
    {
        using var dialog = new ResolutionDialog(_calibration.Resolution);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _calibration.SetResolution(dialog.Resolution);
            UpdateResolutionDisplay();
            UpdateResultGrid();
        }
    }

    private void CalibrateWithCircle()
    {
        var circles = _imageCanvas.Results.OfType<CircleResult>().ToList();
        if (circles.Count == 0)
        {
            MessageBox.Show("請先使用「找圓」工具檢測一個標準圓", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var circle = circles[^1];
        using var dialog = new CircleCalibrationDialog(circle.DiameterPixels);
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _calibration.CalibrateWithCircle(dialog.MeasuredDiameterPx, dialog.ActualDiameterMm);
            UpdateResolutionDisplay();
            UpdateResultGrid();
        }
    }

    private void UpdateResolutionDisplay()
    {
        _resolutionLabel.Text = $"解析度: {_calibration.GetResolutionString()}";
    }

    private void ExportCsv()
    {
        if (_imageCanvas.Results.Count == 0)
        {
            MessageBox.Show("無量測結果可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV 檔案|*.csv",
            FileName = $"measurement_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            ExportHelper.ExportToCsv(dialog.FileName, _imageCanvas.Results, _calibration);
            MessageBox.Show("匯出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ExportHtml()
    {
        if (_imageCanvas.Results.Count == 0)
        {
            MessageBox.Show("無量測結果可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "HTML 檔案|*.html",
            FileName = $"measurement_report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            string? imageBase64 = null;
            try
            {
                using var bitmap = new Bitmap(_imageCanvas.Width, _imageCanvas.Height);
                _imageCanvas.DrawToBitmap(bitmap, new Rectangle(0, 0, _imageCanvas.Width, _imageCanvas.Height));
                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                imageBase64 = Convert.ToBase64String(ms.ToArray());
            }
            catch { }

            ExportHelper.ExportHtmlReport(dialog.FileName, _imageCanvas.Results, _calibration, imageBase64);
            MessageBox.Show("匯出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SaveScreenshot()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "PNG 圖片|*.png",
            FileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            ExportHelper.SaveControlImage(_imageCanvas, dialog.FileName);
            MessageBox.Show("儲存成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "Vision Measurement Toolkit\n" +
            "影像量測工具集\n\n" +
            "版本：1.0.0\n\n" +
            "功能：\n" +
            "• 找圓（霍夫/邊緣擬合）\n" +
            "• 找線（卡尺法）\n" +
            "• 點到點距離\n" +
            "• 角度量測\n" +
            "• 亞像素邊緣檢測比較\n" +
            "  - Parabolic Fit\n" +
            "  - Gaussian Fit\n" +
            "  - Moment Method\n" +
            "  - Sigmoid Fit\n" +
            "• 像素解析度校正\n\n" +
            "快捷鍵：\n" +
            "• Ctrl+O: 開啟影像\n" +
            "• 1-5: 切換工具\n" +
            "• Delete: 刪除選取結果\n" +
            "• Escape: 取消操作\n" +
            "• 滾輪: 縮放影像\n" +
            "• 空白鍵+拖曳: 平移影像",
            "關於",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
