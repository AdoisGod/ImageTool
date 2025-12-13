using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using OxyPlot.WindowsForms;
using SubpixelEdgeComparator.Controls;
using SubpixelEdgeComparator.Core;
using SubpixelEdgeComparator.Dialogs;
using SubpixelEdgeComparator.Utils;

namespace SubpixelEdgeComparator;

public partial class MainForm : Form
{
    private readonly ImageProcessor _processor = new();
    private FullAnalysisResult? _lastResult;
    private double? _syntheticEdgePosition;
    private string? _currentImagePath;

    // UI 控件
    private MenuStrip _menuStrip = null!;
    private ToolStrip _toolStrip = null!;
    private StatusStrip _statusStrip = null!;
    private SplitContainer _mainSplit = null!;
    private ImageCanvas _imageCanvas = null!;
    private PlotView _grayPlot = null!;
    private PlotView _gradientPlot = null!;
    private DataGridView _resultGrid = null!;

    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripStatusLabel _mouseLabel = null!;
    private ToolStripStatusLabel _roiLabel = null!;

    public MainForm()
    {
        InitializeComponent();
        SetupShortcuts();
    }

    private void InitializeComponent()
    {
        Text = "Subpixel Edge Comparator - 亞像素邊緣檢測比較工具";
        Size = new Size(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 700);

        // 選單列
        CreateMenuStrip();

        // 工具列
        CreateToolStrip();

        // 狀態列
        CreateStatusStrip();

        // 主要分割佈局
        CreateMainLayout();

        // 事件綁定
        _imageCanvas.RoiChanged += (s, e) => UpdateRoiStatus();
        _imageCanvas.MousePositionChanged += (s, e) => UpdateMousePosition(e);
    }

    private void CreateMenuStrip()
    {
        _menuStrip = new MenuStrip();

        // 檔案選單
        var fileMenu = new ToolStripMenuItem("檔案(&F)");
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("開啟(&O)", null, (s, e) => LoadImage()) { ShortcutKeys = Keys.Control | Keys.O });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("匯出結果(&E)", null, (s, e) => ExportResults()) { ShortcutKeys = Keys.Control | Keys.E });
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("結束(&X)", null, (s, e) => Close()) { ShortcutKeys = Keys.Alt | Keys.F4 });
        _menuStrip.Items.Add(fileMenu);

        // 工具選單
        var toolMenu = new ToolStripMenuItem("工具(&T)");
        toolMenu.DropDownItems.Add(new ToolStripMenuItem("合成測試影像(&G)", null, (s, e) => GenerateSyntheticImage()) { ShortcutKeys = Keys.Control | Keys.G });
        toolMenu.DropDownItems.Add(new ToolStripMenuItem("執行分析(&R)", null, (s, e) => RunAnalysis()) { ShortcutKeys = Keys.Control | Keys.R });
        toolMenu.DropDownItems.Add(new ToolStripSeparator());
        toolMenu.DropDownItems.Add(new ToolStripMenuItem("清除 ROI", null, (s, e) => _imageCanvas.ClearRoi()) { ShortcutKeys = Keys.Delete });
        _menuStrip.Items.Add(toolMenu);

        // 說明選單
        var helpMenu = new ToolStripMenuItem("說明(&H)");
        helpMenu.DropDownItems.Add(new ToolStripMenuItem("關於(&A)", null, (s, e) => ShowAbout()));
        _menuStrip.Items.Add(helpMenu);

        MainMenuStrip = _menuStrip;
        Controls.Add(_menuStrip);
    }

    private void CreateToolStrip()
    {
        _toolStrip = new ToolStrip();
        _toolStrip.Items.Add(new ToolStripButton("載入影像", null, (s, e) => LoadImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        _toolStrip.Items.Add(new ToolStripButton("合成測試影像", null, (s, e) => GenerateSyntheticImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("清除 ROI", null, (s, e) => _imageCanvas.ClearRoi()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("執行分析", null, (s, e) => RunAnalysis()) { DisplayStyle = ToolStripItemDisplayStyle.Text, Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold) });
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(new ToolStripButton("匯出結果", null, (s, e) => ExportResults()) { DisplayStyle = ToolStripItemDisplayStyle.Text });

        Controls.Add(_toolStrip);
    }

    private void CreateStatusStrip()
    {
        _statusStrip = new StatusStrip();

        _statusLabel = new ToolStripStatusLabel("就緒") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _mouseLabel = new ToolStripStatusLabel("滑鼠: -") { AutoSize = false, Width = 150, TextAlign = ContentAlignment.MiddleLeft };
        _roiLabel = new ToolStripStatusLabel("ROI: 未選取") { AutoSize = false, Width = 200, TextAlign = ContentAlignment.MiddleLeft };

        _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, _mouseLabel, _roiLabel });
        Controls.Add(_statusStrip);
    }

    private void CreateMainLayout()
    {
        _mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 700
        };

        // 左側：影像顯示
        _imageCanvas = new ImageCanvas
        {
            Dock = DockStyle.Fill
        };
        _mainSplit.Panel1.Controls.Add(_imageCanvas);

        // 右側：圖表和結果
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 34));

        // 灰階剖面圖
        _grayPlot = new PlotView { Dock = DockStyle.Fill };
        InitializeGrayPlot();
        var grayGroup = CreateGroupBox("灰階剖面圖", _grayPlot);
        rightPanel.Controls.Add(grayGroup, 0, 0);

        // 梯度剖面圖
        _gradientPlot = new PlotView { Dock = DockStyle.Fill };
        InitializeGradientPlot();
        var gradientGroup = CreateGroupBox("梯度剖面圖 + 邊緣位置標記", _gradientPlot);
        rightPanel.Controls.Add(gradientGroup, 0, 1);

        // 結果表格
        _resultGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None
        };
        InitializeResultGrid();
        var resultGroup = CreateGroupBox("結果表格", _resultGrid);
        rightPanel.Controls.Add(resultGroup, 0, 2);

        _mainSplit.Panel2.Controls.Add(rightPanel);

        Controls.Add(_mainSplit);
    }

    private static GroupBox CreateGroupBox(string title, Control content)
    {
        var group = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(5)
        };
        content.Dock = DockStyle.Fill;
        group.Controls.Add(content);
        return group;
    }

    private void InitializeGrayPlot()
    {
        var model = new PlotModel { Title = "" };
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "取樣點索引" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "灰階值", Minimum = 0, Maximum = 255 });
        _grayPlot.Model = model;
    }

    private void InitializeGradientPlot()
    {
        var model = new PlotModel { Title = "" };
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "取樣點索引" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "梯度值" });
        _gradientPlot.Model = model;
    }

    private void InitializeResultGrid()
    {
        _resultGrid.Columns.Add("Method", "方法");
        _resultGrid.Columns.Add("Position", "位置 (px)");
        _resultGrid.Columns.Add("Error", "誤差 (px)");
        _resultGrid.Columns.Add("Elapsed", "耗時 (ms)");
        _resultGrid.Columns.Add("Quality", "品質");

        _resultGrid.Columns["Method"]!.Width = 80;
        _resultGrid.Columns["Position"]!.Width = 90;
        _resultGrid.Columns["Error"]!.Width = 90;
        _resultGrid.Columns["Elapsed"]!.Width = 80;
        _resultGrid.Columns["Quality"]!.Width = 100;
    }

    private void SetupShortcuts()
    {
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete)
            {
                _imageCanvas.ClearRoi();
            }
        };
    }

    private void LoadImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "選擇影像檔案",
            Filter = "影像檔案|*.png;*.jpg;*.jpeg;*.bmp;*.tiff|所有檔案|*.*"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _processor.LoadImage(dialog.FileName);
                _imageCanvas.SetImage(_processor.ToBitmap());
                _currentImagePath = dialog.FileName;
                _syntheticEdgePosition = null;

                var img = _processor.CurrentImage!;
                _statusLabel.Text = $"已載入：{Path.GetFileName(dialog.FileName)} ({img.Width}x{img.Height})";

                ClearResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入影像失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void GenerateSyntheticImage()
    {
        using var dialog = new SyntheticImageDialog();
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _processor.GenerateSyntheticImage(dialog.Parameters);
                _imageCanvas.SetImage(_processor.ToBitmap());
                _syntheticEdgePosition = dialog.Parameters.EdgePosition;
                _currentImagePath = null;

                _statusLabel.Text = $"已產生合成影像 ({dialog.Parameters.Width}x{dialog.Parameters.Height})，真實邊緣位置: {dialog.Parameters.EdgePosition:F2}";

                ClearResults();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"產生合成影像失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void RunAnalysis()
    {
        // 檢查前置條件
        if (!_processor.HasImage)
        {
            MessageBox.Show("請先載入影像", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_imageCanvas.HasValidRoi)
        {
            MessageBox.Show("請先選取有效的 ROI 線段（長度至少 10 像素）", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;

            var p1 = _imageCanvas.RoiStart!.Value;
            var p2 = _imageCanvas.RoiEnd!.Value;

            var p1d = new OpenCvSharp.Point2d(p1.X, p1.Y);
            var p2d = new OpenCvSharp.Point2d(p2.X, p2.Y);

            _lastResult = _processor.Analyze(p1d, p2d, _syntheticEdgePosition);

            UpdateGrayPlot();
            UpdateGradientPlot();
            UpdateResultGrid();

            _statusLabel.Text = "分析完成";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"分析失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void UpdateGrayPlot()
    {
        if (_lastResult == null) return;

        var model = new PlotModel { Title = "" };
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "取樣點索引" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "灰階值", Minimum = 0, Maximum = 255 });

        var series = new LineSeries
        {
            Title = "灰階剖面",
            Color = OxyColors.DarkBlue,
            StrokeThickness = 1.5
        };

        for (int i = 0; i < _lastResult.GrayProfile.Length; i++)
        {
            series.Points.Add(new DataPoint(i, _lastResult.GrayProfile[i]));
        }

        model.Series.Add(series);
        _grayPlot.Model = model;
    }

    private void UpdateGradientPlot()
    {
        if (_lastResult == null) return;

        var model = new PlotModel { Title = "" };
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "取樣點索引" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "梯度值" });

        // 梯度曲線
        var gradientSeries = new LineSeries
        {
            Title = "梯度",
            Color = OxyColors.Gray,
            StrokeThickness = 1.5
        };

        for (int i = 0; i < _lastResult.GradientProfile.Length; i++)
        {
            gradientSeries.Points.Add(new DataPoint(i, _lastResult.GradientProfile[i]));
        }

        model.Series.Add(gradientSeries);

        // 邊緣位置標記
        var colors = new Dictionary<string, OxyColor>
        {
            { "Parabolic", OxyColors.Blue },
            { "Gaussian", OxyColors.Green },
            { "Moment", OxyColors.Orange },
            { "Sigmoid", OxyColors.Red }
        };

        foreach (var result in _lastResult.Results)
        {
            if (result.Success && result.Position.HasValue && colors.TryGetValue(result.MethodName, out var color))
            {
                var annotation = new LineAnnotation
                {
                    Type = LineAnnotationType.Vertical,
                    X = result.Position.Value,
                    Color = color,
                    StrokeThickness = 2,
                    Text = result.MethodName,
                    TextColor = color,
                    TextVerticalAlignment = VerticalAlignment.Top,
                    TextHorizontalAlignment = HorizontalAlignment.Left
                };
                model.Annotations.Add(annotation);
            }
        }

        // 圖例
        model.LegendPosition = LegendPosition.TopRight;
        model.LegendPlacement = LegendPlacement.Inside;

        _gradientPlot.Model = model;
    }

    private void UpdateResultGrid()
    {
        if (_lastResult == null) return;

        _resultGrid.Rows.Clear();

        foreach (var result in _lastResult.Results)
        {
            string position = result.Position.HasValue ? result.Position.Value.ToString("F3") : "FAILED";
            string error = result.Error.HasValue ? (result.Error.Value >= 0 ? "+" : "") + result.Error.Value.ToString("F3") : "-";
            string elapsed = result.ElapsedMs.ToString("F2");
            string quality = result.RSquared.HasValue ? $"R²={result.RSquared.Value:F3}" : "-";

            int rowIndex = _resultGrid.Rows.Add(result.MethodName, position, error, elapsed, quality);

            // 設定顏色
            if (!result.Success)
            {
                _resultGrid.Rows[rowIndex].DefaultCellStyle.ForeColor = Color.Red;
            }
        }
    }

    private void ClearResults()
    {
        _lastResult = null;
        InitializeGrayPlot();
        InitializeGradientPlot();
        _resultGrid.Rows.Clear();
    }

    private void UpdateMousePosition(PointF pos)
    {
        _mouseLabel.Text = $"滑鼠: ({pos.X:F1}, {pos.Y:F1})";
    }

    private void UpdateRoiStatus()
    {
        if (_imageCanvas.HasValidRoi)
        {
            double length = _imageCanvas.GetRoiLength();
            _roiLabel.Text = $"ROI: 長度 {length:F1} px";
        }
        else if (_imageCanvas.RoiStart.HasValue)
        {
            _roiLabel.Text = "ROI: 線段太短 (< 10 px)";
        }
        else
        {
            _roiLabel.Text = "ROI: 未選取";
        }
    }

    private void ExportResults()
    {
        if (_lastResult == null)
        {
            MessageBox.Show("尚無分析結果可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "匯出結果",
            Filter = "CSV 檔案|*.csv|HTML 報告|*.html|所有檔案|*.*",
            FileName = $"analysis_result_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                string ext = Path.GetExtension(dialog.FileName).ToLower();

                if (ext == ".csv")
                {
                    ExportHelper.ExportToCsv(dialog.FileName, _lastResult, _currentImagePath);
                }
                else if (ext == ".html")
                {
                    // 先儲存圖表截圖
                    string chartPath = Path.Combine(Path.GetTempPath(), "chart_temp.png");
                    ExportHelper.SaveControlImage(_gradientPlot, chartPath);
                    ExportHelper.ExportHtmlReport(dialog.FileName, _lastResult, chartPath, _currentImagePath);
                    File.Delete(chartPath);
                }
                else
                {
                    ExportHelper.ExportToCsv(dialog.FileName, _lastResult, _currentImagePath);
                }

                MessageBox.Show("匯出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "Subpixel Edge Comparator\n" +
            "亞像素邊緣檢測演算法比較工具\n\n" +
            "版本：1.0.0\n\n" +
            "支援的演算法：\n" +
            "• 拋物線擬合 (Parabolic Fit)\n" +
            "• 高斯擬合 (Gaussian Fit)\n" +
            "• 矩法 (Moment Method)\n" +
            "• Sigmoid 擬合 (Error Function Fit)\n\n" +
            "快捷鍵：\n" +
            "• Ctrl+O: 開啟影像\n" +
            "• Ctrl+G: 合成測試影像\n" +
            "• Ctrl+R: 執行分析\n" +
            "• Ctrl+E: 匯出結果\n" +
            "• Delete: 清除 ROI\n" +
            "• Ctrl+滾輪: 縮放影像",
            "關於",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _processor.Dispose();
        }
        base.Dispose(disposing);
    }
}
