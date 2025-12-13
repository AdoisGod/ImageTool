using System.Text;
using SubpixelEdgeComparator.Core;

namespace SubpixelEdgeComparator.Utils;

/// <summary>
/// 匯出功能輔助類別
/// </summary>
public static class ExportHelper
{
    /// <summary>
    /// 匯出結果為 CSV
    /// </summary>
    public static void ExportToCsv(string filePath, FullAnalysisResult result, string? imagePath = null)
    {
        var sb = new StringBuilder();

        // 標題資訊
        sb.AppendLine("# 亞像素邊緣檢測分析結果");
        sb.AppendLine($"# 匯出時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrEmpty(imagePath))
        {
            sb.AppendLine($"# 影像路徑：{imagePath}");
        }
        if (result.TrueEdgePosition.HasValue)
        {
            sb.AppendLine($"# 真實邊緣位置：{result.TrueEdgePosition.Value:F3}");
        }
        sb.AppendLine();

        // 表頭
        sb.AppendLine("方法,位置 (px),誤差 (px),耗時 (ms),品質 (R²),狀態");

        // 資料
        foreach (var r in result.Results)
        {
            string position = r.Position.HasValue ? r.Position.Value.ToString("F3") : "-";
            string error = r.Error.HasValue ? (r.Error.Value >= 0 ? "+" : "") + r.Error.Value.ToString("F3") : "-";
            string elapsed = r.ElapsedMs.ToString("F2");
            string quality = r.RSquared.HasValue ? $"R²={r.RSquared.Value:F3}" : "-";
            string status = r.Success ? "成功" : $"失敗: {r.ErrorMessage}";

            sb.AppendLine($"{r.MethodName},{position},{error},{elapsed},{quality},{status}");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 匯出灰階剖面資料為 CSV
    /// </summary>
    public static void ExportProfileToCsv(string filePath, double[] grayProfile, double[] gradientProfile)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Index,Gray Value,Gradient");

        int maxLen = Math.Max(grayProfile.Length, gradientProfile.Length);
        for (int i = 0; i < maxLen; i++)
        {
            string gray = i < grayProfile.Length ? grayProfile[i].ToString("F2") : "";
            string grad = i < gradientProfile.Length ? gradientProfile[i].ToString("F4") : "";
            sb.AppendLine($"{i},{gray},{grad}");
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// 儲存控件截圖
    /// </summary>
    public static void SaveControlImage(Control control, string filePath)
    {
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(0, 0, control.Width, control.Height));
        bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
    }

    /// <summary>
    /// 產生 HTML 報告
    /// </summary>
    public static void ExportHtmlReport(string filePath, FullAnalysisResult result,
        string? chartImagePath = null, string? imagePath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-TW\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <title>亞像素邊緣檢測分析報告</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: 'Microsoft JhengHei', sans-serif; margin: 40px; }");
        sb.AppendLine("    h1 { color: #333; border-bottom: 2px solid #4CAF50; padding-bottom: 10px; }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        sb.AppendLine("    th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
        sb.AppendLine("    th { background-color: #4CAF50; color: white; }");
        sb.AppendLine("    tr:nth-child(even) { background-color: #f9f9f9; }");
        sb.AppendLine("    .success { color: green; }");
        sb.AppendLine("    .failed { color: red; }");
        sb.AppendLine("    .info { color: #666; font-size: 14px; }");
        sb.AppendLine("    img { max-width: 100%; margin: 20px 0; border: 1px solid #ddd; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("  <h1>亞像素邊緣檢測分析報告</h1>");

        sb.AppendLine("  <p class=\"info\">");
        sb.AppendLine($"    匯出時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}<br>");
        if (!string.IsNullOrEmpty(imagePath))
        {
            sb.AppendLine($"    影像路徑：{imagePath}<br>");
        }
        if (result.TrueEdgePosition.HasValue)
        {
            sb.AppendLine($"    真實邊緣位置：{result.TrueEdgePosition.Value:F3} px");
        }
        sb.AppendLine("  </p>");

        // 結果表格
        sb.AppendLine("  <h2>分析結果</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <tr><th>方法</th><th>位置 (px)</th><th>誤差 (px)</th><th>耗時 (ms)</th><th>品質</th><th>狀態</th></tr>");

        foreach (var r in result.Results)
        {
            string position = r.Position.HasValue ? r.Position.Value.ToString("F3") : "-";
            string error = r.Error.HasValue ? (r.Error.Value >= 0 ? "+" : "") + r.Error.Value.ToString("F3") : "-";
            string elapsed = r.ElapsedMs.ToString("F2");
            string quality = r.RSquared.HasValue ? $"R²={r.RSquared.Value:F3}" : "-";
            string statusClass = r.Success ? "success" : "failed";
            string status = r.Success ? "成功" : $"失敗: {r.ErrorMessage}";

            sb.AppendLine($"    <tr><td>{r.MethodName}</td><td>{position}</td><td>{error}</td><td>{elapsed}</td><td>{quality}</td><td class=\"{statusClass}\">{status}</td></tr>");
        }

        sb.AppendLine("  </table>");

        // 圖表
        if (!string.IsNullOrEmpty(chartImagePath) && File.Exists(chartImagePath))
        {
            sb.AppendLine("  <h2>分析圖表</h2>");
            byte[] imageBytes = File.ReadAllBytes(chartImagePath);
            string base64 = Convert.ToBase64String(imageBytes);
            sb.AppendLine($"  <img src=\"data:image/png;base64,{base64}\" alt=\"分析圖表\">");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}
