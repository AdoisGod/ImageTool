using System.Diagnostics;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace VisionMeasurementToolkit.Core;

/// <summary>
/// 亞像素邊緣檢測演算法
/// </summary>
public static class SubpixelMethods
{
    /// <summary>
    /// 單一演算法結果
    /// </summary>
    public class MethodResult
    {
        public string MethodName { get; set; } = "";
        public double? Position { get; set; }
        public double? Error { get; set; }
        public double ElapsedMs { get; set; }
        public double? RSquared { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// 拋物線擬合法
    /// </summary>
    public static MethodResult ParabolicFit(double[] gradient)
    {
        var sw = Stopwatch.StartNew();
        var result = new MethodResult { MethodName = "Parabolic" };

        try
        {
            if (gradient.Length < 3) { result.Success = false; return result; }

            double[] absGradient = gradient.Select(Math.Abs).ToArray();
            int maxIdx = 0;
            double maxVal = absGradient[0];
            for (int i = 1; i < absGradient.Length; i++)
            {
                if (absGradient[i] > maxVal) { maxVal = absGradient[i]; maxIdx = i; }
            }

            if (maxIdx == 0 || maxIdx == absGradient.Length - 1)
            {
                result.Position = maxIdx;
                result.Success = true;
            }
            else
            {
                double y0 = absGradient[maxIdx - 1];
                double y1 = absGradient[maxIdx];
                double y2 = absGradient[maxIdx + 1];

                double a = (y0 + y2 - 2 * y1) / 2.0;
                double b = (y2 - y0) / 2.0;

                if (Math.Abs(a) > 1e-10)
                {
                    double offset = -b / (2 * a);
                    if (offset >= -1 && offset <= 1)
                    {
                        result.Position = maxIdx + offset;
                        result.Success = true;
                    }
                    else
                    {
                        result.Position = maxIdx;
                        result.Success = true;
                    }
                }
                else
                {
                    result.Position = maxIdx;
                    result.Success = true;
                }
            }
        }
        catch { result.Success = false; }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// 高斯擬合法
    /// </summary>
    public static MethodResult GaussianFit(double[] gradient)
    {
        var sw = Stopwatch.StartNew();
        var result = new MethodResult { MethodName = "Gaussian" };

        try
        {
            if (gradient.Length < 5) { result.Success = false; return result; }

            double[] absGradient = gradient.Select(Math.Abs).ToArray();
            int maxIdx = 0;
            double maxVal = absGradient[0];
            for (int i = 1; i < absGradient.Length; i++)
            {
                if (absGradient[i] > maxVal) { maxVal = absGradient[i]; maxIdx = i; }
            }

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

            var (amplitude, mean, sigma, rSquared, success) = FitGaussian(x, y, maxVal, maxIdx, 1.0);

            if (success && mean >= 0 && mean < gradient.Length)
            {
                result.Position = mean;
                result.RSquared = rSquared;
                result.Success = true;
            }
            else
            {
                result.Success = false;
            }
        }
        catch { result.Success = false; }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// 矩法（質心法）
    /// </summary>
    public static MethodResult MomentMethod(double[] gradient)
    {
        var sw = Stopwatch.StartNew();
        var result = new MethodResult { MethodName = "Moment" };

        try
        {
            if (gradient.Length < 3) { result.Success = false; return result; }

            double[] absGradient = gradient.Select(Math.Abs).ToArray();
            double maxVal = absGradient.Max();
            double threshold = maxVal * 0.3;

            double sumWeighted = 0;
            double sumWeight = 0;

            for (int i = 0; i < absGradient.Length; i++)
            {
                if (absGradient[i] >= threshold)
                {
                    sumWeighted += i * absGradient[i];
                    sumWeight += absGradient[i];
                }
            }

            if (sumWeight > 1e-10)
            {
                result.Position = sumWeighted / sumWeight;
                result.Success = true;
            }
            else
            {
                result.Success = false;
            }
        }
        catch { result.Success = false; }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// Sigmoid 擬合法
    /// </summary>
    public static MethodResult SigmoidFit(double[] intensity)
    {
        var sw = Stopwatch.StartNew();
        var result = new MethodResult { MethodName = "Sigmoid" };

        try
        {
            if (intensity.Length < 5) { result.Success = false; return result; }

            double[] x = new double[intensity.Length];
            for (int i = 0; i < intensity.Length; i++) x[i] = i;

            double minY = intensity.Min();
            double maxY = intensity.Max();
            double initA = minY;
            double initB = (maxY - minY) / 2.0;
            double initX0 = intensity.Length / 2.0;
            double initSigma = 1.0;

            var (a, b, x0, sigma, rSquared, success) = FitSigmoid(x, intensity, initA, initB, initX0, initSigma);

            if (success && x0 >= 0 && x0 < intensity.Length)
            {
                result.Position = x0;
                result.RSquared = rSquared;
                result.Success = true;
            }
            else
            {
                result.Success = false;
            }
        }
        catch { result.Success = false; }

        sw.Stop();
        result.ElapsedMs = sw.Elapsed.TotalMilliseconds;
        return result;
    }

    private static (double amplitude, double mean, double sigma, double rSquared, bool success)
        FitGaussian(double[] x, double[] y, double initA, double initMu, double initSigma)
    {
        double a = initA, mu = initMu, sigma = Math.Max(initSigma, 0.1);
        double lambda = 0.001;

        for (int iter = 0; iter < 100; iter++)
        {
            var residuals = Vector<double>.Build.Dense(x.Length);
            var jacobian = Matrix<double>.Build.Dense(x.Length, 3);

            for (int i = 0; i < x.Length; i++)
            {
                double diff = x[i] - mu;
                double exp = Math.Exp(-diff * diff / (2 * sigma * sigma));
                double predicted = a * exp;
                residuals[i] = y[i] - predicted;

                jacobian[i, 0] = exp;
                jacobian[i, 1] = a * exp * diff / (sigma * sigma);
                jacobian[i, 2] = a * exp * diff * diff / (sigma * sigma * sigma);
            }

            var jtj = jacobian.TransposeThisAndMultiply(jacobian);
            var jtr = jacobian.TransposeThisAndMultiply(residuals);
            var damped = jtj + lambda * Matrix<double>.Build.DenseIdentity(3);

            try
            {
                var delta = damped.Solve(jtr);
                double newA = a + delta[0];
                double newMu = mu + delta[1];
                double newSigma = sigma + delta[2];

                if (newSigma < 0.01) newSigma = 0.01;
                if (newA < 0) newA = Math.Abs(newA);

                double oldError = residuals.L2Norm();
                var newResiduals = Vector<double>.Build.Dense(x.Length);
                for (int i = 0; i < x.Length; i++)
                {
                    double diff = x[i] - newMu;
                    newResiduals[i] = y[i] - newA * Math.Exp(-diff * diff / (2 * newSigma * newSigma));
                }
                double newError = newResiduals.L2Norm();

                if (newError < oldError)
                {
                    a = newA; mu = newMu; sigma = newSigma;
                    lambda *= 0.1;
                    if (Math.Abs(oldError - newError) < 1e-6) break;
                }
                else
                {
                    lambda *= 10;
                }
            }
            catch { break; }
        }

        // Calculate R²
        double meanY = y.Average();
        double ssTot = y.Sum(yi => (yi - meanY) * (yi - meanY));
        double ssRes = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double diff = x[i] - mu;
            double predicted = a * Math.Exp(-diff * diff / (2 * sigma * sigma));
            ssRes += (y[i] - predicted) * (y[i] - predicted);
        }
        double rSquared = ssTot > 0 ? 1 - ssRes / ssTot : 1;

        return (a, mu, Math.Abs(sigma), rSquared, true);
    }

    private static (double a, double b, double x0, double sigma, double rSquared, bool success)
        FitSigmoid(double[] x, double[] y, double initA, double initB, double initX0, double initSigma)
    {
        double a = initA, b = initB, x0 = initX0, sigma = Math.Max(initSigma, 0.1);
        double lambda = 0.001;
        double sqrt2 = Math.Sqrt(2);

        for (int iter = 0; iter < 100; iter++)
        {
            var residuals = Vector<double>.Build.Dense(x.Length);
            var jacobian = Matrix<double>.Build.Dense(x.Length, 4);

            for (int i = 0; i < x.Length; i++)
            {
                double z = (x[i] - x0) / (sigma * sqrt2);
                double erfZ = SpecialFunctions.Erf(z);
                double predicted = a + b * (1 + erfZ);
                residuals[i] = y[i] - predicted;

                double expZ2 = Math.Exp(-z * z);
                double derfDz = 2 / Math.Sqrt(Math.PI) * expZ2;

                jacobian[i, 0] = 1;
                jacobian[i, 1] = 1 + erfZ;
                jacobian[i, 2] = -b * derfDz / (sigma * sqrt2);
                jacobian[i, 3] = -b * derfDz * (x[i] - x0) / (sigma * sigma * sqrt2);
            }

            var jtj = jacobian.TransposeThisAndMultiply(jacobian);
            var jtr = jacobian.TransposeThisAndMultiply(residuals);
            var damped = jtj + lambda * Matrix<double>.Build.DenseIdentity(4);

            try
            {
                var delta = damped.Solve(jtr);
                double newA = a + delta[0];
                double newB = b + delta[1];
                double newX0 = x0 + delta[2];
                double newSigma = sigma + delta[3];

                if (newSigma < 0.01) newSigma = 0.01;

                double oldError = residuals.L2Norm();
                var newResiduals = Vector<double>.Build.Dense(x.Length);
                for (int i = 0; i < x.Length; i++)
                {
                    double z = (x[i] - newX0) / (newSigma * sqrt2);
                    newResiduals[i] = y[i] - (newA + newB * (1 + SpecialFunctions.Erf(z)));
                }
                double newError = newResiduals.L2Norm();

                if (newError < oldError)
                {
                    a = newA; b = newB; x0 = newX0; sigma = newSigma;
                    lambda *= 0.1;
                    if (Math.Abs(oldError - newError) < 1e-6) break;
                }
                else
                {
                    lambda *= 10;
                }
            }
            catch { break; }
        }

        // Calculate R²
        double meanY = y.Average();
        double ssTot = y.Sum(yi => (yi - meanY) * (yi - meanY));
        double ssRes = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double z = (x[i] - x0) / (sigma * sqrt2);
            double predicted = a + b * (1 + SpecialFunctions.Erf(z));
            ssRes += (y[i] - predicted) * (y[i] - predicted);
        }
        double rSquared = ssTot > 0 ? 1 - ssRes / ssTot : 1;

        return (a, b, x0, Math.Abs(sigma), rSquared, true);
    }
}

/// <summary>
/// 亞像素分析完整結果
/// </summary>
public class SubpixelAnalysisResult
{
    public double[] GrayProfile { get; set; } = Array.Empty<double>();
    public double[] GradientProfile { get; set; } = Array.Empty<double>();
    public List<SubpixelMethods.MethodResult> Results { get; set; } = new();
    public double? TrueEdgePosition { get; set; }
}

/// <summary>
/// 亞像素邊緣量測結果
/// </summary>
public class SubpixelEdgeResult : MeasurementResult
{
    public PointF RoiStart { get; set; }
    public PointF RoiEnd { get; set; }
    public List<SubpixelMethods.MethodResult> MethodResults { get; set; } = new();

    public override string ResultType => "亞像素";

    public override string GetSummary(double resolution)
    {
        var best = MethodResults.Where(r => r.Success).OrderBy(r => Math.Abs(r.Error ?? double.MaxValue)).FirstOrDefault();
        if (best != null && best.Position.HasValue)
            return $"最佳: {best.MethodName} @ {best.Position.Value:F3}px";
        return "分析完成";
    }

    public override void Draw(Graphics g, Func<PointF, PointF> toScreen, bool isSelected)
    {
        var p1 = toScreen(RoiStart);
        var p2 = toScreen(RoiEnd);

        using var pen = new Pen(isSelected ? Color.Yellow : Color.Red, isSelected ? 3 : 2);
        g.DrawLine(pen, p1, p2);

        using var brush = new SolidBrush(Color.Red);
        g.FillEllipse(brush, p1.X - 5, p1.Y - 5, 10, 10);
        g.FillEllipse(brush, p2.X - 5, p2.Y - 5, 10, 10);
    }
}
