# Vision Measurement Toolkit

影像量測工具集（C# WinForms 版）

## 專案目標

開發一個具有圖形化操作介面的 Windows 桌面應用程式，提供常用的影像量測功能，包含圓形檢測、直線檢測、距離量測、像素解析度校正等，協助使用者快速進行影像尺寸分析。

## 技術堆疊

- .NET 8.0 (Windows)
- WinForms（圖形介面）
- OpenCvSharp4（影像處理）
- MathNet.Numerics（數值計算、幾何擬合）
- OxyPlot.WindowsForms（圖表繪製，選用）

## NuGet 套件

```
OpenCvSharp4
OpenCvSharp4.runtime.win
MathNet.Numerics
```

---

## 介面佈局

主視窗採用三欄式佈局：

```
┌─────────────────────────────────────────────────────────────────────────┐
│  選單列：[檔案] [工具] [校正] [說明]                                       │
├───────────┬─────────────────────────────────────┬───────────────────────┤
│           │                                     │                       │
│  工具面板  │                                     │  結果面板              │
│           │                                     │                       │
│ ┌───────┐ │                                     │  ┌─────────────────┐  │
│ │找圓    │ │                                     │  │ 量測結果列表     │  │
│ └───────┘ │                                     │  │ (DataGridView)  │  │
│ ┌───────┐ │        影像顯示區                    │  │                 │  │
│ │找線    │ │        (ImageCanvas)                │  │ - 名稱          │  │
│ └───────┘ │                                     │  │ - 數值 (px)     │  │
│ ┌───────┐ │        - 顯示影像                    │  │ - 數值 (mm)     │  │
│ │點到點  │ │        - 顯示量測圖形                │  │                 │  │
│ └───────┘ │        - 滑鼠互動操作                │  └─────────────────┘  │
│ ┌───────┐ │                                     │                       │
│ │線到線  │ │                                     │  ┌─────────────────┐  │
│ └───────┘ │                                     │  │ 詳細資訊         │  │
│ ┌───────┐ │                                     │  │ (PropertyGrid   │  │
│ │角度    │ │                                     │  │  或 TextBox)    │  │
│ └───────┘ │                                     │  │                 │  │
│ ┌───────┐ │                                     │  │ 顯示選取物件的   │  │
│ │校正    │ │                                     │  │ 完整參數        │  │
│ └───────┘ │                                     │  └─────────────────┘  │
│           │                                     │                       │
├───────────┴─────────────────────────────────────┴───────────────────────┤
│  狀態列：[目前工具] [滑鼠座標] [像素解析度] [影像資訊]                      │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 功能規格

### 1. 影像載入

- 選單 [檔案] → [開啟] 或快捷鍵 Ctrl+O
- 支援格式：PNG、JPG、BMP、TIFF
- 載入後自動轉灰階（保留原圖供顯示）
- 支援滑鼠滾輪縮放、拖曳平移
- 支援拖放檔案載入

### 2. 像素解析度校正

在進行實際尺寸量測前，需先設定像素解析度。

#### 2.1 手動輸入

選單 [校正] → [設定解析度]：

```
┌─ 像素解析度設定 ───────────────────┐
│                                   │
│  解析度：[0.05    ] mm/pixel      │
│                                   │
│  或輸入：[20.00   ] pixel/mm      │
│                                   │
│        [確定]    [取消]           │
└───────────────────────────────────┘
```

#### 2.2 使用標準圓校正

選單 [校正] → [使用標準圓校正]：

1. 使用「找圓」工具檢測標準件的圓
2. 彈出對話框：

```
┌─ 標準圓校正 ───────────────────────┐
│                                   │
│  檢測到的圓直徑：[523.45] pixels   │
│                                   │
│  請輸入實際直徑：[25.00 ] mm       │
│                                   │
│  計算解析度：0.0478 mm/pixel      │
│                                   │
│        [套用]    [取消]           │
└───────────────────────────────────┘
```

#### 2.3 使用標準距離校正

選單 [校正] → [使用標準距離校正]：

1. 使用「點到點」工具量測標準件的已知距離
2. 輸入實際距離，自動計算解析度

---

## 量測工具

### 3. 找圓工具

#### 3.1 操作方式

- 點擊工具面板「找圓」按鈕
- 在影像上框選 ROI 區域（矩形）
- 系統自動在 ROI 內檢測圓形

#### 3.2 檢測方法

提供兩種方法，使用者可在工具選項中切換：

**方法 A：霍夫圓檢測**
```
使用 OpenCV HoughCircles
參數可調：
- dp：累加器解析度比例（預設 1.0）
- minDist：圓心最小距離（預設 ROI高度/8）
- param1：Canny 高閾值（預設 100）
- param2：累加器閾值（預設 30）
- minRadius：最小半徑（預設 0）
- maxRadius：最大半徑（預設 0，自動）
```

**方法 B：邊緣擬合**
```
步驟：
1. Canny 邊緣檢測
2. 提取邊緣點
3. 使用最小平方法擬合圓方程式 (x-a)² + (y-b)² = r²
4. 可選：RANSAC 排除離群點
```

#### 3.3 輸出結果

```
圓心 X：256.372 px（12.82 mm）
圓心 Y：198.156 px（9.91 mm）
半徑：  104.523 px（5.23 mm）
直徑：  209.046 px（10.45 mm）
擬合品質：R² = 0.998（僅邊緣擬合法）
```

#### 3.4 視覺化

- 在影像上繪製檢測到的圓（綠色）
- 標記圓心（十字）
- 顯示直徑標註線

---

### 4. 找線工具

#### 4.1 操作方式

- 點擊工具面板「找線」按鈕
- 在影像上畫一條搜尋線段（定義搜尋方向）
- 系統沿搜尋線垂直方向檢測邊緣，擬合直線

#### 4.2 檢測方法

**卡尺法（Caliper）**
```
步驟：
1. 沿使用者畫的線段，等距取 N 個採樣點（預設 10）
2. 在每個採樣點，沿垂直方向取灰階剖面
3. 對每個剖面進行亞像素邊緣檢測
4. 收集所有邊緣點
5. 最小平方法擬合直線（可選 RANSAC）

參數可調：
- 採樣點數量（5-50）
- 剖面長度（pixels）
- 邊緣極性（亮到暗/暗到亮/兩者）
- 邊緣閾值
```

#### 4.3 輸出結果

```
直線方程式：y = 0.0523x + 45.234
角度：3.00°
起點：(50.00, 47.85) px
終點：(450.00, 68.78) px
長度：401.09 px（20.05 mm）
擬合品質：R² = 0.996
```

#### 4.4 視覺化

- 顯示擬合的直線（藍色）
- 顯示各採樣點的檢測位置（小圓點）
- 顯示搜尋區域（半透明）

---

### 5. 點到點距離

#### 5.1 操作方式

- 點擊工具面板「點到點」按鈕
- 在影像上點擊第一點
- 點擊第二點
- 自動計算並顯示距離

#### 5.2 輸出結果

```
點 1：(125.50, 200.25) px
點 2：(380.75, 450.50) px
水平距離：255.25 px（12.76 mm）
垂直距離：250.25 px（12.51 mm）
直線距離：357.58 px（17.88 mm）
角度：44.43°
```

#### 5.3 視覺化

- 顯示兩點（紅色圓點）
- 連接線段
- 標註距離數值

---

### 6. 線到線距離

#### 6.1 操作方式

- 點擊工具面板「線到線」按鈕
- 先使用「找線」工具檢測兩條直線
- 或在結果列表中選取兩條已檢測的直線
- 點擊「計算距離」

#### 6.2 計算方式

**平行線**：計算垂直距離
**非平行線**：計算交點，並提示線不平行

```
若兩線夾角 < 5°，視為平行，計算垂直距離
否則計算交點座標，並顯示夾角
```

#### 6.3 輸出結果

```
線 1 與線 2：
狀態：平行（夾角 0.52°）
垂直距離：85.234 px（4.26 mm）
```

或

```
線 1 與線 2：
狀態：相交（夾角 45.23°）
交點：(256.45, 312.67) px
```

---

### 7. 角度量測

#### 7.1 操作方式

**方式 A：選取兩條線**
- 在結果列表中選取兩條已檢測的直線
- 自動計算夾角

**方式 B：手動三點定義**
- 點擊「角度」工具
- 依序點擊三點（端點-頂點-端點）
- 計算夾角

#### 7.2 輸出結果

```
夾角：45.23°
補角：134.77°
```

---

### 8. 圓到圓距離（進階）

#### 8.1 操作方式

- 選取兩個已檢測的圓
- 計算圓心距離或邊緣最短距離

#### 8.2 輸出結果

```
圓 1 圓心：(150.25, 200.50) px
圓 2 圓心：(400.75, 350.25) px
圓心距離：295.43 px（14.77 mm）
邊緣最短距離：86.19 px（4.31 mm）
邊緣最長距離：504.67 px（25.23 mm）
```

---

## 結果管理

### 9. 結果列表

- 每次量測結果自動加入列表
- 可選取、刪除、重新命名
- 選取時在影像上高亮顯示對應圖形
- 支援多選進行距離/角度計算

### 10. 匯出功能

選單 [檔案] → [匯出結果]：

- **CSV**：所有量測數據
- **影像截圖**：含標註的影像（PNG）
- **報告**：完整報告含影像與數據（可選 PDF/HTML）

---

## 專案結構

```
VisionMeasurementToolkit/
├── VisionMeasurementToolkit.sln
├── VisionMeasurementToolkit/
│   ├── Program.cs
│   ├── MainForm.cs
│   ├── MainForm.Designer.cs
│   │
│   ├── Dialogs/
│   │   ├── ResolutionDialog.cs         # 解析度設定
│   │   ├── CircleCalibrationDialog.cs  # 圓形校正
│   │   └── ToolOptionsDialog.cs        # 工具參數設定
│   │
│   ├── Core/
│   │   ├── ImageManager.cs             # 影像載入與管理
│   │   ├── CalibrationManager.cs       # 校正管理
│   │   ├── MeasurementResult.cs        # 量測結果基底類別
│   │   └── GeometryMath.cs             # 幾何計算
│   │
│   ├── Tools/
│   │   ├── ITool.cs                    # 工具介面
│   │   ├── CircleFinder.cs             # 找圓工具
│   │   ├── LineFinder.cs               # 找線工具
│   │   ├── PointToPointTool.cs         # 點到點量測
│   │   ├── LineToLineTool.cs           # 線到線量測
│   │   └── AngleTool.cs                # 角度量測
│   │
│   ├── Detection/
│   │   ├── HoughCircleDetector.cs      # 霍夫圓檢測
│   │   ├── CircleFitter.cs             # 圓擬合
│   │   ├── CaliperEdgeDetector.cs      # 卡尺邊緣檢測
│   │   ├── LineFitter.cs               # 直線擬合
│   │   └── SubpixelEdge.cs             # 亞像素邊緣（共用）
│   │
│   ├── Controls/
│   │   ├── ImageCanvas.cs              # 影像顯示控件
│   │   ├── ToolPanel.cs                # 工具選擇面板
│   │   └── ResultPanel.cs              # 結果顯示面板
│   │
│   ├── Graphics/
│   │   ├── Overlay.cs                  # 繪圖疊加層
│   │   ├── CircleGraphic.cs            # 圓形繪製
│   │   ├── LineGraphic.cs              # 直線繪製
│   │   └── AnnotationGraphic.cs        # 標註繪製
│   │
│   └── Utils/
│       ├── MathHelper.cs               # 數學輔助
│       ├── ExportHelper.cs             # 匯出功能
│       └── Settings.cs                 # 應用程式設定
```

---

## 類別設計

### MeasurementResult.cs（基底類別）

```csharp
public abstract class MeasurementResult
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime Timestamp { get; set; }
    public abstract string ResultType { get; }
    public abstract string GetSummary(double resolution);
}
```

### CircleResult.cs

```csharp
public class CircleResult : MeasurementResult
{
    public PointF Center { get; set; }
    public double RadiusPixels { get; set; }
    public double? RSquared { get; set; }
    
    public double DiameterPixels => RadiusPixels * 2;
    public double RadiusMm(double res) => RadiusPixels * res;
    public double DiameterMm(double res) => DiameterPixels * res;
}
```

### LineResult.cs

```csharp
public class LineResult : MeasurementResult
{
    public PointF StartPoint { get; set; }
    public PointF EndPoint { get; set; }
    public double Slope { get; set; }
    public double Intercept { get; set; }
    public double AngleDegrees { get; set; }
    public double LengthPixels { get; set; }
    public double? RSquared { get; set; }
}
```

### CalibrationManager.cs

```csharp
public class CalibrationManager
{
    public double Resolution { get; set; } = 1.0;  // mm/pixel
    public bool IsCalibrated { get; set; } = false;
    
    public void SetResolution(double mmPerPixel);
    public void CalibrateWithCircle(double measuredDiameterPx, double actualDiameterMm);
    public void CalibrateWithDistance(double measuredDistancePx, double actualDistanceMm);
    
    public double PixelsToMm(double pixels) => pixels * Resolution;
    public double MmToPixels(double mm) => mm / Resolution;
}
```

### ITool.cs

```csharp
public interface ITool
{
    string Name { get; }
    Cursor ToolCursor { get; }
    
    void OnMouseDown(MouseEventArgs e, PointF imagePoint);
    void OnMouseMove(MouseEventArgs e, PointF imagePoint);
    void OnMouseUp(MouseEventArgs e, PointF imagePoint);
    void OnPaint(Graphics g, Matrix transform);
    
    event EventHandler<MeasurementResult> MeasurementCompleted;
}
```

---

## 操作流程

```
1. 啟動程式
       ↓
2. [載入影像]
       ↓
3. [校正] 設定像素解析度（可選，預設 1 px = 1 單位）
       ↓
4. 選擇量測工具
       ↓
5. 在影像上操作（框選/點擊/拖曳）
       ↓
6. 查看結果面板
       ↓
7. 重複步驟 4-6 進行更多量測
       ↓
8. [匯出結果]
```

---

## 錯誤處理

| 情況 | 處理方式 |
|------|----------|
| 未載入影像就使用工具 | 提示「請先載入影像」 |
| ROI 內找不到圓 | 提示「未檢測到圓形，請調整參數或重新選取」 |
| 找線採樣點不足 | 提示「有效邊緣點不足，無法擬合直線」 |
| 選取的兩條線無法計算距離 | 提示具體原因 |
| 解析度為 0 或負值 | 阻止輸入，提示錯誤 |

---

## 快捷鍵

| 快捷鍵 | 功能 |
|--------|------|
| Ctrl+O | 開啟影像 |
| Ctrl+S | 儲存結果 |
| Ctrl+E | 匯出 |
| 1 | 找圓工具 |
| 2 | 找線工具 |
| 3 | 點到點工具 |
| 4 | 線到線工具 |
| 5 | 角度工具 |
| Delete | 刪除選取的結果 |
| Escape | 取消目前操作 |
| Ctrl+滾輪 | 縮放影像 |
| 空白鍵+拖曳 | 平移影像 |

---

## 工具選項面板

每個工具有對應的參數設定，顯示在工具面板下方或彈出視窗：

### 找圓選項
```
┌─ 找圓選項 ─────────────────┐
│ 方法：[● 霍夫 ○ 邊緣擬合]  │
│ 最小半徑：[10   ] px       │
│ 最大半徑：[500  ] px       │
│ 靈敏度：  [====●===] 30    │
│ □ 使用 RANSAC              │
└────────────────────────────┘
```

### 找線選項
```
┌─ 找線選項 ─────────────────┐
│ 採樣點數：[10   ]          │
│ 剖面長度：[50   ] px       │
│ 邊緣極性：[亮→暗 ▼]        │
│ 邊緣閾值：[====●===] 30    │
│ □ 使用 RANSAC              │
└────────────────────────────┘
```

---

## 未來擴充（Phase 2）

1. **矩形檢測**：找矩形、量測長寬
2. **橢圓檢測**：找橢圓、量測長短軸
3. **輪廓分析**：周長、面積、圓度
4. **批次量測**：對多張影像自動執行相同量測
5. **ROI 模板**：儲存/載入 ROI 設定
6. **座標系定義**：設定原點與軸向
7. **公差判定**：設定上下限，自動判定 OK/NG

---

## License

MIT License
