using SubpixelEdgeComparator.Core;

namespace SubpixelEdgeComparator.Dialogs;

/// <summary>
/// 合成測試影像設定對話框
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
    private Button _okButton = null!;
    private Button _cancelButton = null!;

    /// <summary>
    /// 取得設定的參數
    /// </summary>
    public SyntheticImageParams Parameters { get; private set; } = new();

    public SyntheticImageDialog()
    {
        InitializeComponent();
        LoadDefaultValues();
    }

    private void InitializeComponent()
    {
        Text = "合成測試影像設定";
        Size = new Size(350, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 10
        };
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        int row = 0;

        // 影像寬度
        mainPanel.Controls.Add(CreateLabel("影像寬度："), 0, row);
        _widthInput = CreateNumericUpDown(1, 2000, 0);
        var widthPanel = CreateInputPanel(_widthInput, "px");
        mainPanel.Controls.Add(widthPanel, 1, row++);

        // 影像高度
        mainPanel.Controls.Add(CreateLabel("影像高度："), 0, row);
        _heightInput = CreateNumericUpDown(1, 2000, 0);
        var heightPanel = CreateInputPanel(_heightInput, "px");
        mainPanel.Controls.Add(heightPanel, 1, row++);

        // 邊緣位置
        mainPanel.Controls.Add(CreateLabel("邊緣位置："), 0, row);
        _edgePositionInput = CreateNumericUpDown(0, 2000, 2);
        var edgePanel = CreateInputPanel(_edgePositionInput, "px");
        mainPanel.Controls.Add(edgePanel, 1, row++);

        // 左側灰階
        mainPanel.Controls.Add(CreateLabel("左側灰階："), 0, row);
        _leftGrayInput = CreateNumericUpDown(0, 255, 0);
        var leftGrayPanel = CreateInputPanel(_leftGrayInput, "(0-255)");
        mainPanel.Controls.Add(leftGrayPanel, 1, row++);

        // 右側灰階
        mainPanel.Controls.Add(CreateLabel("右側灰階："), 0, row);
        _rightGrayInput = CreateNumericUpDown(0, 255, 0);
        var rightGrayPanel = CreateInputPanel(_rightGrayInput, "(0-255)");
        mainPanel.Controls.Add(rightGrayPanel, 1, row++);

        // 模糊程度
        mainPanel.Controls.Add(CreateLabel("模糊程度："), 0, row);
        _blurSigmaInput = CreateNumericUpDown(0.1m, 20, 1);
        var blurPanel = CreateInputPanel(_blurSigmaInput, "σ");
        mainPanel.Controls.Add(blurPanel, 1, row++);

        // 加入雜訊
        mainPanel.Controls.Add(CreateLabel("加入雜訊："), 0, row);
        var noisePanel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        _addNoiseCheckBox = new CheckBox { Text = "σ =", AutoSize = true };
        _addNoiseCheckBox.CheckedChanged += (s, e) => _noiseSigmaInput.Enabled = _addNoiseCheckBox.Checked;
        _noiseSigmaInput = CreateNumericUpDown(0.1m, 100, 1);
        _noiseSigmaInput.Width = 60;
        _noiseSigmaInput.Enabled = false;
        noisePanel.Controls.Add(_addNoiseCheckBox);
        noisePanel.Controls.Add(_noiseSigmaInput);
        mainPanel.Controls.Add(noisePanel, 1, row++);

        // 空行
        row++;

        // 按鈕
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        _cancelButton = new Button { Text = "取消", Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        _okButton = new Button { Text = "確定", Size = new Size(80, 30) };
        _okButton.Click += OkButton_Click;

        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_okButton);

        mainPanel.SetColumnSpan(buttonPanel, 2);
        mainPanel.Controls.Add(buttonPanel, 0, row);

        Controls.Add(mainPanel);
        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Padding = new Padding(0, 5, 0, 0)
        };
    }

    private static NumericUpDown CreateNumericUpDown(decimal min, decimal max, int decimalPlaces)
    {
        return new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            DecimalPlaces = decimalPlaces,
            Width = 80,
            Increment = decimalPlaces > 0 ? 0.1m : 1
        };
    }

    private static FlowLayoutPanel CreateInputPanel(NumericUpDown input, string suffix)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        panel.Controls.Add(input);
        panel.Controls.Add(new Label { Text = suffix, AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        return panel;
    }

    private void LoadDefaultValues()
    {
        var defaults = new SyntheticImageParams();
        _widthInput.Value = defaults.Width;
        _heightInput.Value = defaults.Height;
        _edgePositionInput.Value = (decimal)defaults.EdgePosition;
        _leftGrayInput.Value = defaults.LeftGray;
        _rightGrayInput.Value = defaults.RightGray;
        _blurSigmaInput.Value = (decimal)defaults.BlurSigma;
        _addNoiseCheckBox.Checked = defaults.AddNoise;
        _noiseSigmaInput.Value = (decimal)defaults.NoiseSigma;
    }

    private void OkButton_Click(object? sender, EventArgs e)
    {
        Parameters = new SyntheticImageParams
        {
            Width = (int)_widthInput.Value,
            Height = (int)_heightInput.Value,
            EdgePosition = (double)_edgePositionInput.Value,
            LeftGray = (int)_leftGrayInput.Value,
            RightGray = (int)_rightGrayInput.Value,
            BlurSigma = (double)_blurSigmaInput.Value,
            AddNoise = _addNoiseCheckBox.Checked,
            NoiseSigma = (double)_noiseSigmaInput.Value
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
