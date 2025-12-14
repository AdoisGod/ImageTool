using OpenCvSharp;
using VisionVerificationToolkit.Controls;
using VisionVerificationToolkit.Core;
using VisionVerificationToolkit.Detection;
using VisionVerificationToolkit.Objects;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace VisionVerificationToolkit.Pages;

/// <summary>
/// 頁面2：檢測/量測
/// </summary>
public class DetectionPage : UserControl
{
    private readonly ImageManager _imageManager;
    private readonly CalibrationManager _calibration;
    private readonly ResultManager _resultManager = new();

    private ImageCanvas _canvas = null!;
    private TreeView _objectTree = null!;
    private PropertyGrid _propertyGrid = null!;
    private ComboBox _toolCombo = null!;
    private Panel _toolOptions = null!;
    private Label _statusLabel = null!;

    private IDetector? _currentDetector;
    private bool _useOriginalImage = false;

    public DetectionPage(ImageManager imageManager, CalibrationManager calibration)
    {
        _imageManager = imageManager;
        _calibration = calibration;
        InitializeComponent();
        SetupEvents();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;

        var mainSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 300,
            FixedPanel = FixedPanel.Panel2
        };

        // 左側
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        // 工具列
        var toolbar = new ToolStrip();
        toolbar.Items.Add(new ToolStripButton("使用原圖", null, (s, e) => { _useOriginalImage = true; UpdateImage(); }) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("使用處理後", null, (s, e) => { _useOriginalImage = false; UpdateImage(); }) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("校正設定", null, (s, e) => SetCalibration()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("清除所有", null, (s, e) => ClearAll()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("匯出CSV", null, (s, e) => ExportCsv()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        leftPanel.Controls.Add(toolbar);

        // 工具選擇
        var toolPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
        toolPanel.Controls.Add(new Label { Text = "檢測工具:", Location = new System.Drawing.Point(5, 10), AutoSize = true });
        _toolCombo = new ComboBox
        {
            Location = new System.Drawing.Point(70, 7),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _toolCombo.Items.AddRange(new[] { "選擇工具...", "找圓 (霍夫)", "找圓 (邊緣擬合)", "找線 (卡尺)", "找線 (霍夫)", "找點 (角點)", "找輪廓" });
        _toolCombo.SelectedIndex = 0;
        _toolCombo.SelectedIndexChanged += ToolCombo_SelectedIndexChanged;
        toolPanel.Controls.Add(_toolCombo);

        var detectBtn = new Button { Text = "檢測", Location = new System.Drawing.Point(230, 6), Width = 60 };
        detectBtn.Click += (s, e) => ExecuteDetection();
        toolPanel.Controls.Add(detectBtn);

        leftPanel.Controls.Add(toolPanel);

        // 畫布
        _canvas = new ImageCanvas { Dock = DockStyle.Fill, AllowDrop = true };
        _canvas.DragDrop += Canvas_DragDrop;
        _canvas.DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        _canvas.RoiSelected += Canvas_RoiSelected;
        _canvas.ObjectSelected += (s, obj) => SelectObject(obj);
        leftPanel.Controls.Add(_canvas);

        // 狀態
        _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 25 };
        leftPanel.Controls.Add(_statusLabel);

        mainSplit.Panel1.Controls.Add(leftPanel);

        // 右側
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

        // 物件樹
        var treeGroup = new GroupBox { Text = "檢測物件", Dock = DockStyle.Top, Height = 250 };
        _objectTree = new TreeView { Dock = DockStyle.Fill, ShowRootLines = true };
        _objectTree.AfterSelect += ObjectTree_AfterSelect;
        _objectTree.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };
        treeGroup.Controls.Add(_objectTree);
        rightPanel.Controls.Add(treeGroup);

        // 屬性
        var propGroup = new GroupBox { Text = "物件屬性", Dock = DockStyle.Fill };
        _propertyGrid = new PropertyGrid { Dock = DockStyle.Fill };
        propGroup.Controls.Add(_propertyGrid);
        rightPanel.Controls.Add(propGroup);

        // 工具選項面板
        _toolOptions = new Panel { Dock = DockStyle.Bottom, Height = 120, BorderStyle = BorderStyle.FixedSingle };
        rightPanel.Controls.Add(_toolOptions);

        mainSplit.Panel2.Controls.Add(rightPanel);
        Controls.Add(mainSplit);
    }

    private void SetupEvents()
    {
        _imageManager.OriginalImageChanged += (s, e) => UpdateImage();
        _imageManager.ProcessedImageChanged += (s, e) => UpdateImage();

        _resultManager.ObjectAdded += (s, obj) => RefreshObjectTree();
        _resultManager.ObjectRemoved += (s, obj) => RefreshObjectTree();
        _resultManager.ObjectsChanged += (s, e) => RefreshObjectTree();
    }

    public void UpdateImage()
    {
        var mat = _useOriginalImage ? _imageManager.OriginalImage : _imageManager.ProcessedImage;
        if (mat != null)
        {
            _canvas.SetImage(mat);
            _statusLabel.Text = _useOriginalImage ? "使用原圖" : "使用處理後影像";
        }
    }

    private void ToolCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _toolOptions.Controls.Clear();

        switch (_toolCombo.SelectedIndex)
        {
            case 1: // 找圓 (霍夫)
                _currentDetector = new CircleDetector { Method = CircleDetectionMethod.Hough };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                AddToolOption("dp", 1.0, 0.5, 3.0);
                AddToolOption("minDist", 50, 10, 500);
                AddToolOption("param1", 100, 10, 300);
                AddToolOption("param2", 30, 10, 100);
                break;
            case 2: // 找圓 (邊緣擬合)
                _currentDetector = new CircleDetector { Method = CircleDetectionMethod.EdgeFitting };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                AddToolOption("Canny低", 50, 10, 200);
                AddToolOption("Canny高", 150, 50, 300);
                break;
            case 3: // 找線 (卡尺)
                _currentDetector = new LineDetector { Method = LineDetectionMethod.Caliper };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Line;
                AddToolOption("採樣數", 20, 5, 50);
                AddToolOption("剖面長", 30, 10, 100);
                AddToolOption("閾值", 30, 5, 100);
                break;
            case 4: // 找線 (霍夫)
                _currentDetector = new LineDetector { Method = LineDetectionMethod.Hough };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                break;
            case 5: // 找點
                _currentDetector = new PointDetector();
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                AddToolOption("最大數量", 100, 10, 500);
                AddToolOption("最小距離", 10, 1, 50);
                break;
            case 6: // 找輪廓
                _currentDetector = new ContourDetector();
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                AddToolOption("最小面積", 100, 10, 10000);
                AddToolOption("最大面積", 100000, 100, 1000000);
                break;
            default:
                _currentDetector = null;
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.None;
                break;
        }
    }

    private int _optionY = 5;

    private void AddToolOption(string label, double defaultValue, double min, double max)
    {
        var lbl = new Label { Text = $"{label}:", Location = new System.Drawing.Point(5, _optionY + 3), AutoSize = true };
        var num = new NumericUpDown
        {
            Location = new System.Drawing.Point(80, _optionY),
            Width = 80,
            Minimum = (decimal)min,
            Maximum = (decimal)max,
            Value = (decimal)defaultValue,
            DecimalPlaces = 1
        };
        _toolOptions.Controls.Add(lbl);
        _toolOptions.Controls.Add(num);
        _optionY += 28;
    }

    private void Canvas_RoiSelected(object? sender, DrawingRectangleF roi)
    {
        if (_currentDetector == null) return;
        ExecuteDetection(roi);
    }

    private void ExecuteDetection(DrawingRectangleF? roi = null)
    {
        if (_currentDetector == null)
        {
            MessageBox.Show("請先選擇檢測工具", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var mat = _useOriginalImage ? _imageManager.OriginalImage : _imageManager.ProcessedImage;
        if (mat == null)
        {
            MessageBox.Show("請先載入影像", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var startTime = DateTime.Now;
            var results = _currentDetector.Detect(mat, roi);
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

            foreach (var obj in results)
            {
                _resultManager.Add(obj);
                _canvas.AddObject(obj);
            }

            _statusLabel.Text = $"檢測到 {results.Count} 個物件 ({elapsed:F1} ms)";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"檢測失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RefreshObjectTree()
    {
        _objectTree.Nodes.Clear();

        var circleNode = _objectTree.Nodes.Add("circles", "圓形");
        var lineNode = _objectTree.Nodes.Add("lines", "直線");
        var pointNode = _objectTree.Nodes.Add("points", "點");
        var contourNode = _objectTree.Nodes.Add("contours", "輪廓");
        var measureNode = _objectTree.Nodes.Add("measures", "量測");

        foreach (var obj in _resultManager.Objects)
        {
            var node = obj switch
            {
                CircleObject => circleNode.Nodes.Add(obj.Id, obj.Name),
                LineObject => lineNode.Nodes.Add(obj.Id, obj.Name),
                PointObject => pointNode.Nodes.Add(obj.Id, obj.Name),
                ContourObject => contourNode.Nodes.Add(obj.Id, obj.Name),
                MeasurementResult => measureNode.Nodes.Add(obj.Id, obj.Name),
                _ => null
            };
            if (node != null) node.Tag = obj;
        }

        _objectTree.ExpandAll();
    }

    private void ObjectTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is IGeometryObject obj)
        {
            SelectObject(obj);
        }
    }

    private void SelectObject(IGeometryObject obj)
    {
        _resultManager.Select(obj);
        _canvas.SelectObject(obj);

        // 顯示屬性
        var details = obj.GetDetails();
        _propertyGrid.SelectedObject = new DictionaryPropertyGridAdapter(details);
    }

    private void DeleteSelected()
    {
        if (_resultManager.SelectedObject != null)
        {
            var obj = _resultManager.SelectedObject;
            _resultManager.Remove(obj);
            _canvas.RemoveObject(obj);
        }
    }

    private void ClearAll()
    {
        _resultManager.Clear();
        _canvas.ClearObjects();
    }

    private void SetCalibration()
    {
        using var dialog = new Form
        {
            Text = "校正設定",
            Size = new System.Drawing.Size(300, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent
        };

        var label = new Label { Text = "解析度 (mm/pixel):", Location = new System.Drawing.Point(20, 20), AutoSize = true };
        var numBox = new NumericUpDown
        {
            Location = new System.Drawing.Point(130, 18),
            Width = 100,
            DecimalPlaces = 6,
            Minimum = 0.000001m,
            Maximum = 100,
            Value = (decimal)_calibration.Resolution
        };
        var okBtn = new Button { Text = "確定", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(100, 70) };

        dialog.Controls.AddRange(new Control[] { label, numBox, okBtn });
        dialog.AcceptButton = okBtn;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _calibration.SetResolution((double)numBox.Value);
            _statusLabel.Text = $"校正設定: {_calibration.GetResolutionString()}";
        }
    }

    private void ExportCsv()
    {
        if (_resultManager.Objects.Count == 0)
        {
            MessageBox.Show("無物件可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "detection_results.csv" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
            writer.WriteLine("ID,Name,Type,Summary");
            foreach (var obj in _resultManager.Objects)
            {
                writer.WriteLine($"{obj.Id},{obj.Name},{obj.ObjectType},\"{obj.GetSummary()}\"");
            }
            MessageBox.Show("匯出成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void Canvas_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            try
            {
                _imageManager.LoadImage(files[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // PropertyGrid 用的轉接器
    private class DictionaryPropertyGridAdapter : System.ComponentModel.ICustomTypeDescriptor
    {
        private readonly Dictionary<string, object> _dict;

        public DictionaryPropertyGridAdapter(Dictionary<string, object> dict) { _dict = dict; }

        public System.ComponentModel.AttributeCollection GetAttributes() => System.ComponentModel.TypeDescriptor.GetAttributes(this, true);
        public string? GetClassName() => System.ComponentModel.TypeDescriptor.GetClassName(this, true);
        public string? GetComponentName() => System.ComponentModel.TypeDescriptor.GetComponentName(this, true);
        public System.ComponentModel.TypeConverter GetConverter() => System.ComponentModel.TypeDescriptor.GetConverter(this, true);
        public System.ComponentModel.EventDescriptor? GetDefaultEvent() => System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true);
        public System.ComponentModel.PropertyDescriptor? GetDefaultProperty() => null;
        public object? GetEditor(Type editorBaseType) => System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true);
        public System.ComponentModel.EventDescriptorCollection GetEvents() => System.ComponentModel.TypeDescriptor.GetEvents(this, true);
        public System.ComponentModel.EventDescriptorCollection GetEvents(Attribute[]? attributes) => System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true);
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() => GetProperties(null);
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor? pd) => this;

        public System.ComponentModel.PropertyDescriptorCollection GetProperties(Attribute[]? attributes)
        {
            var props = new List<System.ComponentModel.PropertyDescriptor>();
            foreach (var kvp in _dict)
            {
                props.Add(new DictionaryPropertyDescriptor(kvp.Key, kvp.Value));
            }
            return new System.ComponentModel.PropertyDescriptorCollection(props.ToArray());
        }

        private class DictionaryPropertyDescriptor : System.ComponentModel.PropertyDescriptor
        {
            private readonly object _value;
            public DictionaryPropertyDescriptor(string name, object value) : base(name, null) { _value = value; }
            public override Type ComponentType => typeof(DictionaryPropertyGridAdapter);
            public override bool IsReadOnly => true;
            public override Type PropertyType => _value?.GetType() ?? typeof(object);
            public override bool CanResetValue(object component) => false;
            public override object? GetValue(object? component) => _value;
            public override void ResetValue(object component) { }
            public override void SetValue(object? component, object? value) { }
            public override bool ShouldSerializeValue(object component) => false;
        }
    }
}
