using System.Diagnostics;
using SubpixelEdgeComparator.Utils;

namespace SubpixelEdgeComparator.Core;

/// <summary>
/// 四種亞像素邊緣檢測演算法實作
/// </summary>
public static class SubpixelMethods
{
    /// <summary>
    /// 拋物線擬合法 (Parabolic Fit)
    /// 使用梯度峰值附近三點進行拋物線擬合
    /// </summary>
    public static AnalysisResult ParabolicFit(double[] gradient)
    {
        var sw = Stopwatch.StartNew();
        var result = new AnalysisResult { MethodName = "Parabolic" };

        try
        {
            if (gradient.Length < 3)
            {
                result.Success = false;
                result.ErrorMessage = "梯度陣列長度不足";
                return result;
            }

            // 取梯度絕對值
            double[] absGradient = gradient.Select(Math.Abs).ToArray();

            // 找到最大值位置
            int maxIdx = 0;
            double maxVal = absGradient[0];
            for (int i = 1; i < absGradient.Length; i++)
            {
                if (absGradient[i] > maxVal)
                {
                    maxVal = absGradient[i];
                    maxIdx = i;
                }
            }

            // 確保有足夠的鄰近點
            if (maxIdx == 0 || maxIdx == absGradient.Length - 1)
            {
                // 邊緣情況，直接返回整數位置
                result.Position = maxIdx;
                result.Success = true;
                sw.Stop();
                result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
                return result;
            }

            // 取三點進行拋物線擬合
            double y0 = absGradient[maxIdx - 1];
            double y1 = absGradient[maxIdx];
            double y2 = absGradient[maxIdx + 1];

            var (offset, success) = MathHelper.ParabolicPeakFit(y0, y1, y2);

            if (success)
            {
                result.Position = maxIdx + offset;
                result.Success = true;
            }
            else
            {
                // 擬合失敗，使用整數位置
                result.Position = maxIdx;
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// 高斯擬合法 (Gaussian Fit)
    /// 將梯度峰值擬合為高斯曲線
    /// </summary>
    public static AnalysisResult GaussianFit(double[] gradient)
    {
        var sw = Stopwatch.StartNew();
        var result = new AnalysisResult { MethodName = "Gaussian" };

        try
        {
            if (gradient.Length < 5)
            {
                result.Success = false;
                result.ErrorMessage = "梯度陣列長度不足";
                return result;
            }

            // 取梯度絕對值
            double[] absGradient = gradient.Select(Math.Abs).ToArray();

            // 找到峰值位置並提取擬合區域
            int maxIdx = 0;
            double maxVal = absGradient[0];
            for (int i = 1; i < absGradient.Length; i++)
            {
                if (absGradient[i] > maxVal)
                {
                    maxVal = absGradient[i];
                    maxIdx = i;
                }
            }

            // 取峰值附近的點進行擬合（前後各取一定範圍）
            int halfWindow = Math.Min(10, Math.Min(maxIdx, absGradient.Length - 1 - maxIdx));
            int start = maxIdx - halfWindow;
            int end = maxIdx + halfWindow;

            double[] x = new double[end - start + 1];
            double[] y = new double[end - start + 1];
            for (int i = start; i <= end; i++)
            {
                x[i - start] = i;
                y[i - start] = absGradient[i];
            }

            // 進行高斯擬合
            var (amplitude, mean, sigma, rSquared, success) = MathHelper.FitGaussian(x, y);

            if (success && mean >= 0 && mean < gradient.Length)
            {
                result.Position = mean;
                result.RSquared = rSquared;
                result.Success = true;
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "高斯擬合失敗或結果超出範圍";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// 矩法 (Moment Method / 質心法)
    /// 使用加權質心計算邊緣位置
    /// </summary>
    public static AnalysisResult MomentMethod(double[] gradient)
    {
        var sw = Stopwatch.StartNew();
        var result = new AnalysisResult { MethodName = "Moment" };

        try
        {
            if (gradient.Length < 3)
            {
                result.Success = false;
                result.ErrorMessage = "梯度陣列長度不足";
                return result;
            }

            // 取梯度絕對值
            double[] absGradient = gradient.Select(Math.Abs).ToArray();

            // 閾值處理：低於最大值 30% 的設為 0
            double maxVal = absGradient.Max();
            double threshold = maxVal * 0.3;

            double[] thresholded = absGradient.Select(g => g >= threshold ? g : 0).ToArray();

            // 計算質心位置
            double sumWeighted = 0;
            double sumWeight = 0;

            for (int i = 0; i < thresholded.Length; i++)
            {
                sumWeighted += i * thresholded[i];
                sumWeight += thresholded[i];
            }

            if (sumWeight < 1e-10)
            {
                result.Success = false;
                result.ErrorMessage = "權重總和為零";
                return result;
            }

            result.Position = sumWeighted / sumWeight;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// Sigmoid 擬合法 (Error Function Fit)
    /// 直接對灰階剖面進行 Sigmoid 擬合
    /// </summary>
    public static AnalysisResult SigmoidFit(double[] intensity)
    {
        var sw = Stopwatch.StartNew();
        var result = new AnalysisResult { MethodName = "Sigmoid" };

        try
        {
            if (intensity.Length < 5)
            {
                result.Success = false;
                result.ErrorMessage = "灰階陣列長度不足";
                return result;
            }

            // 建立 x 座標
            double[] x = new double[intensity.Length];
            for (int i = 0; i < intensity.Length; i++)
            {
                x[i] = i;
            }

            // 進行 Sigmoid 擬合
            var (a, b, x0, sigma, rSquared, success) = MathHelper.FitSigmoid(x, intensity);

            if (success && x0 >= 0 && x0 < intensity.Length)
            {
                result.Position = x0;
                result.RSquared = rSquared;
                result.Success = true;
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "Sigmoid 擬合失敗或結果超出範圍";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }
}
