using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Dialogs;

/// <summary>
/// 合成影像參數
/// </summary>
public class SyntheticImageParams
{
    public int Width { get; set; } = 200;
    public int Height { get; set; } = 200;
    public double EdgePosition { get; set; } = 100.0;
    public int LeftGray { get; set; } = 50;
    public int RightGray { get; set; } = 200;
    public double BlurSigma { get; set; } = 1.5;
    public bool AddNoise { get; set; } = false;
    public double NoiseSigma { get; set; } = 5.0;
}

/// <summary>
/// 合成測試影像對話框
/// </summary>
public class SyntheticImageDialog : Form
{
    private NumericUpDown _widthInput = null!;
    private NumericUpDown _heightInput = null!;
    private NumericUpDown _edgePositionInput = null!;
    private NumericUpDown _leftGrayInput = null!;
    private NumericUpDown _rightGrayInput = null!;
    private NumericUpDown _blurSigmaInput = null!;
    private CheckBox _addNoiseCheckBox = null!;
    private NumericUpDown _noiseSigmaInput = null!;

    public SyntheticImageParams Parameters { get; } = new();

    public SyntheticImageDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "合成測試影像設定";
        Size = new Size(350, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 3,
            RowCount = 10
        };

        int row = 0;

        // 寬度
        panel.Controls.Add(new Label { Text = "影像寬度：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _widthInput = new NumericUpDown { Minimum = 50, Maximum = 2000, Value = 200, Width = 80 };
        panel.Controls.Add(_widthInput, 1, row);
        panel.Controls.Add(new Label { Text = "px", AutoSize = true }, 2, row++);

        // 高度
        panel.Controls.Add(new Label { Text = "影像高度：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _heightInput = new NumericUpDown { Minimum = 50, Maximum = 2000, Value = 200, Width = 80 };
        panel.Controls.Add(_heightInput, 1, row);
        panel.Controls.Add(new Label { Text = "px", AutoSize = true }, 2, row++);

        // 邊緣位置
        panel.Controls.Add(new Label { Text = "邊緣位置：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _edgePositionInput = new NumericUpDown { Minimum = 1, Maximum = 2000, DecimalPlaces = 2, Increment = 0.1m, Value = 100, Width = 80 };
        panel.Controls.Add(_edgePositionInput, 1, row);
        panel.Controls.Add(new Label { Text = "px", AutoSize = true }, 2, row++);

        // 左側灰階
        panel.Controls.Add(new Label { Text = "左側灰階：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _leftGrayInput = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 50, Width = 80 };
        panel.Controls.Add(_leftGrayInput, 1, row);
        panel.Controls.Add(new Label { Text = "(0-255)", AutoSize = true }, 2, row++);

        // 右側灰階
        panel.Controls.Add(new Label { Text = "右側灰階：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _rightGrayInput = new NumericUpDown { Minimum = 0, Maximum = 255, Value = 200, Width = 80 };
        panel.Controls.Add(_rightGrayInput, 1, row);
        panel.Controls.Add(new Label { Text = "(0-255)", AutoSize = true }, 2, row++);

        // 模糊程度
        panel.Controls.Add(new Label { Text = "模糊程度：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        _blurSigmaInput = new NumericUpDown { Minimum = 0.1m, Maximum = 20, DecimalPlaces = 1, Increment = 0.1m, Value = 1.5m, Width = 80 };
        panel.Controls.Add(_blurSigmaInput, 1, row);
        panel.Controls.Add(new Label { Text = "σ", AutoSize = true }, 2, row++);

        // 雜訊
        panel.Controls.Add(new Label { Text = "加入雜訊：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        var noisePanel = new FlowLayoutPanel { AutoSize = true };
        _addNoiseCheckBox = new CheckBox { Text = "σ =", AutoSize = true };
        _noiseSigmaInput = new NumericUpDown { Minimum = 0.1m, Maximum = 100, DecimalPlaces = 1, Value = 5, Width = 60, Enabled = false };
        _addNoiseCheckBox.CheckedChanged += (s, e) => _noiseSigmaInput.Enabled = _addNoiseCheckBox.Checked;
        noisePanel.Controls.Add(_addNoiseCheckBox);
        noisePanel.Controls.Add(_noiseSigmaInput);
        panel.Controls.Add(noisePanel, 1, row++);
        panel.SetColumnSpan(noisePanel, 2);

        row++;

        // 按鈕
        var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var cancelBtn = new Button { Text = "取消", Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        var okBtn = new Button { Text = "確定", Size = new Size(80, 30) };
        okBtn.Click += (s, e) =>
        {
            Parameters.Width = (int)_widthInput.Value;
            Parameters.Height = (int)_heightInput.Value;
            Parameters.EdgePosition = (double)_edgePositionInput.Value;
            Parameters.LeftGray = (int)_leftGrayInput.Value;
            Parameters.RightGray = (int)_rightGrayInput.Value;
            Parameters.BlurSigma = (double)_blurSigmaInput.Value;
            Parameters.AddNoise = _addNoiseCheckBox.Checked;
            Parameters.NoiseSigma = (double)_noiseSigmaInput.Value;
            DialogResult = DialogResult.OK;
            Close();
        };

        btnPanel.Controls.Add(cancelBtn);
        btnPanel.Controls.Add(okBtn);
        panel.SetColumnSpan(btnPanel, 3);
        panel.Controls.Add(btnPanel, 0, row);

        Controls.Add(panel);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }
}
