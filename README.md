# Subpixel Edge Comparator

亞像素邊緣檢測演算法比較工具（C# WinForms 版）

## 專案目標

開發一個具有圖形化操作介面的 Windows 桌面應用程式，用於比較四種亞像素邊緣檢測演算法的效果，協助使用者快速評估哪種演算法適合其影像特性。

## 技術堆疊

- .NET 8.0 (Windows)
- WinForms（圖形介面）
- OpenCvSharp4（影像處理）
- MathNet.Numerics（數值計算、曲線擬合）
- OxyPlot.WindowsForms（圖表繪製）

## NuGet 套件

```
OpenCvSharp4
OpenCvSharp4.runtime.win
MathNet.Numerics
OxyPlot.WindowsForms
```

---

## 介面佈局

主視窗採用左右分割佈局：

```
┌─────────────────────────────────────────────────────────────────┐
│  選單列：[檔案] [工具] [說明]                                      │
├────────────────────────────────────┬────────────────────────────┤
│                                    │  ┌────────────────────┐    │
│                                    │  │ 灰階剖面圖         │    │
│                                    │  │ (OxyPlot)          │    │
│      影像顯示區                     │  └────────────────────┘    │
│      (PictureBox)                  │  ┌────────────────────┐    │
│                                    │  │ 梯度剖面圖         │    │
│      - 顯示載入的影像               │  │ + 邊緣位置標記     │    │
│      - 滑鼠拖曳畫 ROI 線段          │  │ (OxyPlot)          │    │
│      - 顯示 ROI 線段（紅色）        │  └────────────────────┘    │
│                                    │  ┌────────────────────┐    │
│                                    │  │ 結果表格           │    │
│                                    │  │ (DataGridView)     │    │
├────────────────────────────────────┴────────────────────────────┤
│  工具列：[載入影像] [合成測試影像] [清除ROI] [執行分析] [匯出結果]   │
├─────────────────────────────────────────────────────────────────┤
│  狀態列：顯示目前狀態、滑鼠座標、ROI 資訊                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 功能規格

### 1. 載入影像

- 工具列按鈕「載入影像」或選單 [檔案] → [開啟]
- 開啟檔案對話框，篩選條件：`*.png;*.jpg;*.jpeg;*.bmp;*.tiff`
- 載入後自動轉灰階，顯示於左側影像顯示區
- 支援滑鼠滾輪縮放、拖曳平移
- 狀態列顯示影像尺寸與檔案路徑

### 2. 合成測試影像

- 工具列按鈕「合成測試影像」
- 彈出設定對話框：

```
┌─ 合成測試影像設定 ─────────────────┐
│                                   │
│  影像寬度：  [200    ] px         │
│  影像高度：  [200    ] px         │
│  邊緣位置：  [100.00 ] px         │
│  左側灰階：  [50     ] (0-255)    │
│  右側灰階：  [200    ] (0-255)    │
│  模糊程度：  [1.5    ] σ          │
│  加入雜訊：  [勾選框] σ=[5.0]     │
│                                   │
│        [確定]    [取消]           │
└───────────────────────────────────┘
```

- 產生影像後直接載入到主畫面
- 記錄真實邊緣位置，供後續驗證精度

### 3. ROI 線段選取

- 在影像顯示區，滑鼠操作：
  - 左鍵按下：記錄起點
  - 拖曳：即時顯示線段預覽（虛線）
  - 左鍵放開：確定終點，繪製正式 ROI 線段（紅色實線）
- 線段端點顯示座標標籤
- 狀態列顯示線段長度
- 線段長度 < 10 像素時，顯示警告並不接受
- 「清除 ROI」按鈕可重新選取

### 4. 執行分析

- 點擊「執行分析」按鈕
- 檢查前置條件：已載入影像、已選取有效 ROI
- 執行流程：
  1. 沿 ROI 線段提取灰階剖面（雙線性插值，超取樣 2x）
  2. 計算梯度
  3. 依序執行四種亞像素演算法
  4. 更新右側圖表與結果表格

### 5. 亞像素演算法實作

#### 5.1 拋物線擬合 (Parabolic Fit)

```
輸入：梯度陣列
步驟：
  1. 找到梯度絕對值最大的位置 idx
  2. 取 idx-1, idx, idx+1 三點
  3. 解聯立方程式求拋物線係數 a, b, c
  4. 峰值偏移 = -b / (2a)
  5. 亞像素位置 = idx + 偏移
輸出：邊緣位置、計算耗時
```

#### 5.2 高斯擬合 (Gaussian Fit)

```
輸入：梯度陣列（取絕對值）
步驟：
  1. 使用 MathNet.Numerics 的 Fit 或自行實作 Levenberg-Marquardt
  2. 模型：y = A × exp(-(x-μ)² / (2σ²))
  3. 初始值：A=峰值, μ=峰值位置, σ=1.0
輸出：μ 為邊緣位置、R² 擬合品質、計算耗時
```

#### 5.3 矩法 (Moment Method)

```
輸入：梯度陣列（取絕對值）
步驟：
  1. 閾值 = max × 0.3
  2. 低於閾值的設為 0
  3. 位置 = Σ(i × g[i]) / Σ(g[i])
輸出：質心位置、計算耗時
```

#### 5.4 Sigmoid 擬合 (Error Function Fit)

```
輸入：灰階陣列（原始值）
步驟：
  1. 模型：y = A + B × (1 + erf((x - x₀) / (σ√2)))
  2. 使用非線性最小平方法擬合
  3. 初始值：x₀=中點, A=min, B=(max-min)/2, σ=1.0
輸出：x₀ 為邊緣位置、R² 擬合品質、計算耗時
```

### 6. 結果顯示

#### 6.1 灰階剖面圖（右上）

- X 軸：取樣點索引
- Y 軸：灰階值 (0-255)
- 單一曲線顯示剖面

#### 6.2 梯度剖面圖（右中）

- X 軸：取樣點索引
- Y 軸：梯度值
- 顯示梯度曲線
- 四條垂直線標記各方法檢測到的邊緣位置
- 圖例標示顏色對應

```
顏色配置：
- Parabolic：藍色
- Gaussian：綠色
- Moment：橘色
- Sigmoid：紅色
```

#### 6.3 結果表格（右下）

DataGridView 欄位：

| 方法 | 位置 (px) | 誤差 (px) | 耗時 (ms) | 品質 |
|------|-----------|-----------|-----------|------|
| Parabolic | 100.372 | +0.372 | 0.02 | - |
| Gaussian | 100.158 | +0.158 | 0.35 | R²=0.994 |
| Moment | 100.401 | +0.401 | 0.01 | - |
| Sigmoid | 100.061 | +0.061 | 1.20 | R²=0.998 |

- 「誤差」欄位：僅在使用合成測試影像時顯示（已知真實位置）
- 真實影像時該欄位顯示 "-"

### 7. 匯出結果

- 工具列按鈕「匯出結果」
- 匯出內容：
  - 結果表格 (CSV)
  - 圖表截圖 (PNG)
  - 分析報告 (可選，含影像縮圖與參數)

---

## 專案結構

```
SubpixelEdgeComparator/
├── SubpixelEdgeComparator.sln
├── SubpixelEdgeComparator/
│   ├── Program.cs
│   ├── MainForm.cs                 # 主視窗
│   ├── MainForm.Designer.cs
│   ├── Dialogs/
│   │   └── SyntheticImageDialog.cs # 合成影像設定對話框
│   │
│   ├── Core/
│   │   ├── ImageProcessor.cs       # 影像載入、剖面提取、梯度計算
│   │   ├── SubpixelMethods.cs      # 四種亞像素演算法
│   │   └── AnalysisResult.cs       # 結果資料結構
│   │
│   ├── Controls/
│   │   └── ImageCanvas.cs          # 自訂控件：影像顯示 + ROI 繪製
│   │
│   └── Utils/
│       ├── MathHelper.cs           # 數學輔助（擬合、erf 等）
│       └── ExportHelper.cs         # 匯出功能
```

---

## 類別設計

### AnalysisResult.cs

```csharp
public class AnalysisResult
{
    public string MethodName { get; set; }
    public double? Position { get; set; }      // null 表示失敗
    public double? Error { get; set; }         // 與真實位置的誤差
    public double ElapsedMs { get; set; }
    public double? RSquared { get; set; }      // 擬合品質
    public bool Success { get; set; }
}
```

### SubpixelMethods.cs

```csharp
public static class SubpixelMethods
{
    public static AnalysisResult ParabolicFit(double[] gradient);
    public static AnalysisResult GaussianFit(double[] gradient);
    public static AnalysisResult MomentMethod(double[] gradient);
    public static AnalysisResult SigmoidFit(double[] intensity);
}
```

### ImageProcessor.cs

```csharp
public class ImageProcessor
{
    public Mat LoadImage(string path);
    public Mat GenerateSyntheticImage(SyntheticImageParams param);
    public double[] ExtractProfile(Mat image, Point p1, Point p2, int supersample = 2);
    public double[] ComputeGradient(double[] profile);
}
```

---

## 操作流程

```
1. 啟動程式
       ↓
2. [載入影像] 或 [合成測試影像]
       ↓
3. 在影像上拖曳畫出 ROI 線段
       ↓
4. 點擊 [執行分析]
       ↓
5. 查看右側圖表與結果表格
       ↓
6. (可選) 調整 ROI 重新分析
       ↓
7. (可選) [匯出結果]
```

---

## 錯誤處理

| 情況 | 處理方式 |
|------|----------|
| 未載入影像就執行分析 | 彈出提示「請先載入影像」 |
| 未選取 ROI 就執行分析 | 彈出提示「請先選取 ROI 線段」 |
| ROI 線段太短 (< 10px) | 彈出提示「線段太短，請重新選取」 |
| 擬合不收斂 | 該方法結果標示 "FAILED"，不中斷其他方法 |
| 影像檔案損壞 | 彈出錯誤訊息，不載入 |

---

## 快捷鍵

| 快捷鍵 | 功能 |
|--------|------|
| Ctrl+O | 開啟影像 |
| Ctrl+G | 合成測試影像 |
| Ctrl+R | 執行分析 |
| Ctrl+E | 匯出結果 |
| Delete | 清除 ROI |
| Ctrl+滾輪 | 縮放影像 |

---

## 未來擴充（Phase 2）

1. **多線段分析**：同時選取多條 ROI，統計標準差
2. **批次處理**：載入整個資料夾，自動分析並匯出報表
3. **ROI 模板**：儲存/載入 ROI 設定，方便重複使用
4. **演算法參數調整**：讓使用者微調各演算法的參數（閾值、初始值等）

---

## License

MIT License
