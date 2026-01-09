using OpenCvSharp;
using VisionVerificationToolkit.Controls;
using VisionVerificationToolkit.Construction;
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
    private bool _isSelectingObject = false; // 防止選取事件無限遞迴
    private readonly List<IGeometryObject> _multiSelectedObjects = new(); // 多選物件
    private string? _currentMeasureMode = null; // 當前量測模式: "距離" 或 "角度"
    private string? _currentConstructMode = null; // 當前建構模式

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
            SplitterDistance = 800,
            FixedPanel = FixedPanel.None
        };

        // ===== 左側 =====
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        // 工具列
        var toolbar = new ToolStrip { Dock = DockStyle.Top };
        toolbar.Items.Add(new ToolStripButton("使用原圖", null, (s, e) => { _useOriginalImage = true; UpdateImage(); }) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("使用處理後", null, (s, e) => { _useOriginalImage = false; UpdateImage(); }) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("校正設定", null, (s, e) => SetCalibration()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripButton("清除所有", null, (s, e) => ClearAll()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(new ToolStripLabel("匯出:"));
        toolbar.Items.Add(new ToolStripButton("CSV", null, (s, e) => ExportCsv()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("JSON", null, (s, e) => ExportJson()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("PNG", null, (s, e) => ExportAnnotatedImage()) { DisplayStyle = ToolStripItemDisplayStyle.Text });
        toolbar.Items.Add(new ToolStripButton("HTML", null, (s, e) => ExportHtmlReport()) { DisplayStyle = ToolStripItemDisplayStyle.Text });

        // 工具選擇面板
        var toolSelectPanel = new Panel { Dock = DockStyle.Top, Height = 40 };
        var toolLabel = new Label { Text = "檢測工具:", Location = new System.Drawing.Point(10, 10), AutoSize = true };
        _toolCombo = new ComboBox
        {
            Location = new System.Drawing.Point(80, 7),
            Width = 140,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _toolCombo.Items.AddRange(new[] {
            "選擇工具...",
            "找圓 (霍夫)", "找圓 (邊緣擬合)",
            "找線 (卡尺)", "找線 (霍夫)",
            "找點 (角點)", "找輪廓", "能量輪廓",
            "───建構───",
            "兩線交點", "平行線", "垂直線",
            "圓心連線", "圓心提取", "兩點中點",
            "───量測───",
            "量測距離", "量測角度",
            "量測同心度", "量測平行度", "量測垂直度",
            "量測圓度", "量測直線度"
        });
        _toolCombo.SelectedIndex = 0;
        _toolCombo.SelectedIndexChanged += ToolCombo_SelectedIndexChanged;

        var detectBtn = new Button { Text = "執行", Location = new System.Drawing.Point(230, 6), Width = 60, Height = 26 };
        detectBtn.Click += (s, e) => ExecuteCurrentTool();

        var tipLabel = new Label { Text = "提示: 框選ROI或Ctrl+點擊多選物件", Location = new System.Drawing.Point(295, 10), AutoSize = true, ForeColor = Color.Gray };

        toolSelectPanel.Controls.AddRange(new Control[] { toolLabel, _toolCombo, detectBtn, tipLabel });

        // 狀態
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            BorderStyle = BorderStyle.FixedSingle
        };

        // 畫布
        _canvas = new ImageCanvas { Dock = DockStyle.Fill, AllowDrop = true };
        _canvas.DragDrop += Canvas_DragDrop;
        _canvas.DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        _canvas.RoiSelected += Canvas_RoiSelected;
        _canvas.LineDrawn += Canvas_LineDrawn;
        _canvas.ObjectSelected += (s, obj) => SelectObject(obj);

        // 按正確順序加入
        leftPanel.Controls.Add(_canvas);
        leftPanel.Controls.Add(_statusLabel);
        leftPanel.Controls.Add(toolSelectPanel);
        leftPanel.Controls.Add(toolbar);

        mainSplit.Panel1.Controls.Add(leftPanel);

        // ===== 右側 =====
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };

        // 工具選項面板 (底部)
        _toolOptions = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 180,
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        var toolOptionsLabel = new Label
        {
            Text = "工具參數",
            Dock = DockStyle.Top,
            Height = 22,
            Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(240, 240, 240)
        };
        _toolOptions.Controls.Add(toolOptionsLabel);

        // 屬性 (填滿)
        var propGroup = new GroupBox { Text = "物件屬性", Dock = DockStyle.Fill, Padding = new Padding(5) };
        _propertyGrid = new PropertyGrid { Dock = DockStyle.Fill, HelpVisible = false };
        propGroup.Controls.Add(_propertyGrid);

        // 物件樹 (上方)
        var treeGroup = new GroupBox { Text = "檢測物件 (Ctrl+點擊多選)", Dock = DockStyle.Top, Height = 280 };
        _objectTree = new TreeView { Dock = DockStyle.Fill, ShowRootLines = true };
        _objectTree.AfterSelect += ObjectTree_AfterSelect;
        _objectTree.NodeMouseClick += ObjectTree_NodeMouseClick;
        _objectTree.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) DeleteSelected(); };

        var treeButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 30, FlowDirection = FlowDirection.LeftToRight };
        var deleteBtn = new Button { Text = "刪除選取", Width = 70, Height = 24 };
        deleteBtn.Click += (s, e) => DeleteSelected();
        var clearBtn = new Button { Text = "清除全部", Width = 70, Height = 24 };
        clearBtn.Click += (s, e) => ClearAll();
        treeButtons.Controls.AddRange(new Control[] { deleteBtn, clearBtn });

        treeGroup.Controls.Add(_objectTree);
        treeGroup.Controls.Add(treeButtons);

        // 按正確順序加入右側
        rightPanel.Controls.Add(propGroup);
        rightPanel.Controls.Add(_toolOptions);
        rightPanel.Controls.Add(treeGroup);

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
        // 清除舊的控件，保留標題
        while (_toolOptions.Controls.Count > 1)
        {
            _toolOptions.Controls.RemoveAt(1);
        }

        // 重置模式
        _currentDetector = null;
        _currentMeasureMode = null;
        _currentConstructMode = null;
        _canvas.CurrentDrawMode = ImageCanvas.DrawMode.None;

        int y = 25;

        switch (_toolCombo.SelectedIndex)
        {
            case 1: // 找圓 (霍夫)
                _currentDetector = new CircleDetector { Method = CircleDetectionMethod.Hough };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                y = AddToolOption(y, "dp", 1.0, 0.5, 3.0, v => ((CircleDetector)_currentDetector!).Dp = v);
                y = AddToolOption(y, "minDist", 50, 10, 500, v => ((CircleDetector)_currentDetector!).MinDist = v);
                y = AddToolOption(y, "param1", 100, 10, 300, v => ((CircleDetector)_currentDetector!).Param1 = v);
                y = AddToolOption(y, "param2", 30, 10, 100, v => ((CircleDetector)_currentDetector!).Param2 = v);
                break;
            case 2: // 找圓 (邊緣擬合)
                _currentDetector = new CircleDetector { Method = CircleDetectionMethod.EdgeFitting };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                y = AddToolOption(y, "Canny低", 50, 10, 200, v => ((CircleDetector)_currentDetector!).CannyThreshold1 = v);
                y = AddToolOption(y, "Canny高", 150, 50, 300, v => ((CircleDetector)_currentDetector!).CannyThreshold2 = v);
                break;
            case 3: // 找線 (卡尺)
                _currentDetector = new LineDetector { Method = LineDetectionMethod.Caliper };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Line;
                y = AddToolOption(y, "採樣數", 20, 5, 50, v => ((LineDetector)_currentDetector!).CaliperCount = (int)v);
                y = AddToolOption(y, "剖面長", 30, 10, 100, v => ((LineDetector)_currentDetector!).ProfileLength = (int)v);
                y = AddToolOption(y, "閾值", 30, 5, 100, v => ((LineDetector)_currentDetector!).EdgeThreshold = (int)v);
                break;
            case 4: // 找線 (霍夫)
                _currentDetector = new LineDetector { Method = LineDetectionMethod.Hough };
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                y = AddToolOption(y, "閾值", 50, 10, 200, v => ((LineDetector)_currentDetector!).HoughThreshold = v);
                y = AddToolOption(y, "最小長度", 100, 10, 500, v => ((LineDetector)_currentDetector!).MinLineLength = v);
                y = AddToolOption(y, "最大間隙", 10, 1, 50, v => ((LineDetector)_currentDetector!).MaxLineGap = v);
                break;
            case 5: // 找點
                _currentDetector = new PointDetector();
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                y = AddToolOption(y, "最大數量", 100, 10, 500, v => ((PointDetector)_currentDetector!).MaxCorners = (int)v);
                y = AddToolOption(y, "最小距離", 10, 1, 50, v => ((PointDetector)_currentDetector!).MinDistance = v);
                break;
            case 6: // 找輪廓
                _currentDetector = new ContourDetector();
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                y = AddToolOption(y, "最小面積", 100, 10, 50000, v => ((ContourDetector)_currentDetector!).MinArea = v);
                y = AddToolOption(y, "最大面積", 100000, 100, 1000000, v => ((ContourDetector)_currentDetector!).MaxArea = v);
                break;
            case 7: // 能量輪廓
                _currentDetector = new EnergyContourDetector();
                _canvas.CurrentDrawMode = ImageCanvas.DrawMode.Rectangle;
                y = AddToolOption(y, "數據權重", 1.0, 0.1, 10.0, v => ((EnergyContourDetector)_currentDetector!).DataWeight = v);
                y = AddToolOption(y, "平滑權重", 0.5, 0.0, 5.0, v => ((EnergyContourDetector)_currentDetector!).SmoothWeight = v);
                y = AddToolOption(y, "角度懲罰", 2.0, 0.1, 10.0, v => ((EnergyContourDetector)_currentDetector!).AnglePenalty = v);
                y = AddToolOption(y, "搜尋範圍", 10, 1, 20, v => ((EnergyContourDetector)_currentDetector!).SearchRange = (int)v);
                break;
            case 8: // ───建構─── 分隔線
                _toolCombo.SelectedIndex = 0;
                return;
            case 9: // 兩線交點
                _currentConstructMode = "兩線交點";
                AddConstructionUI(y, "選取兩條線", "線+線 → 交點");
                break;
            case 10: // 平行線
                _currentConstructMode = "平行線";
                AddConstructionUI(y, "選取一條線和一個點", "線+點 → 平行線");
                break;
            case 11: // 垂直線
                _currentConstructMode = "垂直線";
                AddConstructionUI(y, "選取一條線和一個點", "線+點 → 垂直線");
                break;
            case 12: // 圓心連線
                _currentConstructMode = "圓心連線";
                AddConstructionUI(y, "選取兩個圓", "圓+圓 → 連線");
                break;
            case 13: // 圓心提取
                _currentConstructMode = "圓心提取";
                AddConstructionUI(y, "選取一個圓", "圓 → 圓心點");
                break;
            case 14: // 兩點中點
                _currentConstructMode = "兩點中點";
                AddConstructionUI(y, "選取兩個點", "點+點 → 中點");
                break;
            case 15: // ───量測─── 分隔線
                _toolCombo.SelectedIndex = 0;
                return;
            case 16: // 量測距離
                _currentMeasureMode = "距離";
                AddMeasurementUI(y);
                break;
            case 17: // 量測角度
                _currentMeasureMode = "角度";
                AddMeasurementUI(y);
                break;
            case 18: // 量測同心度
                _currentMeasureMode = "同心度";
                AddGeometryMeasurementUI(y, "選取兩個圓", "圓+圓 → 圓心偏移量");
                break;
            case 19: // 量測平行度
                _currentMeasureMode = "平行度";
                AddGeometryMeasurementUI(y, "選取兩條線", "線+線 → 角度偏差");
                break;
            case 20: // 量測垂直度
                _currentMeasureMode = "垂直度";
                AddGeometryMeasurementUI(y, "選取兩條線", "線+線 → 與90°的偏差");
                break;
            case 21: // 量測圓度
                _currentMeasureMode = "圓度";
                AddGeometryMeasurementUI(y, "選取一個圓", "圓 → 擬合R²");
                break;
            case 22: // 量測直線度
                _currentMeasureMode = "直線度";
                AddGeometryMeasurementUI(y, "選取一條線", "線 → 擬合R²");
                break;
        }
    }

    private void ExecuteCurrentTool()
    {
        if (_currentMeasureMode == "距離")
        {
            ExecuteDistanceMeasurement();
        }
        else if (_currentMeasureMode == "角度")
        {
            ExecuteAngleMeasurement();
        }
        else if (_currentMeasureMode == "同心度")
        {
            ExecuteConcentricityMeasurement();
        }
        else if (_currentMeasureMode == "平行度")
        {
            ExecuteParallelismMeasurement();
        }
        else if (_currentMeasureMode == "垂直度")
        {
            ExecutePerpendicularityMeasurement();
        }
        else if (_currentMeasureMode == "圓度")
        {
            ExecuteCircularityMeasurement();
        }
        else if (_currentMeasureMode == "直線度")
        {
            ExecuteLinearityMeasurement();
        }
        else if (_currentConstructMode != null)
        {
            ExecuteConstruction();
        }
        else if (_currentDetector != null)
        {
            ExecuteDetection();
        }
        else
        {
            MessageBox.Show("請先選擇工具", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void AddConstructionUI(int y, string instruction, string formula)
    {
        var infoLabel = new Label
        {
            Text = $"1. Ctrl+點擊{instruction}\n2. 點擊上方「執行」按鈕",
            Location = new System.Drawing.Point(5, y),
            Size = new System.Drawing.Size(170, 50),
            ForeColor = Color.DarkBlue
        };
        _toolOptions.Controls.Add(infoLabel);

        var formulaLabel = new Label
        {
            Text = $"建構: {formula}",
            Location = new System.Drawing.Point(5, y + 55),
            Size = new System.Drawing.Size(170, 25),
            ForeColor = Color.Gray
        };
        _toolOptions.Controls.Add(formulaLabel);
    }

    private void ExecuteConstruction()
    {
        var selected = GetSelectedObjectsFromTree();

        IGeometryObject? result = null;
        List<IGeometryObject>? results = null;

        switch (_currentConstructMode)
        {
            case "兩線交點":
                var lines = selected.OfType<LineObject>().ToList();
                if (lines.Count < 2)
                {
                    MessageBox.Show("請選取兩條線", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                result = ObjectConstructor.LineIntersection(lines[0], lines[1]);
                if (result == null)
                {
                    MessageBox.Show("兩線平行，無交點", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                break;

            case "平行線":
                var lineForParallel = selected.OfType<LineObject>().FirstOrDefault();
                var pointForParallel = selected.OfType<PointObject>().FirstOrDefault();
                if (lineForParallel == null || pointForParallel == null)
                {
                    MessageBox.Show("請選取一條線和一個點", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                result = ObjectConstructor.ParallelLine(lineForParallel, pointForParallel);
                break;

            case "垂直線":
                var lineForPerp = selected.OfType<LineObject>().FirstOrDefault();
                var pointForPerp = selected.OfType<PointObject>().FirstOrDefault();
                if (lineForPerp == null || pointForPerp == null)
                {
                    MessageBox.Show("請選取一條線和一個點", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                result = ObjectConstructor.PerpendicularLine(lineForPerp, pointForPerp);
                break;

            case "圓心連線":
                var circles = selected.OfType<CircleObject>().ToList();
                if (circles.Count < 2)
                {
                    MessageBox.Show("請選取兩個圓", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                result = ObjectConstructor.ConnectCircleCenters(circles[0], circles[1]);
                break;

            case "圓心提取":
                var circle = selected.OfType<CircleObject>().FirstOrDefault();
                if (circle == null)
                {
                    MessageBox.Show("請選取一個圓", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                result = ObjectConstructor.ExtractCircleCenter(circle);
                break;

            case "兩點中點":
                var points = selected.OfType<PointObject>().ToList();
                if (points.Count < 2)
                {
                    MessageBox.Show("請選取兩個點", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                result = ObjectConstructor.Midpoint(points[0], points[1]);
                break;

            default:
                MessageBox.Show("未知的建構類型", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
        }

        // 添加結果
        if (result != null)
        {
            _resultManager.Add(result);
            _canvas.AddObject(result);
            _statusLabel.Text = $"建構完成: {result.GetSummary()}";
        }

        if (results != null)
        {
            foreach (var obj in results)
            {
                _resultManager.Add(obj);
                _canvas.AddObject(obj);
            }
            _statusLabel.Text = $"建構完成: {results.Count} 個物件";
        }
    }

    private void AddMeasurementUI(int y)
    {
        var infoLabel = new Label
        {
            Text = _currentMeasureMode == "距離"
                ? "1. Ctrl+點擊選取兩個物件\n2. 點擊上方「執行」按鈕"
                : "1. Ctrl+點擊選取兩條線\n2. 點擊上方「執行」按鈕",
            Location = new System.Drawing.Point(5, y),
            Size = new System.Drawing.Size(170, 50),
            ForeColor = Color.DarkBlue
        };
        _toolOptions.Controls.Add(infoLabel);

        var supportLabel = new Label
        {
            Text = _currentMeasureMode == "距離"
                ? "支援組合:\n• 點+點  • 圓+圓\n• 線+線  • 點+線\n• 點+圓"
                : "支援:\n• 線+線 夾角",
            Location = new System.Drawing.Point(5, y + 55),
            Size = new System.Drawing.Size(170, 70),
            ForeColor = Color.Gray
        };
        _toolOptions.Controls.Add(supportLabel);
    }

    private void ExecuteDistanceMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        if (selected.Count != 2)
        {
            MessageBox.Show("請在物件列表中選取兩個物件 (使用 Ctrl+點擊多選)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var obj1 = selected[0];
        var obj2 = selected[1];
        MeasurementResult? result = null;

        // 根據物件類型計算距離
        if (obj1 is PointObject p1 && obj2 is PointObject p2)
        {
            result = new DistanceMeasurementResult(p1.Position, p2.Position, "點對點距離");
        }
        else if (obj1 is CircleObject c1 && obj2 is CircleObject c2)
        {
            // 圓心距離
            double dist = CircleObject.CenterDistance(c1, c2);
            result = new MeasurementResult(MeasurementType.Distance, dist, "圓心距離")
            {
                AnnotationPoints = { c1.Center, c2.Center },
                SourceObjects = { c1, c2 }
            };
        }
        else if (obj1 is LineObject l1 && obj2 is LineObject l2)
        {
            // 線對線：計算平行線距離 (取中點)
            var mid1 = new DrawingPointF((l1.StartPoint.X + l1.EndPoint.X) / 2, (l1.StartPoint.Y + l1.EndPoint.Y) / 2);
            double dist = l2.DistanceToPoint(mid1);
            result = new MeasurementResult(MeasurementType.Distance, dist, "線對線距離")
            {
                AnnotationPoints = { mid1, l2.ProjectPoint(mid1) },
                SourceObjects = { l1, l2 }
            };
        }
        else if ((obj1 is PointObject pt1 && obj2 is LineObject ln1) || (obj1 is LineObject ln2 && obj2 is PointObject pt2))
        {
            var point = obj1 is PointObject ? ((PointObject)obj1).Position : ((PointObject)obj2).Position;
            var line = obj1 is LineObject ? (LineObject)obj1 : (LineObject)obj2;
            double dist = line.DistanceToPoint(point);
            result = new MeasurementResult(MeasurementType.Distance, dist, "點到線距離")
            {
                AnnotationPoints = { point, line.ProjectPoint(point) },
                SourceObjects = { obj1, obj2 }
            };
        }
        else if ((obj1 is PointObject ptc1 && obj2 is CircleObject cir1) || (obj1 is CircleObject cir2 && obj2 is PointObject ptc2))
        {
            var point = obj1 is PointObject ? ((PointObject)obj1).Position : ((PointObject)obj2).Position;
            var circle = obj1 is CircleObject ? (CircleObject)obj1 : (CircleObject)obj2;
            double distToEdge = circle.DistanceToEdge(point);
            result = new MeasurementResult(MeasurementType.Distance, distToEdge, "點到圓邊距離")
            {
                AnnotationPoints = { point, circle.Center },
                SourceObjects = { obj1, obj2 }
            };
        }
        else
        {
            MessageBox.Show("不支援的物件組合，請選取：\n• 點+點\n• 圓+圓\n• 線+線\n• 點+線\n• 點+圓", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (result != null)
        {
            // 套用校正
            result.ValueMm = _calibration.PixelsToMm(result.Value);
            if (_calibration.IsCalibrated)
                result.Unit = "mm";

            _resultManager.Add(result);
            _canvas.AddObject(result);
            _statusLabel.Text = $"量測結果: {result.GetSummary()}";
        }
    }

    private void ExecuteAngleMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        var lines = selected.OfType<LineObject>().ToList();

        if (lines.Count != 2)
        {
            MessageBox.Show("請在物件列表中選取兩條線 (使用 Ctrl+點擊多選)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var l1 = lines[0];
        var l2 = lines[1];

        // 計算夾角
        double angle = LineObject.AngleBetween(l1, l2);

        // 找交點作為頂點
        var intersection = LineObject.Intersection(l1, l2);
        var vertex = intersection ?? new DrawingPointF(
            (l1.StartPoint.X + l2.StartPoint.X) / 2,
            (l1.StartPoint.Y + l2.StartPoint.Y) / 2);

        var result = new AngleMeasurementResult(
            l1.StartPoint,
            vertex,
            l2.StartPoint,
            "線對線夾角")
        {
            SourceObjects = { l1, l2 }
        };
        result.Value = angle; // 覆寫計算值

        _resultManager.Add(result);
        _canvas.AddObject(result);
        _statusLabel.Text = $"量測結果: {angle:F2}°";
    }

    private List<IGeometryObject> GetSelectedObjectsFromTree()
    {
        // 返回多選列表的複製
        if (_multiSelectedObjects.Count > 0)
        {
            return new List<IGeometryObject>(_multiSelectedObjects);
        }
        // 如果沒有多選，返回當前選取的
        if (_resultManager.SelectedObject != null)
        {
            return new List<IGeometryObject> { _resultManager.SelectedObject };
        }
        return new List<IGeometryObject>();
    }

    private IEnumerable<TreeNode> GetAllNodes(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            yield return node;
            foreach (var child in GetAllNodes(node.Nodes))
                yield return child;
        }
    }

    private int AddToolOption(int y, string label, double defaultValue, double min, double max, Action<double> onValueChanged)
    {
        var lbl = new Label
        {
            Text = $"{label}:",
            Location = new System.Drawing.Point(8, y + 4),
            AutoSize = true,
            Font = new Font("Microsoft JhengHei", 9)
        };
        var num = new NumericUpDown
        {
            Location = new System.Drawing.Point(85, y),
            Width = 75,
            Height = 24,
            Minimum = (decimal)min,
            Maximum = (decimal)max,
            Value = (decimal)defaultValue,
            DecimalPlaces = defaultValue % 1 == 0 ? 0 : 1,
            Font = new Font("Microsoft JhengHei", 9)
        };
        num.ValueChanged += (s, e) => onValueChanged((double)num.Value);
        _toolOptions.Controls.Add(lbl);
        _toolOptions.Controls.Add(num);
        return y + 30;
    }

    private void Canvas_RoiSelected(object? sender, DrawingRectangleF roi)
    {
        if (_currentDetector == null) return;
        ExecuteDetection(roi);
    }

    private void Canvas_LineDrawn(object? sender, (DrawingPointF Start, DrawingPointF End) line)
    {
        // 卡尺法：設定搜尋線並執行檢測
        if (_currentDetector is LineDetector lineDetector && lineDetector.Method == LineDetectionMethod.Caliper)
        {
            lineDetector.SearchStart = line.Start;
            lineDetector.SearchEnd = line.End;
            ExecuteDetection(null);
        }
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
            // 顯示處理中指示
            Cursor = Cursors.WaitCursor;
            _statusLabel.Text = "檢測中...";
            Application.DoEvents();

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
            _statusLabel.Text = "檢測失敗";
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RefreshObjectTree()
    {
        _objectTree.Nodes.Clear();

        var circleNode = _objectTree.Nodes.Add("circles", $"圓形 ({_resultManager.Circles.Count()})");
        var lineNode = _objectTree.Nodes.Add("lines", $"直線 ({_resultManager.Lines.Count()})");
        var pointNode = _objectTree.Nodes.Add("points", $"點 ({_resultManager.Points.Count()})");
        var contourNode = _objectTree.Nodes.Add("contours", $"輪廓 ({_resultManager.Contours.Count()})");
        var measureNode = _objectTree.Nodes.Add("measures", $"量測 ({_resultManager.Measurements.Count()})");

        foreach (var obj in _resultManager.Objects)
        {
            var node = obj switch
            {
                CircleObject => circleNode.Nodes.Add(obj.Id, $"{obj.Name}: {obj.GetSummary()}"),
                LineObject => lineNode.Nodes.Add(obj.Id, $"{obj.Name}: {obj.GetSummary()}"),
                PointObject => pointNode.Nodes.Add(obj.Id, $"{obj.Name}: {obj.GetSummary()}"),
                ContourObject => contourNode.Nodes.Add(obj.Id, $"{obj.Name}: {obj.GetSummary()}"),
                MeasurementResult => measureNode.Nodes.Add(obj.Id, $"{obj.Name}: {obj.GetSummary()}"),
                _ => null
            };
            if (node != null) node.Tag = obj;
        }

        _objectTree.ExpandAll();
    }

    private void ObjectTree_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        // 只在非 Ctrl 點擊時處理（Ctrl 點擊由 NodeMouseClick 處理）
        if (ModifierKeys != Keys.Control && e.Node?.Tag is IGeometryObject obj)
        {
            _multiSelectedObjects.Clear();
            _multiSelectedObjects.Add(obj);
            SelectObject(obj);
            UpdateTreeNodeHighlight();
        }
    }

    private void ObjectTree_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (ModifierKeys == Keys.Control && e.Node?.Tag is IGeometryObject obj)
        {
            // Ctrl+點擊：切換多選
            if (_multiSelectedObjects.Contains(obj))
            {
                _multiSelectedObjects.Remove(obj);
            }
            else
            {
                _multiSelectedObjects.Add(obj);
            }
            UpdateTreeNodeHighlight();
            _statusLabel.Text = $"已選取 {_multiSelectedObjects.Count} 個物件";
        }
    }

    private void UpdateTreeNodeHighlight()
    {
        // 更新樹節點的視覺狀態
        foreach (TreeNode node in GetAllNodes(_objectTree.Nodes))
        {
            if (node.Tag is IGeometryObject obj)
            {
                node.BackColor = _multiSelectedObjects.Contains(obj) ? Color.LightBlue : Color.White;
            }
        }
    }

    private void SelectObject(IGeometryObject obj)
    {
        // 防止無限遞迴
        if (_isSelectingObject) return;
        _isSelectingObject = true;

        try
        {
            _resultManager.Select(obj);
            _canvas.SelectObject(obj);

            // 顯示屬性
            var details = obj.GetDetails();
            _propertyGrid.SelectedObject = new DictionaryPropertyGridAdapter(details);
        }
        finally
        {
            _isSelectingObject = false;
        }
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
        _statusLabel.Text = "已清除所有物件";
    }

    private void SetCalibration()
    {
        using var dialog = new Form
        {
            Text = "校正設定",
            Size = new System.Drawing.Size(400, 350),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var currentLabel = new Label
        {
            Text = $"當前: {_calibration.GetResolutionString()}",
            Location = new System.Drawing.Point(20, 15),
            AutoSize = true,
            Font = new Font("Microsoft JhengHei", 9, FontStyle.Bold)
        };

        // 方法選擇
        var methodGroup = new GroupBox
        {
            Text = "校正方法",
            Location = new System.Drawing.Point(15, 45),
            Size = new System.Drawing.Size(355, 200)
        };

        // 方法1: 手動輸入
        var manualRadio = new RadioButton { Text = "手動輸入解析度", Location = new System.Drawing.Point(15, 25), AutoSize = true, Checked = true };
        var manualLabel = new Label { Text = "解析度 (mm/pixel):", Location = new System.Drawing.Point(35, 52), AutoSize = true };
        var manualNum = new NumericUpDown
        {
            Location = new System.Drawing.Point(160, 50),
            Width = 100,
            DecimalPlaces = 6,
            Minimum = 0.000001m,
            Maximum = 100,
            Value = (decimal)_calibration.Resolution,
            Increment = 0.001m
        };

        // 方法2: 圓形校正
        var circleRadio = new RadioButton { Text = "使用已檢測圓形校正", Location = new System.Drawing.Point(15, 85), AutoSize = true };
        var circleCombo = new ComboBox { Location = new System.Drawing.Point(35, 110), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
        var circleDiaLabel = new Label { Text = "實際直徑 (mm):", Location = new System.Drawing.Point(195, 113), AutoSize = true };
        var circleDiaNum = new NumericUpDown
        {
            Location = new System.Drawing.Point(290, 110),
            Width = 55,
            DecimalPlaces = 3,
            Minimum = 0.001m,
            Maximum = 10000,
            Value = 10,
            Enabled = false
        };

        // 方法3: 兩點距離校正
        var distRadio = new RadioButton { Text = "使用兩點距離校正", Location = new System.Drawing.Point(15, 145), AutoSize = true };
        var distLabel1 = new Label { Text = "選取兩點 (Ctrl+點)", Location = new System.Drawing.Point(35, 170), AutoSize = true, ForeColor = Color.Gray };
        var distActualLabel = new Label { Text = "實際距離 (mm):", Location = new System.Drawing.Point(195, 170), AutoSize = true };
        var distActualNum = new NumericUpDown
        {
            Location = new System.Drawing.Point(290, 167),
            Width = 55,
            DecimalPlaces = 3,
            Minimum = 0.001m,
            Maximum = 10000,
            Value = 10,
            Enabled = false
        };

        // 填入圓形選項
        foreach (var c in _resultManager.Circles)
        {
            circleCombo.Items.Add($"{c.Name} (D={c.Diameter:F1}px)");
        }
        if (circleCombo.Items.Count > 0) circleCombo.SelectedIndex = 0;

        // 啟用/禁用控件
        manualRadio.CheckedChanged += (s, e) => { manualNum.Enabled = manualRadio.Checked; };
        circleRadio.CheckedChanged += (s, e) => { circleCombo.Enabled = circleDiaNum.Enabled = circleRadio.Checked; };
        distRadio.CheckedChanged += (s, e) => { distActualNum.Enabled = distRadio.Checked; };

        methodGroup.Controls.AddRange(new Control[] { manualRadio, manualLabel, manualNum, circleRadio, circleCombo, circleDiaLabel, circleDiaNum, distRadio, distLabel1, distActualLabel, distActualNum });

        var okBtn = new Button { Text = "確定", DialogResult = DialogResult.OK, Location = new System.Drawing.Point(120, 265), Width = 70 };
        var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new System.Drawing.Point(200, 265), Width = 70 };
        var resetBtn = new Button { Text = "重置", Location = new System.Drawing.Point(280, 265), Width = 70 };
        resetBtn.Click += (s, e) =>
        {
            _calibration.Reset();
            currentLabel.Text = $"當前: {_calibration.GetResolutionString()}";
            manualNum.Value = 1;
        };

        dialog.Controls.AddRange(new Control[] { currentLabel, methodGroup, okBtn, cancelBtn, resetBtn });
        dialog.AcceptButton = okBtn;
        dialog.CancelButton = cancelBtn;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                if (manualRadio.Checked)
                {
                    _calibration.SetResolution((double)manualNum.Value);
                }
                else if (circleRadio.Checked && circleCombo.SelectedIndex >= 0)
                {
                    var circles = _resultManager.Circles.ToList();
                    if (circleCombo.SelectedIndex < circles.Count)
                    {
                        var circle = circles[circleCombo.SelectedIndex];
                        _calibration.CalibrateWithCircle(circle.Diameter, (double)circleDiaNum.Value);
                    }
                }
                else if (distRadio.Checked)
                {
                    var points = GetSelectedObjectsFromTree().OfType<PointObject>().ToList();
                    if (points.Count >= 2)
                    {
                        double dx = points[1].X - points[0].X;
                        double dy = points[1].Y - points[0].Y;
                        double distPx = Math.Sqrt(dx * dx + dy * dy);
                        _calibration.CalibrateWithDistance(distPx, (double)distActualNum.Value);
                    }
                    else
                    {
                        MessageBox.Show("請先 Ctrl+點擊選取兩個點", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                _statusLabel.Text = $"校正已設定: {_calibration.GetResolutionString()}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"校正失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

    private void ExportJson()
    {
        if (_resultManager.Objects.Count == 0)
        {
            MessageBox.Show("無物件可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = "detection_results.json" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var exportData = new
            {
                ExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Calibration = new
                {
                    _calibration.IsCalibrated,
                    _calibration.Resolution,
                    Unit = _calibration.IsCalibrated ? "mm" : "px"
                },
                Objects = _resultManager.Objects.Select(obj => new
                {
                    obj.Id,
                    obj.Name,
                    obj.ObjectType,
                    Summary = obj.GetSummary(),
                    Details = obj.GetDetails()
                }).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            File.WriteAllText(dialog.FileName, json);
            MessageBox.Show("JSON 匯出成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void ExportAnnotatedImage()
    {
        var mat = _useOriginalImage ? _imageManager.OriginalImage : _imageManager.ProcessedImage;
        if (mat == null)
        {
            MessageBox.Show("無影像可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "PNG|*.png|JPEG|*.jpg", FileName = "annotated_image.png" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                using var annotated = mat.Clone();

                // 轉換為彩色以繪製標註
                using var colorMat = annotated.Channels() == 1
                    ? annotated.CvtColor(ColorConversionCodes.GRAY2BGR)
                    : annotated.Clone();

                foreach (var obj in _resultManager.Objects)
                {
                    var color = new Scalar(obj.DisplayColor.B, obj.DisplayColor.G, obj.DisplayColor.R);

                    if (obj is CircleObject circle)
                    {
                        Cv2.Circle(colorMat, new OpenCvSharp.Point(circle.CenterX, circle.CenterY), (int)circle.Radius, color, 2);
                        Cv2.Circle(colorMat, new OpenCvSharp.Point(circle.CenterX, circle.CenterY), 3, color, -1);
                        Cv2.PutText(colorMat, circle.Name, new OpenCvSharp.Point(circle.CenterX + 5, circle.CenterY - 5),
                            HersheyFonts.HersheySimplex, 0.4, color, 1);
                    }
                    else if (obj is LineObject line)
                    {
                        Cv2.Line(colorMat, new OpenCvSharp.Point(line.StartX, line.StartY),
                            new OpenCvSharp.Point(line.EndX, line.EndY), color, 2);
                        Cv2.PutText(colorMat, line.Name, new OpenCvSharp.Point((line.StartX + line.EndX) / 2, (line.StartY + line.EndY) / 2 - 5),
                            HersheyFonts.HersheySimplex, 0.4, color, 1);
                    }
                    else if (obj is PointObject point)
                    {
                        Cv2.Circle(colorMat, new OpenCvSharp.Point(point.X, point.Y), 5, color, -1);
                        Cv2.PutText(colorMat, point.Name, new OpenCvSharp.Point(point.X + 5, point.Y - 5),
                            HersheyFonts.HersheySimplex, 0.4, color, 1);
                    }
                    else if (obj is MeasurementResult measurement && measurement.AnnotationPoints.Count >= 2)
                    {
                        var p1 = measurement.AnnotationPoints[0];
                        var p2 = measurement.AnnotationPoints[1];
                        Cv2.Line(colorMat, new OpenCvSharp.Point(p1.X, p1.Y),
                            new OpenCvSharp.Point(p2.X, p2.Y), color, 2);
                        var midX = (p1.X + p2.X) / 2;
                        var midY = (p1.Y + p2.Y) / 2;
                        Cv2.PutText(colorMat, measurement.GetSummary(), new OpenCvSharp.Point(midX, midY - 5),
                            HersheyFonts.HersheySimplex, 0.4, color, 1);
                    }
                }

                colorMat.SaveImage(dialog.FileName);
                MessageBox.Show("標註影像匯出成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportHtmlReport()
    {
        if (_resultManager.Objects.Count == 0)
        {
            MessageBox.Show("無物件可匯出", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog { Filter = "HTML|*.html", FileName = "detection_report.html" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html><head><meta charset='utf-8'>");
                sb.AppendLine("<title>檢測報告</title>");
                sb.AppendLine("<style>");
                sb.AppendLine("body { font-family: 'Microsoft JhengHei', Arial, sans-serif; margin: 20px; }");
                sb.AppendLine("h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
                sb.AppendLine("h2 { color: #666; margin-top: 30px; }");
                sb.AppendLine("table { border-collapse: collapse; width: 100%; margin-bottom: 20px; }");
                sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
                sb.AppendLine("th { background-color: #4CAF50; color: white; }");
                sb.AppendLine("tr:nth-child(even) { background-color: #f2f2f2; }");
                sb.AppendLine(".summary { background: #e8f5e9; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
                sb.AppendLine(".img-container { text-align: center; margin: 20px 0; }");
                sb.AppendLine(".img-container img { max-width: 100%; border: 1px solid #ddd; }");
                sb.AppendLine("</style></head><body>");

                sb.AppendLine("<h1>機器視覺檢測報告</h1>");
                sb.AppendLine($"<p>生成時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");

                // 摘要
                sb.AppendLine("<div class='summary'>");
                sb.AppendLine($"<p><strong>校正狀態:</strong> {(_calibration.IsCalibrated ? "已校正" : "未校正")}</p>");
                if (_calibration.IsCalibrated)
                    sb.AppendLine($"<p><strong>解析度:</strong> {_calibration.GetResolutionString()}</p>");
                sb.AppendLine($"<p><strong>總物件數:</strong> {_resultManager.Objects.Count}</p>");
                sb.AppendLine($"<p><strong>圓形:</strong> {_resultManager.Circles.Count()}, <strong>直線:</strong> {_resultManager.Lines.Count()}, <strong>點:</strong> {_resultManager.Points.Count()}, <strong>量測:</strong> {_resultManager.Measurements.Count()}</p>");
                sb.AppendLine("</div>");

                // 嵌入影像 (Base64)
                var mat = _useOriginalImage ? _imageManager.OriginalImage : _imageManager.ProcessedImage;
                if (mat != null)
                {
                    try
                    {
                        using var tempMat = mat.Clone();
                        var imgBytes = tempMat.ToBytes(".png");
                        var base64 = Convert.ToBase64String(imgBytes);
                        sb.AppendLine("<div class='img-container'>");
                        sb.AppendLine($"<img src='data:image/png;base64,{base64}' alt='檢測影像'/>");
                        sb.AppendLine("</div>");
                    }
                    catch { /* 忽略影像嵌入錯誤 */ }
                }

                // 圓形表格
                if (_resultManager.Circles.Any())
                {
                    sb.AppendLine("<h2>圓形</h2>");
                    sb.AppendLine("<table><tr><th>名稱</th><th>中心X</th><th>中心Y</th><th>半徑</th><th>直徑</th></tr>");
                    foreach (var c in _resultManager.Circles)
                    {
                        sb.AppendLine($"<tr><td>{c.Name}</td><td>{c.CenterX:F2}</td><td>{c.CenterY:F2}</td><td>{c.Radius:F2}</td><td>{c.Diameter:F2}</td></tr>");
                    }
                    sb.AppendLine("</table>");
                }

                // 直線表格
                if (_resultManager.Lines.Any())
                {
                    sb.AppendLine("<h2>直線</h2>");
                    sb.AppendLine("<table><tr><th>名稱</th><th>起點X</th><th>起點Y</th><th>終點X</th><th>終點Y</th><th>長度</th><th>角度</th></tr>");
                    foreach (var l in _resultManager.Lines)
                    {
                        sb.AppendLine($"<tr><td>{l.Name}</td><td>{l.StartX:F2}</td><td>{l.StartY:F2}</td><td>{l.EndX:F2}</td><td>{l.EndY:F2}</td><td>{l.Length:F2}</td><td>{l.AngleDegrees:F2}°</td></tr>");
                    }
                    sb.AppendLine("</table>");
                }

                // 點表格
                if (_resultManager.Points.Any())
                {
                    sb.AppendLine("<h2>點</h2>");
                    sb.AppendLine("<table><tr><th>名稱</th><th>X</th><th>Y</th></tr>");
                    foreach (var p in _resultManager.Points)
                    {
                        sb.AppendLine($"<tr><td>{p.Name}</td><td>{p.X:F2}</td><td>{p.Y:F2}</td></tr>");
                    }
                    sb.AppendLine("</table>");
                }

                // 量測表格
                if (_resultManager.Measurements.Any())
                {
                    sb.AppendLine("<h2>量測結果</h2>");
                    sb.AppendLine("<table><tr><th>名稱</th><th>類型</th><th>數值(px)</th><th>數值(mm)</th></tr>");
                    foreach (var m in _resultManager.Measurements)
                    {
                        string mmValue = m.ValueMm.HasValue ? $"{m.ValueMm:F3}" : "-";
                        sb.AppendLine($"<tr><td>{m.Name}</td><td>{m.Type}</td><td>{m.Value:F3}</td><td>{mmValue}</td></tr>");
                    }
                    sb.AppendLine("</table>");
                }

                sb.AppendLine("</body></html>");

                File.WriteAllText(dialog.FileName, sb.ToString());
                MessageBox.Show("HTML 報告匯出成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"匯出失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void AddGeometryMeasurementUI(int y, string instruction, string formula)
    {
        var infoLabel = new Label
        {
            Text = $"1. Ctrl+點擊{instruction}\n2. 點擊上方「執行」按鈕",
            Location = new System.Drawing.Point(5, y),
            Size = new System.Drawing.Size(170, 50),
            ForeColor = Color.DarkBlue
        };
        _toolOptions.Controls.Add(infoLabel);

        var formulaLabel = new Label
        {
            Text = $"計算: {formula}",
            Location = new System.Drawing.Point(5, y + 55),
            Size = new System.Drawing.Size(170, 25),
            ForeColor = Color.Gray
        };
        _toolOptions.Controls.Add(formulaLabel);
    }

    private void ExecuteConcentricityMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        var circles = selected.OfType<CircleObject>().ToList();

        if (circles.Count != 2)
        {
            MessageBox.Show("請在物件列表中選取兩個圓 (使用 Ctrl+點擊多選)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = new ConcentricityMeasurementResult(circles[0], circles[1], "同心度");

        // 套用校正
        result.ValueMm = _calibration.PixelsToMm(result.Value);
        if (_calibration.IsCalibrated)
            result.Unit = "mm";

        _resultManager.Add(result);
        _canvas.AddObject(result);
        _statusLabel.Text = $"量測結果: {result.GetSummary()}";
    }

    private void ExecuteParallelismMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        var lines = selected.OfType<LineObject>().ToList();

        if (lines.Count != 2)
        {
            MessageBox.Show("請在物件列表中選取兩條線 (使用 Ctrl+點擊多選)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = new ParallelismMeasurementResult(lines[0], lines[1], "平行度");

        _resultManager.Add(result);
        _canvas.AddObject(result);
        _statusLabel.Text = $"量測結果: {result.GetSummary()}";
    }

    private void ExecutePerpendicularityMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        var lines = selected.OfType<LineObject>().ToList();

        if (lines.Count != 2)
        {
            MessageBox.Show("請在物件列表中選取兩條線 (使用 Ctrl+點擊多選)", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = new PerpendicularityMeasurementResult(lines[0], lines[1], "垂直度");

        _resultManager.Add(result);
        _canvas.AddObject(result);
        _statusLabel.Text = $"量測結果: {result.GetSummary()}";
    }

    private void ExecuteCircularityMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        var circle = selected.OfType<CircleObject>().FirstOrDefault();

        if (circle == null)
        {
            MessageBox.Show("請在物件列表中選取一個圓", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = new CircularityMeasurementResult(circle, "圓度");

        _resultManager.Add(result);
        _canvas.AddObject(result);
        _statusLabel.Text = $"量測結果: {result.GetSummary()}";
    }

    private void ExecuteLinearityMeasurement()
    {
        var selected = GetSelectedObjectsFromTree();
        var line = selected.OfType<LineObject>().FirstOrDefault();

        if (line == null)
        {
            MessageBox.Show("請在物件列表中選取一條線", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = new LinearityMeasurementResult(line, "直線度");

        _resultManager.Add(result);
        _canvas.AddObject(result);
        _statusLabel.Text = $"量測結果: {result.GetSummary()}";
    }

    private void Canvas_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _imageManager.LoadImage(files[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
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
