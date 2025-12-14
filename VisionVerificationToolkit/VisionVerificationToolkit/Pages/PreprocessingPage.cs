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

        // 主分割
        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 300,
            FixedPanel = FixedPanel.Panel2
        };

        // 左側：影像顯示
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        // 工具列
        var toolbar = new ToolStrip();
        toolbar.Items.Add(new ToolStripButton("載入影像", null, (s, e) => LoadImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("儲存結果", null, (s, e) => SaveResult()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("重置", null, (s, e) => ResetPipeline()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());

        var presetCombo = new ToolStripComboBox() { DropDownStyle = ComboBoxStyle.DropDownList };
        presetCombo.Items.AddRange(new[] { "選擇預設...", "標準邊緣檢測", "低對比度影像", "高雜訊環境", "模糊邊界" });
        presetCombo.SelectedIndex = 0;
        presetCombo.SelectedIndexChanged += PresetCombo_SelectedIndexChanged;
        toolbar.Items.Add(new ToolStripLabel("常用預設:"));
        toolbar.Items.Add(presetCombo);

        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("傳送至檢測", null, (s, e) => SendToDetection?.Invoke(this, EventArgs.Empty)) { DisplayStyle = ToolStripItemDisplayStyle.Text });

        leftPanel.Controls.Add(toolbar);

        // 影像畫布
        _canvas = new DualImageCanvas { Dock = DockStyle.Fill };
        leftPanel.Controls.Add(_canvas);

        // 品質指標
        _qualityIndicator = new QualityIndicator
        {
            Dock = DockStyle.Bottom,
            Height = 160,
            BorderStyle = BorderStyle.FixedSingle
        };
        leftPanel.Controls.Add(_qualityIndicator);

        mainSplit.Panel1.Controls.Add(leftPanel);

        // 右側：管線設定
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

        // 管線列表
        var pipelineGroup = new GroupBox { Text = "處理管線", Dock = DockStyle.Top, Height = 200 };

        _pipelineList = new ListBox { Dock = DockStyle.Fill };
        _pipelineList.SelectedIndexChanged += PipelineList_SelectedIndexChanged;
        pipelineGroup.Controls.Add(_pipelineList);

        var pipelineButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30, FlowDirection = FlowDirection.LeftToRight };
        pipelineButtons.Controls.Add(CreateButton("上移", () => MoveStep(-1)));
        pipelineButtons.Controls.Add(CreateButton("下移", () => MoveStep(1)));
        pipelineButtons.Controls.Add(CreateButton("刪除", DeleteStep));
        pipelineButtons.Controls.Add(CreateButton("清空", () => { _pipelineManager.Clear(); RefreshPipelineList(); }));
        pipelineGroup.Controls.Add(pipelineButtons);

        rightPanel.Controls.Add(pipelineGroup);

        // 新增步驟
        var addGroup = new GroupBox { Text = "新增步驟", Dock = DockStyle.Top, Height = 80, Top = 210 };
        var addPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        _stepTypeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var category in StepFactory.GetAllCategories())
        {
            foreach (var (name, typeName) in StepFactory.GetStepsByCategory(category))
            {
                _stepTypeCombo.Items.Add(new StepItem(name, typeName, category));
            }
        }
        if (_stepTypeCombo.Items.Count > 0) _stepTypeCombo.SelectedIndex = 0;

        addPanel.Controls.Add(_stepTypeCombo, 0, 0);
        addPanel.Controls.Add(CreateButton("新增", AddStep), 1, 0);

        _livePreviewCheck = new CheckBox { Text = "即時預覽", Checked = true, Dock = DockStyle.Fill };
        _livePreviewCheck.CheckedChanged += (s, e) => { if (_livePreviewCheck.Checked) ExecutePipeline(); };
        addPanel.Controls.Add(_livePreviewCheck, 0, 1);
        addPanel.Controls.Add(CreateButton("套用", () => ExecutePipeline()), 1, 1);

        addGroup.Controls.Add(addPanel);
        rightPanel.Controls.Add(addGroup);

        // 參數設定
        var paramGroup = new GroupBox { Text = "步驟參數", Dock = DockStyle.Fill };
        _parameterGrid = new PropertyGrid { Dock = DockStyle.Fill };
        _parameterGrid.PropertyValueChanged += (s, e) => { if (_livePreviewCheck.Checked) ExecutePipeline(); };
        paramGroup.Controls.Add(_parameterGrid);
        rightPanel.Controls.Add(paramGroup);

        // 狀態標籤
        _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 25, TextAlign = ContentAlignment.MiddleLeft };
        rightPanel.Controls.Add(_statusLabel);

        mainSplit.Panel2.Controls.Add(rightPanel);
        Controls.Add(mainSplit);
    }

    private Button CreateButton(string text, Action onClick)
    {
        var btn = new Button { Text = text, Width = 60, Height = 25 };
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
                _imageManager.LoadImage(dialog.FileName);
                _statusLabel.Text = $"已載入: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
    }

    private void ResetPipeline()
    {
        _pipelineManager.Clear();
        _imageManager.ResetProcessedImage();
    }

    private void PresetCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        var combo = sender as ToolStripComboBox;
        if (combo?.SelectedIndex <= 0) return;

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
