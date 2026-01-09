using System.Text;
using VisionMeasurementToolkit.Core;

namespace VisionMeasurementToolkit.Utils;

/// <summary>
/// 匯出功能輔助類別
/// </summary>
public static class ExportHelper
{
    /// <summary>
    /// 匯出結果為 CSV
    /// </summary>
    public static void ExportToCsv(string filePath, IEnumerable<MeasurementResult> results, CalibrationManager calibration)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 影像量測結果匯出");
        sb.AppendLine($"# 匯出時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# 解析度：{calibration.GetResolutionString()}");
        sb.AppendLine();

        sb.AppendLine("編號,類型,名稱,摘要,時間");

        int idx = 1;
        foreach (var result in results)
        {
            sb.AppendLine($"{idx},{result.ResultType},{result.Name},\"{result.GetSummary(calibration.Resolution)}\",{result.Timestamp:HH:mm:ss}");
            idx++;
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
    public static void ExportHtmlReport(string filePath, IEnumerable<MeasurementResult> results,
        CalibrationManager calibration, string? imageBase64 = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"zh-TW\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <title>影像量測報告</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: 'Microsoft JhengHei', sans-serif; margin: 40px; background: #f5f5f5; }");
        sb.AppendLine("    .container { background: white; padding: 30px; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }");
        sb.AppendLine("    h1 { color: #2196F3; border-bottom: 3px solid #2196F3; padding-bottom: 10px; }");
        sb.AppendLine("    h2 { color: #333; margin-top: 30px; }");
        sb.AppendLine("    table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        sb.AppendLine("    th, td { border: 1px solid #ddd; padding: 12px; text-align: left; }");
        sb.AppendLine("    th { background-color: #2196F3; color: white; }");
        sb.AppendLine("    tr:nth-child(even) { background-color: #f9f9f9; }");
        sb.AppendLine("    tr:hover { background-color: #e3f2fd; }");
        sb.AppendLine("    .info { color: #666; font-size: 14px; margin-bottom: 20px; }");
        sb.AppendLine("    .type-圓 { color: #4CAF50; font-weight: bold; }");
        sb.AppendLine("    .type-線 { color: #2196F3; font-weight: bold; }");
        sb.AppendLine("    .type-距離 { color: #FF9800; font-weight: bold; }");
        sb.AppendLine("    .type-角度 { color: #9C27B0; font-weight: bold; }");
        sb.AppendLine("    img { max-width: 100%; margin: 20px 0; border: 1px solid #ddd; border-radius: 4px; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        sb.AppendLine("  <h1>影像量測報告</h1>");

        sb.AppendLine("  <p class=\"info\">");
        sb.AppendLine($"    匯出時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}<br>");
        sb.AppendLine($"    解析度：{calibration.GetResolutionString()}<br>");
        sb.AppendLine($"    校正方式：{calibration.CalibrationMethod}");
        sb.AppendLine("  </p>");

        if (!string.IsNullOrEmpty(imageBase64))
        {
            sb.AppendLine("  <h2>量測影像</h2>");
            sb.AppendLine($"  <img src=\"data:image/png;base64,{imageBase64}\" alt=\"量測影像\">");
        }

        sb.AppendLine("  <h2>量測結果</h2>");
        sb.AppendLine("  <table>");
        sb.AppendLine("    <tr><th>#</th><th>類型</th><th>名稱</th><th>結果摘要</th><th>時間</th></tr>");

        int idx = 1;
        foreach (var result in results)
        {
            sb.AppendLine($"    <tr>");
            sb.AppendLine($"      <td>{idx}</td>");
            sb.AppendLine($"      <td class=\"type-{result.ResultType}\">{result.ResultType}</td>");
            sb.AppendLine($"      <td>{result.Name}</td>");
            sb.AppendLine($"      <td>{result.GetSummary(calibration.Resolution)}</td>");
            sb.AppendLine($"      <td>{result.Timestamp:HH:mm:ss}</td>");
            sb.AppendLine($"    </tr>");
            idx++;
        }

        sb.AppendLine("  </table>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }
}
