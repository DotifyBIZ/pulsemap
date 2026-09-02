using MathNet.Numerics.LinearAlgebra;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Interpolation;

/// <summary>
/// Ordinary kriging: weights lambda_i minimizing estimation variance subject to sum(lambda_i) = 1
/// (unbiasedness), solved via the standard Lagrange-multiplier system
///
///   [Gamma  1] [lambda]   [gamma_0]
///   [1^T    0] [mu    ] = [1      ]
///
/// where Gamma_ij = variogram(dist(sample_i, sample_j)) and gamma_0_i = variogram(dist(sample_i, query)).
/// Gamma is factored once via LU and reused across all query positions — the matrix depends only
/// on the sample set, not the query point, so this turns an O(queries * n^3) job into O(n^3 + queries * n^2).
/// </summary>
public sealed class OrdinaryKrigingInterpolator : IKrigingInterpolator
{
    private const double ConstantValueTolerance = 1e-9;

    public IReadOnlyList<double> Interpolate(IReadOnlyList<CoverageSample> samples, IReadOnlyList<Point2D> queryPositions) =>
        [.. InterpolateCore(samples, queryPositions).Select(r => r.Estimate)];

    public IReadOnlyList<double> InterpolateVariance(IReadOnlyList<CoverageSample> samples, IReadOnlyList<Point2D> queryPositions) =>
        [.. InterpolateCore(samples, queryPositions).Select(r => r.Variance)];

    // Shared by Interpolate/InterpolateVariance so both reuse the same one-time LU factorization —
    // the estimate and its variance fall out of the exact same per-query solve, not a second pass.
    private static IReadOnlyList<(double Estimate, double Variance)> InterpolateCore(IReadOnlyList<CoverageSample> samples, IReadOnlyList<Point2D> queryPositions)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(queryPositions);

        if (samples.Count == 0)
        {
            throw new ArgumentException("At least one sample is required to interpolate.", nameof(samples));
        }

        samples = DeduplicatePositions(samples);

        if (samples.Count == 1 || IsConstant(samples))
        {
            double constantValue = samples[0].ValueDbm;
            return queryPositions.Select(_ => (constantValue, 0.0)).ToArray();
        }

        var variogram = Variogram.FitFromSamples(samples);
        int n = samples.Count;

        var systemMatrix = Matrix<double>.Build.Dense(n + 1, n + 1);
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                systemMatrix[i, j] = variogram.Evaluate(samples[i].Position.DistanceTo(samples[j].Position));
            }

            systemMatrix[i, n] = 1;
            systemMatrix[n, i] = 1;
        }

        var factorized = systemMatrix.LU();

        var results = new (double Estimate, double Variance)[queryPositions.Count];
        for (int q = 0; q < queryPositions.Count; q++)
        {
            var rhs = Vector<double>.Build.Dense(n + 1);
            for (int i = 0; i < n; i++)
            {
                rhs[i] = variogram.Evaluate(samples[i].Position.DistanceTo(queryPositions[q]));
            }

            rhs[n] = 1;

            var weights = factorized.Solve(rhs);

            double estimate = 0;
            for (int i = 0; i < n; i++)
            {
                estimate += weights[i] * samples[i].ValueDbm;
            }

            // Ordinary kriging variance: sum(lambda_i * gamma_0_i) + mu, where mu is the Lagrange
            // multiplier — the same weights vector's last entry — from the system solved above.
            double variance = weights[n];
            for (int i = 0; i < n; i++)
            {
                variance += weights[i] * rhs[i];
            }

            results[q] = (estimate, variance);
        }

        return results;
    }

    private static bool IsConstant(IReadOnlyList<CoverageSample> samples)
    {
        double first = samples[0].ValueDbm;
        return samples.All(s => Math.Abs(s.ValueDbm - first) < ConstantValueTolerance);
    }

    // Two samples at the same position make every pairwise variogram term between them 0
    // (self-distance), which makes the system matrix singular and Solve() returns NaN silently.
    // Averaging co-located samples up front is the standard kriging fix for duplicate positions.
    private static IReadOnlyList<CoverageSample> DeduplicatePositions(IReadOnlyList<CoverageSample> samples)
    {
        var groups = samples.GroupBy(s => s.Position).ToList();
        if (groups.Count == samples.Count)
        {
            return samples;
        }

        return groups.Select(g => new CoverageSample(g.Key, g.Average(s => s.ValueDbm))).ToArray();
    }
}
