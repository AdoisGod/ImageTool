using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace SubpixelEdgeComparator.Utils;

/// <summary>
/// 數學計算輔助類別
/// </summary>
public static class MathHelper
{
    /// <summary>
    /// 計算誤差函數 (Error Function)
    /// </summary>
    public static double Erf(double x)
    {
        return SpecialFunctions.Erf(x);
    }

    /// <summary>
    /// 計算 R² (決定係數)
    /// </summary>
    public static double CalculateRSquared(double[] observed, double[] predicted)
    {
        if (observed.Length != predicted.Length || observed.Length == 0)
            return 0;

        double mean = observed.Average();
        double ssTotal = observed.Sum(y => (y - mean) * (y - mean));
        double ssResidual = observed.Zip(predicted, (o, p) => (o - p) * (o - p)).Sum();

        if (ssTotal == 0) return 1;
        return 1 - (ssResidual / ssTotal);
    }

    /// <summary>
    /// 高斯函數
    /// </summary>
    public static double Gaussian(double x, double amplitude, double mean, double sigma)
    {
        double exponent = -((x - mean) * (x - mean)) / (2 * sigma * sigma);
        return amplitude * Math.Exp(exponent);
    }

    /// <summary>
    /// Sigmoid 函數 (使用 erf)
    /// y = A + B * (1 + erf((x - x0) / (sigma * sqrt(2))))
    /// </summary>
    public static double Sigmoid(double x, double a, double b, double x0, double sigma)
    {
        return a + b * (1 + Erf((x - x0) / (sigma * Math.Sqrt(2))));
    }

    /// <summary>
    /// 使用三點拋物線擬合找峰值位置
    /// </summary>
    public static (double position, bool success) ParabolicPeakFit(double y0, double y1, double y2)
    {
        // 假設三點在 x = -1, 0, 1
        // y = ax² + bx + c
        // y0 = a - b + c
        // y1 = c
        // y2 = a + b + c
        //
        // c = y1
        // a = (y0 + y2 - 2*y1) / 2
        // b = (y2 - y0) / 2
        //
        // 峰值位置：dy/dx = 2ax + b = 0 => x = -b/(2a)

        double c = y1;
        double a = (y0 + y2 - 2 * y1) / 2.0;
        double b = (y2 - y0) / 2.0;

        if (Math.Abs(a) < 1e-10)
        {
            return (0, false);
        }

        double offset = -b / (2 * a);

        // 偏移量應該在 -1 到 1 之間
        if (offset < -1 || offset > 1)
        {
            return (offset, false);
        }

        return (offset, true);
    }

    /// <summary>
    /// 使用 Levenberg-Marquardt 演算法進行高斯擬合
    /// </summary>
    public static (double amplitude, double mean, double sigma, double rSquared, bool success)
        FitGaussian(double[] x, double[] y)
    {
        try
        {
            // 初始值估計
            int peakIdx = 0;
            double peakVal = y[0];
            for (int i = 1; i < y.Length; i++)
            {
                if (y[i] > peakVal)
                {
                    peakVal = y[i];
                    peakIdx = i;
                }
            }

            double initialAmplitude = peakVal;
            double initialMean = x[peakIdx];
            double initialSigma = 1.0;

            // 使用簡化的非線性最小平方擬合
            var result = FitGaussianLM(x, y, initialAmplitude, initialMean, initialSigma);

            if (result.success)
            {
                // 計算 R²
                double[] predicted = x.Select(xi => Gaussian(xi, result.amplitude, result.mean, result.sigma)).ToArray();
                double rSquared = CalculateRSquared(y, predicted);
                return (result.amplitude, result.mean, result.sigma, rSquared, true);
            }

            return (0, 0, 0, 0, false);
        }
        catch
        {
            return (0, 0, 0, 0, false);
        }
    }

    /// <summary>
    /// 簡化的 Levenberg-Marquardt 高斯擬合
    /// </summary>
    private static (double amplitude, double mean, double sigma, bool success)
        FitGaussianLM(double[] x, double[] y, double initA, double initMu, double initSigma)
    {
        double a = initA;
        double mu = initMu;
        double sigma = Math.Max(initSigma, 0.1);

        double lambda = 0.001;
        int maxIterations = 100;
        double tolerance = 1e-6;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            // 計算殘差和雅可比矩陣
            var (residuals, jacobian) = ComputeGaussianJacobian(x, y, a, mu, sigma);

            // J^T * J
            var jtj = jacobian.TransposeThisAndMultiply(jacobian);
            // J^T * r
            var jtr = jacobian.TransposeThisAndMultiply(residuals);

            // (J^T * J + lambda * I) * delta = J^T * r
            var damped = jtj + lambda * Matrix<double>.Build.DenseIdentity(3);
            var delta = damped.Solve(jtr);

            double newA = a + delta[0];
            double newMu = mu + delta[1];
            double newSigma = sigma + delta[2];

            if (newSigma < 0.01) newSigma = 0.01;
            if (newA < 0) newA = Math.Abs(newA);

            // 計算新的殘差
            double oldError = residuals.L2Norm();
            var newResiduals = ComputeGaussianResiduals(x, y, newA, newMu, newSigma);
            double newError = newResiduals.L2Norm();

            if (newError < oldError)
            {
                a = newA;
                mu = newMu;
                sigma = newSigma;
                lambda *= 0.1;

                if (Math.Abs(oldError - newError) < tolerance)
                    break;
            }
            else
            {
                lambda *= 10;
            }
        }

        return (a, mu, Math.Abs(sigma), true);
    }

    private static (Vector<double> residuals, Matrix<double> jacobian)
        ComputeGaussianJacobian(double[] x, double[] y, double a, double mu, double sigma)
    {
        int n = x.Length;
        var residuals = Vector<double>.Build.Dense(n);
        var jacobian = Matrix<double>.Build.Dense(n, 3);

        for (int i = 0; i < n; i++)
        {
            double xi = x[i];
            double diff = xi - mu;
            double exp = Math.Exp(-diff * diff / (2 * sigma * sigma));
            double predicted = a * exp;

            residuals[i] = y[i] - predicted;

            // 偏導數
            jacobian[i, 0] = exp;  // ∂f/∂a
            jacobian[i, 1] = a * exp * diff / (sigma * sigma);  // ∂f/∂mu
            jacobian[i, 2] = a * exp * diff * diff / (sigma * sigma * sigma);  // ∂f/∂sigma
        }

        return (residuals, jacobian);
    }

    private static Vector<double> ComputeGaussianResiduals(double[] x, double[] y, double a, double mu, double sigma)
    {
        int n = x.Length;
        var residuals = Vector<double>.Build.Dense(n);

        for (int i = 0; i < n; i++)
        {
            double diff = x[i] - mu;
            double predicted = a * Math.Exp(-diff * diff / (2 * sigma * sigma));
            residuals[i] = y[i] - predicted;
        }

        return residuals;
    }

    /// <summary>
    /// 使用 Levenberg-Marquardt 演算法進行 Sigmoid 擬合
    /// </summary>
    public static (double a, double b, double x0, double sigma, double rSquared, bool success)
        FitSigmoid(double[] x, double[] y)
    {
        try
        {
            // 初始值估計
            double minY = y.Min();
            double maxY = y.Max();
            double initialA = minY;
            double initialB = (maxY - minY) / 2.0;
            double initialX0 = x[x.Length / 2];
            double initialSigma = 1.0;

            var result = FitSigmoidLM(x, y, initialA, initialB, initialX0, initialSigma);

            if (result.success)
            {
                double[] predicted = x.Select(xi => Sigmoid(xi, result.a, result.b, result.x0, result.sigma)).ToArray();
                double rSquared = CalculateRSquared(y, predicted);
                return (result.a, result.b, result.x0, result.sigma, rSquared, true);
            }

            return (0, 0, 0, 0, 0, false);
        }
        catch
        {
            return (0, 0, 0, 0, 0, false);
        }
    }

    private static (double a, double b, double x0, double sigma, bool success)
        FitSigmoidLM(double[] x, double[] y, double initA, double initB, double initX0, double initSigma)
    {
        double a = initA;
        double b = initB;
        double x0 = initX0;
        double sigma = Math.Max(initSigma, 0.1);

        double lambda = 0.001;
        int maxIterations = 100;
        double tolerance = 1e-6;

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var (residuals, jacobian) = ComputeSigmoidJacobian(x, y, a, b, x0, sigma);

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
                var newResiduals = ComputeSigmoidResiduals(x, y, newA, newB, newX0, newSigma);
                double newError = newResiduals.L2Norm();

                if (newError < oldError)
                {
                    a = newA;
                    b = newB;
                    x0 = newX0;
                    sigma = newSigma;
                    lambda *= 0.1;

                    if (Math.Abs(oldError - newError) < tolerance)
                        break;
                }
                else
                {
                    lambda *= 10;
                }
            }
            catch
            {
                lambda *= 10;
            }
        }

        return (a, b, x0, Math.Abs(sigma), true);
    }

    private static (Vector<double> residuals, Matrix<double> jacobian)
        ComputeSigmoidJacobian(double[] x, double[] y, double a, double b, double x0, double sigma)
    {
        int n = x.Length;
        var residuals = Vector<double>.Build.Dense(n);
        var jacobian = Matrix<double>.Build.Dense(n, 4);

        double sqrt2 = Math.Sqrt(2);

        for (int i = 0; i < n; i++)
        {
            double xi = x[i];
            double z = (xi - x0) / (sigma * sqrt2);
            double erfZ = Erf(z);
            double predicted = a + b * (1 + erfZ);

            residuals[i] = y[i] - predicted;

            // 偏導數
            double expZ2 = Math.Exp(-z * z);
            double derfDz = 2 / Math.Sqrt(Math.PI) * expZ2;

            jacobian[i, 0] = 1;  // ∂f/∂a
            jacobian[i, 1] = 1 + erfZ;  // ∂f/∂b
            jacobian[i, 2] = -b * derfDz / (sigma * sqrt2);  // ∂f/∂x0
            jacobian[i, 3] = -b * derfDz * (xi - x0) / (sigma * sigma * sqrt2);  // ∂f/∂sigma
        }

        return (residuals, jacobian);
    }

    private static Vector<double> ComputeSigmoidResiduals(double[] x, double[] y, double a, double b, double x0, double sigma)
    {
        int n = x.Length;
        var residuals = Vector<double>.Build.Dense(n);

        for (int i = 0; i < n; i++)
        {
            double predicted = Sigmoid(x[i], a, b, x0, sigma);
            residuals[i] = y[i] - predicted;
        }

        return residuals;
    }

    /// <summary>
    /// 雙線性插值
    /// </summary>
    public static double BilinearInterpolate(double[,] image, double x, double y)
    {
        int height = image.GetLength(0);
        int width = image.GetLength(1);

        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);
        int x1 = x0 + 1;
        int y1 = y0 + 1;

        // 邊界檢查
        x0 = Math.Clamp(x0, 0, width - 1);
        x1 = Math.Clamp(x1, 0, width - 1);
        y0 = Math.Clamp(y0, 0, height - 1);
        y1 = Math.Clamp(y1, 0, height - 1);

        double xFrac = x - Math.Floor(x);
        double yFrac = y - Math.Floor(y);

        double v00 = image[y0, x0];
        double v10 = image[y0, x1];
        double v01 = image[y1, x0];
        double v11 = image[y1, x1];

        double v0 = v00 * (1 - xFrac) + v10 * xFrac;
        double v1 = v01 * (1 - xFrac) + v11 * xFrac;

        return v0 * (1 - yFrac) + v1 * yFrac;
    }
}
