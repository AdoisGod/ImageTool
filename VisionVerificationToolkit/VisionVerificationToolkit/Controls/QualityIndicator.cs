using VisionVerificationToolkit.Processing;

namespace VisionVerificationToolkit.Controls;

/// <summary>
/// 影像品質指標顯示控件
/// </summary>
public class QualityIndicator : Panel
{
    private ImageQualityResult? _result;

    private Label _sharpnessLabel = null!;
    private ProgressBar _sharpnessBar = null!;
    private Label _contrastLabel = null!;
    private ProgressBar _contrastBar = null!;
    private Label _brightnessLabel = null!;
    private ProgressBar _brightnessBar = null!;
    private Label _noiseLabel = null!;
    private ProgressBar _noiseBar = null!;
    private Label _dynamicRangeLabel = null!;
    private ProgressBar _dynamicRangeBar = null!;
    private Label _overallLabel = null!;

    public QualityIndicator()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AutoScroll = true;
        Padding = new Padding(10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 7,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));

        int row = 0;

        // 銳利度
        layout.Controls.Add(new Label { Text = "銳利度:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _sharpnessBar = new ProgressBar { Dock = DockStyle.Fill, Maximum = 600 };
        layout.Controls.Add(_sharpnessBar, 1, row);
        _sharpnessLabel = new Label { Text = "-", AutoSize = true, Anchor = AnchorStyles.Right };
        layout.Controls.Add(_sharpnessLabel, 2, row++);

        // 對比度
        layout.Controls.Add(new Label { Text = "對比度:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _contrastBar = new ProgressBar { Dock = DockStyle.Fill, Maximum = 100 };
        layout.Controls.Add(_contrastBar, 1, row);
        _contrastLabel = new Label { Text = "-", AutoSize = true, Anchor = AnchorStyles.Right };
        layout.Controls.Add(_contrastLabel, 2, row++);

        // 亮度
        layout.Controls.Add(new Label { Text = "亮度:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _brightnessBar = new ProgressBar { Dock = DockStyle.Fill, Maximum = 255 };
        layout.Controls.Add(_brightnessBar, 1, row);
        _brightnessLabel = new Label { Text = "-", AutoSize = true, Anchor = AnchorStyles.Right };
        layout.Controls.Add(_brightnessLabel, 2, row++);

        // 雜訊
        layout.Controls.Add(new Label { Text = "雜訊:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _noiseBar = new ProgressBar { Dock = DockStyle.Fill, Maximum = 100 };
        layout.Controls.Add(_noiseBar, 1, row);
        _noiseLabel = new Label { Text = "-", AutoSize = true, Anchor = AnchorStyles.Right };
        layout.Controls.Add(_noiseLabel, 2, row++);

        // 動態範圍
        layout.Controls.Add(new Label { Text = "動態範圍:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _dynamicRangeBar = new ProgressBar { Dock = DockStyle.Fill, Maximum = 255 };
        layout.Controls.Add(_dynamicRangeBar, 1, row);
        _dynamicRangeLabel = new Label { Text = "-", AutoSize = true, Anchor = AnchorStyles.Right };
        layout.Controls.Add(_dynamicRangeLabel, 2, row++);

        // 分隔線
        layout.Controls.Add(new Label { Text = "", Height = 10 }, 0, row++);

        // 整體評價
        _overallLabel = new Label
        {
            Text = "整體評價：-",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
            ForeColor = Color.DarkGreen
        };
        layout.Controls.Add(_overallLabel, 0, row);
        layout.SetColumnSpan(_overallLabel, 3);

        Controls.Add(layout);
    }

    public void SetResult(ImageQualityResult? result)
    {
        _result = result;

        if (result == null)
        {
            _sharpnessLabel.Text = "-";
            _sharpnessBar.Value = 0;
            _contrastLabel.Text = "-";
            _contrastBar.Value = 0;
            _brightnessLabel.Text = "-";
            _brightnessBar.Value = 0;
            _noiseLabel.Text = "-";
            _noiseBar.Value = 0;
            _dynamicRangeLabel.Text = "-";
            _dynamicRangeBar.Value = 0;
            _overallLabel.Text = "整體評價：-";
            return;
        }

        // 銳利度
        _sharpnessLabel.Text = $"{result.Sharpness:F0} ({result.SharpnessLevel})";
        _sharpnessLabel.ForeColor = result.SharpnessColor;
        _sharpnessBar.Value = Math.Min(600, (int)result.Sharpness);

        // 對比度
        _contrastLabel.Text = $"{result.Contrast:F1} ({result.ContrastLevel})";
        _contrastLabel.ForeColor = result.ContrastColor;
        _contrastBar.Value = Math.Min(100, (int)result.Contrast);

        // 亮度
        _brightnessLabel.Text = $"{result.Brightness:F0} ({result.BrightnessLevel})";
        _brightnessLabel.ForeColor = result.BrightnessColor;
        _brightnessBar.Value = Math.Min(255, (int)result.Brightness);

        // 雜訊
        _noiseLabel.Text = $"{result.NoiseLevel:F2} ({result.NoiseDescription})";
        _noiseLabel.ForeColor = result.NoiseColor;
        _noiseBar.Value = Math.Min(100, (int)(result.NoiseLevel * 100));

        // 動態範圍
        _dynamicRangeLabel.Text = $"{result.DynamicRange:F0} ({result.DynamicRangeLevel})";
        _dynamicRangeLabel.ForeColor = result.DynamicRangeColor;
        _dynamicRangeBar.Value = Math.Min(255, (int)result.DynamicRange);

        // 整體評價
        _overallLabel.Text = $"建議：{result.GetOverallAssessment()}";
    }

    public void Clear()
    {
        SetResult(null);
    }
}
