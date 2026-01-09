namespace VisionMeasurementToolkit.Dialogs;

/// <summary>
/// 像素解析度設定對話框
/// </summary>
public class ResolutionDialog : Form
{
    private NumericUpDown _mmPerPixelInput = null!;
    private NumericUpDown _pixelPerMmInput = null!;
    private RadioButton _mmPerPixelRadio = null!;
    private RadioButton _pixelPerMmRadio = null!;

    public double Resolution { get; private set; } = 1.0;
    public bool UseMmPerPixel { get; private set; } = true;

    public ResolutionDialog(double currentResolution)
    {
        Resolution = currentResolution;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "像素解析度設定";
        Size = new Size(350, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 4,
            ColumnCount = 3
        };

        // mm/pixel
        _mmPerPixelRadio = new RadioButton { Text = "解析度：", Checked = true, AutoSize = true };
        _mmPerPixelInput = new NumericUpDown
        {
            Minimum = 0.0001m,
            Maximum = 100,
            DecimalPlaces = 4,
            Increment = 0.001m,
            Value = (decimal)Resolution,
            Width = 100
        };
        var mmLabel = new Label { Text = "mm/pixel", AutoSize = true, Padding = new Padding(0, 5, 0, 0) };

        panel.Controls.Add(_mmPerPixelRadio, 0, 0);
        panel.Controls.Add(_mmPerPixelInput, 1, 0);
        panel.Controls.Add(mmLabel, 2, 0);

        // pixel/mm
        _pixelPerMmRadio = new RadioButton { Text = "或輸入：", AutoSize = true };
        _pixelPerMmInput = new NumericUpDown
        {
            Minimum = 0.01m,
            Maximum = 10000,
            DecimalPlaces = 2,
            Increment = 1,
            Value = (decimal)(1.0 / Resolution),
            Width = 100,
            Enabled = false
        };
        var pxLabel = new Label { Text = "pixel/mm", AutoSize = true, Padding = new Padding(0, 5, 0, 0) };

        panel.Controls.Add(_pixelPerMmRadio, 0, 1);
        panel.Controls.Add(_pixelPerMmInput, 1, 1);
        panel.Controls.Add(pxLabel, 2, 1);

        // 互斥切換
        _mmPerPixelRadio.CheckedChanged += (s, e) =>
        {
            _mmPerPixelInput.Enabled = _mmPerPixelRadio.Checked;
            _pixelPerMmInput.Enabled = !_mmPerPixelRadio.Checked;
        };

        // 按鈕
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };

        var cancelBtn = new Button { Text = "取消", Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        var okBtn = new Button { Text = "確定", Size = new Size(80, 30) };
        okBtn.Click += (s, e) =>
        {
            UseMmPerPixel = _mmPerPixelRadio.Checked;
            Resolution = UseMmPerPixel
                ? (double)_mmPerPixelInput.Value
                : 1.0 / (double)_pixelPerMmInput.Value;
            DialogResult = DialogResult.OK;
            Close();
        };

        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Controls.Add(okBtn);

        panel.SetColumnSpan(buttonPanel, 3);
        panel.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(panel);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }
}

/// <summary>
/// 標準圓校正對話框
/// </summary>
public class CircleCalibrationDialog : Form
{
    private Label _measuredLabel = null!;
    private NumericUpDown _actualInput = null!;
    private Label _resultLabel = null!;

    public double MeasuredDiameterPx { get; }
    public double ActualDiameterMm { get; private set; }
    public double CalculatedResolution { get; private set; }

    public CircleCalibrationDialog(double measuredDiameterPx)
    {
        MeasuredDiameterPx = measuredDiameterPx;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "標準圓校正";
        Size = new Size(350, 220);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 5,
            ColumnCount = 2
        };

        // 檢測到的直徑
        panel.Controls.Add(new Label { Text = "檢測到的圓直徑：", AutoSize = true }, 0, 0);
        _measuredLabel = new Label { Text = $"{MeasuredDiameterPx:F2} pixels", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        panel.Controls.Add(_measuredLabel, 1, 0);

        // 實際直徑輸入
        panel.Controls.Add(new Label { Text = "請輸入實際直徑：", AutoSize = true }, 0, 1);
        var inputPanel = new FlowLayoutPanel { AutoSize = true };
        _actualInput = new NumericUpDown
        {
            Minimum = 0.001m,
            Maximum = 10000,
            DecimalPlaces = 3,
            Increment = 0.1m,
            Value = 25,
            Width = 100
        };
        _actualInput.ValueChanged += UpdateResult;
        inputPanel.Controls.Add(_actualInput);
        inputPanel.Controls.Add(new Label { Text = "mm", AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        panel.Controls.Add(inputPanel, 1, 1);

        // 計算結果
        panel.Controls.Add(new Label { Text = "計算解析度：", AutoSize = true }, 0, 2);
        _resultLabel = new Label { Text = "", AutoSize = true, ForeColor = Color.Blue };
        panel.Controls.Add(_resultLabel, 1, 2);

        UpdateResult(null, EventArgs.Empty);

        // 按鈕
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill
        };

        var cancelBtn = new Button { Text = "取消", Size = new Size(80, 30), DialogResult = DialogResult.Cancel };
        var okBtn = new Button { Text = "套用", Size = new Size(80, 30) };
        okBtn.Click += (s, e) =>
        {
            ActualDiameterMm = (double)_actualInput.Value;
            CalculatedResolution = ActualDiameterMm / MeasuredDiameterPx;
            DialogResult = DialogResult.OK;
            Close();
        };

        buttonPanel.Controls.Add(cancelBtn);
        buttonPanel.Controls.Add(okBtn);

        panel.SetColumnSpan(buttonPanel, 2);
        panel.Controls.Add(buttonPanel, 0, 4);

        Controls.Add(panel);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
    }

    private void UpdateResult(object? sender, EventArgs e)
    {
        double actual = (double)_actualInput.Value;
        double resolution = actual / MeasuredDiameterPx;
        _resultLabel.Text = $"{resolution:F4} mm/pixel";
    }
}
