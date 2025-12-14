using OpenCvSharp;
using VisionVerificationToolkit.Core;
using VisionVerificationToolkit.Detection;
using VisionVerificationToolkit.Processing;
using VisionVerificationToolkit.Processing.Steps;
using DrawingSize = System.Drawing.Size;

namespace VisionVerificationToolkit.Pages;

/// <summary>
/// 頁面3：參數搜尋
/// </summary>
public class ParameterSearchPage : UserControl
{
    private readonly ImageManager _imageManager;
    private readonly PipelineManager _pipelineManager;

    private ComboBox _targetCombo = null!;
    private DataGridView _paramGrid = null!;
    private ComboBox _evalTypeCombo = null!;
    private NumericUpDown _expectedCountNum = null!;
    private RadioButton _gridSearchRadio = null!;
    private RadioButton _randomSearchRadio = null!;
    private ProgressBar _progressBar = null!;
    private Label _progressLabel = null!;
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private DataGridView _resultGrid = null!;
    private Label _statusLabel = null!;

    private CancellationTokenSource? _cts;
    private List<SearchResult> _searchResults = new();

    public ParameterSearchPage(ImageManager imageManager, PipelineManager pipelineManager)
    {
        _imageManager = imageManager;
        _pipelineManager = pipelineManager;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(10);

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200)); // 搜尋設定
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // 評估設定
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // 搜尋控制
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 結果

        // 搜尋設定
        var searchGroup = new GroupBox { Text = "搜尋設定", Dock = DockStyle.Fill };
        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        searchPanel.Controls.Add(new Label { Text = "搜尋目標:", Anchor = AnchorStyles.Left }, 0, 0);
        _targetCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        _targetCombo.Items.AddRange(new[] { "找圓 (霍夫)", "找圓 (邊緣擬合)", "找線 (卡尺)", "邊緣檢測" });
        _targetCombo.SelectedIndex = 0;
        _targetCombo.SelectedIndexChanged += TargetCombo_SelectedIndexChanged;
        searchPanel.Controls.Add(_targetCombo, 1, 0);

        searchPanel.Controls.Add(new Label { Text = "參數範圍:", Anchor = AnchorStyles.Left }, 0, 1);
        _paramGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            RowHeadersVisible = false
        };
        _paramGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "啟用", Width = 50 });
        _paramGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "參數", Width = 120, ReadOnly = true });
        _paramGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "起始值", Width = 80 });
        _paramGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "結束值", Width = 80 });
        _paramGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "步進", Width = 60 });
        searchPanel.Controls.Add(_paramGrid, 1, 1);

        searchGroup.Controls.Add(searchPanel);
        mainLayout.Controls.Add(searchGroup, 0, 0);

        // 評估設定
        var evalGroup = new GroupBox { Text = "評估方式", Dock = DockStyle.Fill };
        var evalPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };

        var evalTypePanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Height = 30 };
        evalTypePanel.Controls.Add(new Label { Text = "評估類型:", AutoSize = true });
        _evalTypeCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _evalTypeCombo.Items.AddRange(new[] { "數量符合", "品質最佳", "位置符合" });
        _evalTypeCombo.SelectedIndex = 0;
        evalTypePanel.Controls.Add(_evalTypeCombo);
        evalPanel.Controls.Add(evalTypePanel);

        var countPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Height = 30 };
        countPanel.Controls.Add(new Label { Text = "預期檢測數量:", AutoSize = true });
        _expectedCountNum = new NumericUpDown { Width = 60, Minimum = 1, Maximum = 100, Value = 1 };
        countPanel.Controls.Add(_expectedCountNum);
        evalPanel.Controls.Add(countPanel);

        evalGroup.Controls.Add(evalPanel);
        mainLayout.Controls.Add(evalGroup, 0, 1);

        // 搜尋控制
        var controlGroup = new GroupBox { Text = "搜尋控制", Dock = DockStyle.Fill };
        var controlPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };

        _gridSearchRadio = new RadioButton { Text = "Grid Search", Checked = true, AutoSize = true };
        _randomSearchRadio = new RadioButton { Text = "Random Search", AutoSize = true };
        controlPanel.Controls.Add(_gridSearchRadio);
        controlPanel.Controls.Add(_randomSearchRadio);

        _startButton = new Button { Text = "開始搜尋", Width = 80 };
        _startButton.Click += StartButton_Click;
        _stopButton = new Button { Text = "停止", Width = 60, Enabled = false };
        _stopButton.Click += (s, e) => _cts?.Cancel();
        controlPanel.Controls.Add(_startButton);
        controlPanel.Controls.Add(_stopButton);

        _progressBar = new ProgressBar { Width = 200, Height = 20 };
        _progressLabel = new Label { Text = "就緒", AutoSize = true };
        controlPanel.Controls.Add(_progressBar);
        controlPanel.Controls.Add(_progressLabel);

        controlGroup.Controls.Add(controlPanel);
        mainLayout.Controls.Add(controlGroup, 0, 2);

        // 搜尋結果
        var resultGroup = new GroupBox { Text = "搜尋結果", Dock = DockStyle.Fill };
        var resultPanel = new Panel { Dock = DockStyle.Fill };

        _resultGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "排名", Width = 50 });
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "評分", Width = 80 });
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "參數", Width = 300 });
        _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "檢測數", Width = 60 });
        _resultGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "套用", Width = 60, Text = "套用", UseColumnTextForButtonValue = true });
        _resultGrid.CellClick += ResultGrid_CellClick;
        resultPanel.Controls.Add(_resultGrid);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 35 };
        var applyBtn = new Button { Text = "套用最佳參數", Width = 100 };
        applyBtn.Click += (s, e) => ApplyBestResult();
        var exportBtn = new Button { Text = "匯出結果", Width = 80 };
        exportBtn.Click += (s, e) => ExportResults();
        buttonPanel.Controls.Add(applyBtn);
        buttonPanel.Controls.Add(exportBtn);
        resultPanel.Controls.Add(buttonPanel);

        resultGroup.Controls.Add(resultPanel);
        mainLayout.Controls.Add(resultGroup, 0, 3);

        // 狀態
        _statusLabel = new Label { Dock = DockStyle.Bottom, Height = 25 };
        mainLayout.Controls.Add(_statusLabel);

        Controls.Add(mainLayout);

        // 初始化參數表
        UpdateParameterGrid();
    }

    private void TargetCombo_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateParameterGrid();
    }

    private void UpdateParameterGrid()
    {
        _paramGrid.Rows.Clear();

        switch (_targetCombo.SelectedIndex)
        {
            case 0: // 找圓 (霍夫)
                AddParameterRow(true, "高斯 σ", 1.0, 3.0, 0.5);
                AddParameterRow(true, "dp", 1.0, 2.0, 0.5);
                AddParameterRow(true, "minDist", 30, 100, 10);
                AddParameterRow(true, "param1", 50, 150, 25);
                AddParameterRow(true, "param2", 20, 50, 5);
                break;
            case 1: // 找圓 (邊緣擬合)
                AddParameterRow(true, "高斯 σ", 0.5, 3.0, 0.5);
                AddParameterRow(true, "Canny 低", 30, 100, 10);
                AddParameterRow(true, "Canny 高", 100, 250, 25);
                break;
            case 2: // 找線 (卡尺)
                AddParameterRow(true, "採樣數", 10, 30, 5);
                AddParameterRow(true, "剖面長", 20, 50, 10);
                AddParameterRow(true, "閾值", 20, 60, 10);
                break;
            case 3: // 邊緣檢測
                AddParameterRow(true, "高斯 σ", 0.5, 3.0, 0.5);
                AddParameterRow(true, "Canny 低", 30, 100, 10);
                AddParameterRow(true, "Canny 高", 100, 250, 25);
                break;
        }
    }

    private void AddParameterRow(bool enabled, string name, double start, double end, double step)
    {
        _paramGrid.Rows.Add(enabled, name, start, end, step);
    }

    private async void StartButton_Click(object? sender, EventArgs e)
    {
        if (_imageManager.ProcessedImage == null && _imageManager.OriginalImage == null)
        {
            MessageBox.Show("請先載入影像", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var parameters = GetSearchParameters();
        if (parameters.Count == 0)
        {
            MessageBox.Show("請至少勾選一個參數進行搜尋", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var combinations = GenerateCombinations(parameters);
        if (combinations.Count == 0)
        {
            MessageBox.Show("無法產生參數組合，請檢查參數範圍設定", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _progressLabel.Text = $"準備搜尋 {combinations.Count} 組參數...";

        _cts = new CancellationTokenSource();
        _startButton.Enabled = false;
        _stopButton.Enabled = true;
        _searchResults.Clear();
        _resultGrid.Rows.Clear();

        try
        {
            await Task.Run(() => ExecuteSearch(_cts.Token));
        }
        catch (OperationCanceledException)
        {
            _progressLabel.Text = "已取消";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"搜尋失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            DisplayResults();
        }
    }

    private void ExecuteSearch(CancellationToken ct)
    {
        var parameters = GetSearchParameters();
        var combinations = GenerateCombinations(parameters);

        int total = combinations.Count;
        int current = 0;

        UpdateProgress(0, total, "搜尋中...");

        var mat = _imageManager.ProcessedImage ?? _imageManager.OriginalImage;
        if (mat == null) return;

        foreach (var combo in combinations)
        {
            ct.ThrowIfCancellationRequested();

            var result = EvaluateCombination(mat, combo);
            _searchResults.Add(result);

            current++;
            UpdateProgress(current, total, $"搜尋中... {current}/{total}");
        }

        // 排序
        _searchResults = _searchResults.OrderByDescending(r => r.Score).ToList();
    }

    private List<SearchParameter> GetSearchParameters()
    {
        var list = new List<SearchParameter>();

        for (int i = 0; i < _paramGrid.Rows.Count; i++)
        {
            var row = _paramGrid.Rows[i];

            // 安全地取得 checkbox 值
            var cellValue = row.Cells[0].Value;
            bool enabled = cellValue != null && cellValue != DBNull.Value && Convert.ToBoolean(cellValue);
            if (!enabled) continue;

            string name = row.Cells[1].Value?.ToString() ?? "";
            if (string.IsNullOrEmpty(name)) continue;

            try
            {
                double start = Convert.ToDouble(row.Cells[2].Value ?? 0);
                double end = Convert.ToDouble(row.Cells[3].Value ?? 0);
                double step = Convert.ToDouble(row.Cells[4].Value ?? 1);

                if (step <= 0) step = 1;
                if (end < start) (start, end) = (end, start);

                list.Add(new SearchParameter(name, start, end, step));
            }
            catch { }
        }

        return list;
    }

    private List<Dictionary<string, double>> GenerateCombinations(List<SearchParameter> parameters)
    {
        var results = new List<Dictionary<string, double>> { new() };

        foreach (var param in parameters)
        {
            var newResults = new List<Dictionary<string, double>>();

            foreach (var existing in results)
            {
                for (double v = param.Start; v <= param.End; v += param.Step)
                {
                    var newCombo = new Dictionary<string, double>(existing) { [param.Name] = v };
                    newResults.Add(newCombo);
                }
            }

            results = newResults;
        }

        return results;
    }

    private SearchResult EvaluateCombination(Mat image, Dictionary<string, double> parameters)
    {
        int detectedCount = 0;
        double quality = 0;

        try
        {
            using var processed = ApplyPreprocessing(image, parameters);

            switch (_targetCombo.SelectedIndex)
            {
                case 0: // 找圓 (霍夫)
                case 1: // 找圓 (邊緣擬合)
                    var circleDetector = new CircleDetector
                    {
                        Method = _targetCombo.SelectedIndex == 0 ? CircleDetectionMethod.Hough : CircleDetectionMethod.EdgeFitting,
                        Param1 = parameters.GetValueOrDefault("param1", 100),
                        Param2 = parameters.GetValueOrDefault("param2", 30),
                        Dp = parameters.GetValueOrDefault("dp", 1.0),
                        MinDist = parameters.GetValueOrDefault("minDist", 50),
                        CannyThreshold1 = parameters.GetValueOrDefault("Canny 低", 50),
                        CannyThreshold2 = parameters.GetValueOrDefault("Canny 高", 150)
                    };
                    var circles = circleDetector.Detect(processed);
                    detectedCount = circles.Count;
                    quality = circles.Count > 0 ? circles.Average(c => ((Objects.CircleObject)c).RSquared ?? 0.5) : 0;
                    break;

                case 2: // 找線
                    var lineDetector = new LineDetector
                    {
                        Method = LineDetectionMethod.Hough,
                        HoughThreshold = parameters.GetValueOrDefault("閾值", 50)
                    };
                    var lines = lineDetector.Detect(processed);
                    detectedCount = lines.Count;
                    quality = lines.Count > 0 ? lines.Average(l => ((Objects.LineObject)l).RSquared ?? 0.5) : 0;
                    break;

                case 3: // 邊緣檢測
                    using (var edges = new Mat())
                    {
                        Cv2.Canny(processed, edges, parameters.GetValueOrDefault("Canny 低", 50), parameters.GetValueOrDefault("Canny 高", 150));
                        detectedCount = Cv2.CountNonZero(edges);
                        quality = detectedCount > 0 ? 1.0 : 0;
                    }
                    break;
            }
        }
        catch { }

        // 計算評分
        int expectedCount = (int)_expectedCountNum.Value;
        double score = _evalTypeCombo.SelectedIndex switch
        {
            0 => 1.0 - Math.Abs(detectedCount - expectedCount) / (double)Math.Max(1, expectedCount),
            1 => quality,
            _ => detectedCount == expectedCount ? 1.0 : 0
        };

        return new SearchResult
        {
            Parameters = parameters,
            DetectedCount = detectedCount,
            Quality = quality,
            Score = Math.Max(0, score)
        };
    }

    private Mat ApplyPreprocessing(Mat image, Dictionary<string, double> parameters)
    {
        var result = image.Clone();

        if (parameters.TryGetValue("高斯 σ", out var sigma) && sigma > 0)
        {
            using var blurred = new Mat();
            Cv2.GaussianBlur(result, blurred, new OpenCvSharp.Size(0, 0), sigma);
            result.Dispose();
            result = blurred.Clone();
        }

        return result;
    }

    private void UpdateProgress(int current, int total, string text)
    {
        if (InvokeRequired)
        {
            Invoke(() => UpdateProgress(current, total, text));
            return;
        }

        _progressBar.Maximum = total;
        _progressBar.Value = current;
        _progressLabel.Text = text;
    }

    private void DisplayResults()
    {
        _resultGrid.Rows.Clear();

        int rank = 1;
        foreach (var result in _searchResults.Take(50))
        {
            var paramStr = string.Join(", ", result.Parameters.Select(p => $"{p.Key}={p.Value:F1}"));
            _resultGrid.Rows.Add(rank++, $"{result.Score:F3}", paramStr, result.DetectedCount, "套用");
        }

        _statusLabel.Text = $"搜尋完成，共 {_searchResults.Count} 組合";
    }

    private void ResultGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == 4 && e.RowIndex >= 0 && e.RowIndex < _searchResults.Count)
        {
            ApplyResult(_searchResults[e.RowIndex]);
        }
    }

    private void ApplyBestResult()
    {
        if (_searchResults.Count > 0)
        {
            ApplyResult(_searchResults[0]);
        }
    }

    private void ApplyResult(SearchResult result)
    {
        _pipelineManager.Clear();

        if (result.Parameters.TryGetValue("高斯 σ", out var sigma) && sigma > 0)
        {
            _pipelineManager.AddStep(new GaussianBlurStep { SigmaX = sigma, SigmaY = sigma });
        }

        if (result.Parameters.ContainsKey("Canny 低") && result.Parameters.ContainsKey("Canny 高"))
        {
            _pipelineManager.AddStep(new CannyStep
            {
                Threshold1 = result.Parameters["Canny 低"],
                Threshold2 = result.Parameters["Canny 高"]
            });
        }

        MessageBox.Show($"已套用參數：\n{string.Join("\n", result.Parameters.Select(p => $"{p.Key} = {p.Value:F2}"))}", "套用成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportResults()
    {
        if (_searchResults.Count == 0) return;

        using var dialog = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "search_results.csv" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
            writer.WriteLine("Rank,Score,Parameters,DetectedCount,Quality");

            int rank = 1;
            foreach (var result in _searchResults)
            {
                var paramStr = string.Join("; ", result.Parameters.Select(p => $"{p.Key}={p.Value:F2}"));
                writer.WriteLine($"{rank++},{result.Score:F4},\"{paramStr}\",{result.DetectedCount},{result.Quality:F4}");
            }

            MessageBox.Show("匯出成功", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private class SearchParameter
    {
        public string Name { get; }
        public double Start { get; }
        public double End { get; }
        public double Step { get; }

        public SearchParameter(string name, double start, double end, double step)
        {
            Name = name;
            Start = start;
            End = end;
            Step = step;
        }
    }

    private class SearchResult
    {
        public Dictionary<string, double> Parameters { get; set; } = new();
        public int DetectedCount { get; set; }
        public double Quality { get; set; }
        public double Score { get; set; }
    }
}
