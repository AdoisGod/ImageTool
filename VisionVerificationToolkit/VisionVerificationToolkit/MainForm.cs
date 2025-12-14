using VisionVerificationToolkit.Core;
using VisionVerificationToolkit.Pages;
using DrawingSize = System.Drawing.Size;

namespace VisionVerificationToolkit;

/// <summary>
/// 主視窗 - 三頁面 TabControl 架構
/// </summary>
public partial class MainForm : Form
{
    private readonly ImageManager _imageManager = new();
    private readonly CalibrationManager _calibration = new();
    private readonly PipelineManager _pipelineManager = new();

    private TabControl _tabControl = null!;
    private PreprocessingPage _preprocessingPage = null!;
    private DetectionPage _detectionPage = null!;
    private ParameterSearchPage _parameterSearchPage = null!;

    private ToolStripStatusLabel _imageInfoLabel = null!;
    private ToolStripStatusLabel _mouseLabel = null!;
    private ToolStripStatusLabel _memoryLabel = null!;
    private System.Windows.Forms.Timer _statusTimer = null!;

    public MainForm()
    {
        InitializeComponent();
        SetupPages();
        SetupEvents();
        SetupShortcuts();
        StartStatusTimer();
    }

    private void InitializeComponent()
    {
        Text = "Vision Verification Toolkit - 機器視覺驗證工具集";
        Size = new DrawingSize(1400, 900);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new DrawingSize(1200, 800);

        // 主選單
        var menuStrip = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("檔案(&F)");
        fileMenu.DropDownItems.Add("開啟影像", null, (s, e) => LoadImage());
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add("結束", null, (s, e) => Close());
        menuStrip.Items.Add(fileMenu);

        var viewMenu = new ToolStripMenuItem("檢視(&V)");
        viewMenu.DropDownItems.Add("前處理", null, (s, e) => _tabControl.SelectedIndex = 0);
        viewMenu.DropDownItems.Add("檢測/量測", null, (s, e) => _tabControl.SelectedIndex = 1);
        viewMenu.DropDownItems.Add("參數搜尋", null, (s, e) => _tabControl.SelectedIndex = 2);
        menuStrip.Items.Add(viewMenu);

        var helpMenu = new ToolStripMenuItem("說明(&H)");
        helpMenu.DropDownItems.Add("關於", null, (s, e) => ShowAbout());
        menuStrip.Items.Add(helpMenu);

        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        // 狀態列
        var statusStrip = new StatusStrip();
        _imageInfoLabel = new ToolStripStatusLabel("影像: 無") { AutoSize = false, Width = 250 };
        _mouseLabel = new ToolStripStatusLabel("座標: -") { AutoSize = false, Width = 150 };
        _memoryLabel = new ToolStripStatusLabel("記憶體: -") { AutoSize = false, Width = 120 };
        var spacer = new ToolStripStatusLabel { Spring = true };

        statusStrip.Items.AddRange(new ToolStripItem[] { _imageInfoLabel, _mouseLabel, _memoryLabel, spacer });
        Controls.Add(statusStrip);

        // TabControl
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft JhengHei", 10)
        };
        Controls.Add(_tabControl);
    }

    private void SetupPages()
    {
        // 頁面1：前處理
        _preprocessingPage = new PreprocessingPage(_imageManager, _pipelineManager);
        var tab1 = new TabPage("頁面1: 前處理") { Padding = new Padding(3) };
        tab1.Controls.Add(_preprocessingPage);
        _tabControl.TabPages.Add(tab1);

        // 頁面2：檢測/量測
        _detectionPage = new DetectionPage(_imageManager, _calibration);
        var tab2 = new TabPage("頁面2: 檢測/量測") { Padding = new Padding(3) };
        tab2.Controls.Add(_detectionPage);
        _tabControl.TabPages.Add(tab2);

        // 頁面3：參數搜尋
        _parameterSearchPage = new ParameterSearchPage(_imageManager, _pipelineManager);
        var tab3 = new TabPage("頁面3: 參數搜尋") { Padding = new Padding(3) };
        tab3.Controls.Add(_parameterSearchPage);
        _tabControl.TabPages.Add(tab3);

        // 頁面切換時更新檢測頁面的影像
        _tabControl.SelectedIndexChanged += (s, e) =>
        {
            if (_tabControl.SelectedIndex == 1)
            {
                _detectionPage.UpdateImage();
            }
        };

        // 前處理頁面傳送至檢測
        _preprocessingPage.SendToDetection += (s, e) =>
        {
            _tabControl.SelectedIndex = 1;
            _detectionPage.UpdateImage();
        };
    }

    private void SetupEvents()
    {
        _imageManager.OriginalImageChanged += (s, e) =>
        {
            UpdateImageInfo();
        };

        AllowDrop = true;
        DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (s, e) =>
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
        };
    }

    private void SetupShortcuts()
    {
        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            switch (e.KeyCode)
            {
                case Keys.D1:
                    _tabControl.SelectedIndex = 0;
                    e.Handled = true;
                    break;
                case Keys.D2:
                    _tabControl.SelectedIndex = 1;
                    e.Handled = true;
                    break;
                case Keys.D3:
                    _tabControl.SelectedIndex = 2;
                    e.Handled = true;
                    break;
                case Keys.O when e.Control:
                    LoadImage();
                    e.Handled = true;
                    break;
            }
        };
    }

    private void StartStatusTimer()
    {
        _statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statusTimer.Tick += (s, e) =>
        {
            var memory = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
            _memoryLabel.Text = $"記憶體: {memory:F1} MB";
        };
        _statusTimer.Start();
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UpdateImageInfo()
    {
        if (_imageManager.HasImage)
        {
            string path = _imageManager.ImagePath ?? "合成影像";
            string filename = Path.GetFileName(path);
            _imageInfoLabel.Text = $"影像: {filename} ({_imageManager.Width}x{_imageManager.Height})";
        }
        else
        {
            _imageInfoLabel.Text = "影像: 無";
        }
    }

    private void ShowAbout()
    {
        MessageBox.Show(
            "Vision Verification Toolkit\n" +
            "機器視覺驗證工具集\n\n" +
            "版本：1.0.0\n\n" +
            "功能：\n" +
            "• 影像前處理 Pipeline\n" +
            "• 影像品質分析\n" +
            "• 圓形/直線/點/輪廓檢測\n" +
            "• 幾何量測\n" +
            "• 參數自動搜尋\n\n" +
            "快捷鍵：\n" +
            "• Ctrl+O: 開啟影像\n" +
            "• 1/2/3: 切換頁面\n" +
            "• 滾輪: 縮放影像\n" +
            "• 中鍵拖曳: 平移影像",
            "關於",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        _statusTimer?.Stop();
        _imageManager?.Dispose();
    }
}
