using OpenCvSharp;
using VisionVerificationToolkit.Controls;
using VisionVerificationToolkit.Core;
using VisionVerificationToolkit.Processing;
using DrawingSize = System.Drawing.Size;

namespace VisionVerificationToolkit.Pages;

/// <summary>
/// 頁面1：前處理
/// </summary>
public class PreprocessingPage : UserControl
{
    private readonly ImageManager _imageManager;
    private readonly PipelineManager _pipelineManager;

    private DualImageCanvas _canvas = null!;
    private QualityIndicator _qualityIndicator = null!;
    private ListBox _pipelineList = null!;
    private PropertyGrid _parameterGrid = null!;
    private ComboBox _stepTypeCombo = null!;
    private CheckBox _livePreviewCheck = null!;
    private Label _statusLabel = null!;

    public event EventHandler? SendToDetection;

    public ImageManager ImageManager => _imageManager;
    public PipelineManager PipelineManager => _pipelineManager;

    public PreprocessingPage(ImageManager imageManager, PipelineManager pipelineManager)
    {
        _imageManager = imageManager;
        _pipelineManager = pipelineManager;
        InitializeComponent();
        SetupEvents();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;

        // 主分割 - 左右分割
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 800,
            FixedPanel = FixedPanel.None
        };

        // ===== 左側：影像顯示 =====
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        // 工具列
        var toolbar = new ToolStrip { Dock = DockStyle.Top };
        toolbar.Items.Add(new ToolStripButton("載入影像", null, (s, e) => LoadImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("儲存結果", null, (s, e) => SaveResult()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("儲存管線", null, (s, e) => SavePipeline()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("載入管線", null, (s, e) => LoadPipeline()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("重置", null, (s, e) => ResetPipeline()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());

        var presetCombo = new ToolStripComboBox() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        presetCombo.Items.AddRange(new[] { "選擇預設...", "標準邊緣檢測", "低對比度影像", "高雜訊環境", "模糊邊界" });
        presetCombo.SelectedIndex = 0;
        presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
        toolbar.Items.Add(new ToolStripLabel("常用預設:"));
        toolbar.Items.Add(presetCombo);

        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("傳送至檢測 →", null, (s, e) => SendToDetection?.Invoke(this, EventArgs.Empty)) { DisplayStyle = ToolStripItemDisplayStyle.Text });

        // 品質指標 (底部)
        _qualityIndicator = new QualityIndicator
        {
            Dock = DockStyle.Bottom,
            Height = 150,
            BorderStyle = BorderStyle.FixedSingle
        };

        // 影像畫布 (填滿)
        _canvas = new DualImageCanvas { Dock = DockStyle.Fill };

        // 按正確順序加入控件 (後加的先處理 Dock)
        leftPanel.Controls.Add(_canvas);
        leftPanel.Controls.Add(_qualityIndicator);
        leftPanel.Controls.Add(toolbar);

        mainSplit.Panel1.Controls.Add(leftPanel);

        // ===== 右側：管線設定 =====
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

        // 狀態標籤 (底部)
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            BorderStyle = BorderStyle.FixedSingle
        };

        // 參數設定 (填滿剩餘空間)
        var paramGroup = new GroupBox { Text = "步驟參數", Dock = DockStyle.Fill, Padding = new Padding(5) };
        _parameterGrid = new PropertyGrid { Dock = DockStyle.Fill, HelpVisible = true };
        _parameterGrid.PropertyValueChanged += (s, e) => { if (_livePreviewCheck.Checked) ExecutePipeline(); };
        paramGroup.Controls.Add(_parameterGrid);

        // 新增步驟區 (中間)
        var addGroup = new GroupBox { Text = "新增步驟", Dock = DockStyle.Top, Height = 90, Padding = new Padding(5) };
        var addPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(3)
        };
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75));
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        addPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        addPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _stepTypeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var category in StepFactory.GetAllCategories())
        {
            foreach (var (name, typeName) in StepFactory.GetStepsByCategory(category))
            {
                _stepTypeCombo.Items.Add(new StepItem(name, typeName, category));
            }
        }
        if (_stepTypeCombo.Items.Count > 0) _stepTypeCombo.SelectedIndex = 0;

        var addBtn = new Button { Text = "新增", Dock = DockStyle.Fill };
        addBtn.Click += (s, e) => AddStep();

        _livePreviewCheck = new CheckBox { Text = "即時預覽", Checked = true, Dock = DockStyle.Fill };
        _livePreviewCheck.CheckedChanged += (s, e) => { if (_livePreviewCheck.Checked) ExecutePipeline(); };

        var applyBtn = new Button { Text = "套用", Dock = DockStyle.Fill };
        applyBtn.Click += (s, e) => ExecutePipeline();

        addPanel.Controls.Add(_stepTypeCombo, 0, 0);
        addPanel.Controls.Add(addBtn, 1, 0);
        addPanel.Controls.Add(_livePreviewCheck, 0, 1);
        addPanel.Controls.Add(applyBtn, 1, 1);
        addGroup.Controls.Add(addPanel);

        // 管線列表 (上方)
        var pipelineGroup = new GroupBox { Text = "處理管線", Dock = DockStyle.Top, Height = 220 };
        _pipelineList = new ListBox { Dock = DockStyle.Fill };
        _pipelineList.SelectedIndexChanged += PipelineList_SelectedIndexChanged;
        _pipelineList.DoubleClick += (s, e) => ToggleStepEnabled();

        var pipelineButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, FlowDirection = FlowDirection.LeftToRight };
        pipelineButtons.Controls.Add(CreateButton("↑上移", () => MoveStep(-1)));
        pipelineButtons.Controls.Add(CreateButton("↓下移", () => MoveStep(1)));
        pipelineButtons.Controls.Add(CreateButton("刪除", DeleteStep));
        pipelineButtons.Controls.Add(CreateButton("啟用/停用", ToggleStepEnabled));
        pipelineButtons.Controls.Add(CreateButton("清空", () => { _pipelineManager.Clear(); RefreshPipelineList(); }));

        pipelineGroup.Controls.Add(_pipelineList);
        pipelineGroup.Controls.Add(pipelineButtons);

        // 按正確順序加入右側控件
        rightPanel.Controls.Add(paramGroup);
        rightPanel.Controls.Add(addGroup);
        rightPanel.Controls.Add(pipelineGroup);
        rightPanel.Controls.Add(_statusLabel);

        mainSplit.Panel2.Controls.Add(rightPanel);
        Controls.Add(mainSplit);
    }

    private Button CreateButton(string text, Action onClick)
    {
        var btn = new Button { Text = text, Width = 70, Height = 26 };
        btn.Click += (s, e) => onClick();
        return btn;
    }

    private void SetupEvents()
    {
        _imageManager.OriginalImageChanged += (s, e) =>
        {
            if (_imageManager.OriginalImage != null)
            {
                _canvas.SetOriginalImage(_imageManager.OriginalImage);
                // 只在載入時分析一次品質
                AnalyzeQuality();
            }
        };

        _imageManager.ProcessedImageChanged += (s, e) =>
        {
            if (_imageManager.ProcessedImage != null)
            {
                _canvas.SetProcessedImage(_imageManager.ProcessedImage);
            }
        };

        _pipelineManager.PipelineChanged += (s, e) => RefreshPipelineList();
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
            try
            {
                Cursor = Cursors.WaitCursor;
                _statusLabel.Text = "載入中...";
                Application.DoEvents();

                _imageManager.LoadImage(dialog.FileName);
                _statusLabel.Text = $"已載入: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "載入失敗";
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }

    private void SaveResult()
    {
        if (_imageManager.ProcessedImage == null)
        {
            MessageBox.Show("無處理後影像", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
            FileName = "processed"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _imageManager.SaveProcessedImage(dialog.FileName);
            MessageBox.Show("儲存成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SavePipeline()
    {
        if (_pipelineManager.Count == 0)
        {
            MessageBox.Show("管線中沒有步驟", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "儲存管線",
            Filter = "管線檔案|*.pipeline.json|JSON|*.json",
            FileName = "pipeline"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _pipelineManager.SaveToFile(dialog.FileName);
                MessageBox.Show("管線儲存成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _statusLabel.Text = $"已儲存管線: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"儲存失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void LoadPipeline()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "載入管線",
            Filter = "管線檔案|*.pipeline.json;*.json|所有檔案|*.*"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                _pipelineManager.LoadFromFile(dialog.FileName, StepFactory.Create);
                _statusLabel.Text = $"已載入管線: {Path.GetFileName(dialog.FileName)} ({_pipelineManager.Count} 步驟)";

                if (_imageManager.OriginalImage != null && _livePreviewCheck.Checked)
                {
                    ExecutePipeline();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void AnalyzeQuality()
    {
        if (_imageManager.OriginalImage == null) return;

        var result = ImageQualityAnalyzer.Analyze(_imageManager.OriginalImage);
        _qualityIndicator.SetResult(result);
    }

    private void RefreshPipelineList()
    {
        _pipelineList.Items.Clear();
        for (int i = 0; i < _pipelineManager.Steps.Count; i++)
        {
            var step = _pipelineManager.Steps[i];
            string enabled = step.IsEnabled ? "●" : "○";
            _pipelineList.Items.Add($"{enabled} [{i + 1}] {step.Name}: {step.GetParameterDescription()}");
        }
    }

    private void PipelineList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int idx = _pipelineList.SelectedIndex;
        var step = _pipelineManager.GetStep(idx);
        _parameterGrid.SelectedObject = step;
    }

    private void ToggleStepEnabled()
    {
        int idx = _pipelineList.SelectedIndex;
        var step = _pipelineManager.GetStep(idx);
        if (step != null)
        {
            step.IsEnabled = !step.IsEnabled;
            RefreshPipelineList();
            _pipelineList.SelectedIndex = idx;
            if (_livePreviewCheck.Checked) ExecutePipeline();
        }
    }

    private void AddStep()
    {
        if (_stepTypeCombo.SelectedItem is StepItem item)
        {
            var step = StepFactory.Create(item.TypeName);
            if (step != null)
            {
                _pipelineManager.AddStep(step);
                if (_livePreviewCheck.Checked) ExecutePipeline();
            }
        }
    }

    private void DeleteStep()
    {
        int idx = _pipelineList.SelectedIndex;
        if (idx >= 0)
        {
            _pipelineManager.RemoveStep(idx);
            if (_livePreviewCheck.Checked) ExecutePipeline();
        }
    }

    private void MoveStep(int direction)
    {
        int idx = _pipelineList.SelectedIndex;
        int newIdx = idx + direction;
        if (idx >= 0 && newIdx >= 0 && newIdx < _pipelineManager.Count)
        {
            _pipelineManager.MoveStep(idx, newIdx);
            _pipelineList.SelectedIndex = newIdx;
            if (_livePreviewCheck.Checked) ExecutePipeline();
        }
    }

    private void ExecutePipeline()
    {
        if (_imageManager.OriginalImage == null) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            _statusLabel.Text = "處理中...";
            Application.DoEvents();

            var startTime = DateTime.Now;
            using var result = _pipelineManager.Execute(_imageManager.OriginalImage);
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            _imageManager.UpdateProcessedImage(result);
            _statusLabel.Text = $"處理完成 ({elapsed:F1} ms)";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"處理失敗: {ex.Message}";
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ResetPipeline()
    {
        _pipelineManager.Clear();
        _imageManager.ResetProcessedImage();
        _statusLabel.Text = "已重置";
    }

    private void PresetCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var combo = sender as ToolStripComboBox;
        if (combo == null || combo.SelectedIndex <= 0) return;

        _pipelineManager.Clear();

        switch (combo.SelectedIndex)
        {
            case 1: // 標準邊緣檢測
                _pipelineManager.AddStep(new Processing.Steps.GaussianBlurStep { SigmaX = 1.5, SigmaY = 1.5 });
                _pipelineManager.AddStep(new Processing.Steps.CannyStep { Threshold1 = 50, Threshold2 = 150 });
                break;
            case 2: // 低對比度影像
                _pipelineManager.AddStep(new Processing.Steps.CLAHEStep());
                _pipelineManager.AddStep(new Processing.Steps.GaussianBlurStep());
                _pipelineManager.AddStep(new Processing.Steps.AdaptiveThresholdStep());
                break;
            case 3: // 高雜訊環境
                _pipelineManager.AddStep(new Processing.Steps.MedianBlurStep { KernelSize = 5 });
                _pipelineManager.AddStep(new Processing.Steps.BilateralFilterStep());
                _pipelineManager.AddStep(new Processing.Steps.OpeningStep());
                break;
            case 4: // 模糊邊界
                _pipelineManager.AddStep(new Processing.Steps.CLAHEStep());
                _pipelineManager.AddStep(new Processing.Steps.SharpenStep { Strength = 1.5 });
                _pipelineManager.AddStep(new Processing.Steps.CannyStep());
                break;
        }

        combo.SelectedIndex = 0;
        ExecutePipeline();
    }

    private class StepItem
    {
        public string Name { get; }
        public string TypeName { get; }
        public string Category { get; }

        public StepItem(string name, string typeName, string category)
        {
            Name = name;
            TypeName = typeName;
            Category = category;
        }

        public override string ToString() => $"[{Category}] {Name}";
    }
}
