namespace Pulsemap.App.Core.Interpolation;

/// <summary>
/// Spherical semivariogram, gamma(h) = nugget + sill * (1.5*(h/range) - 0.5*(h/range)^3) for
/// h &lt; range, else nugget + sill; gamma(0) = 0.
///
/// Parameters are estimated heuristically rather than fit from a binned empirical variogram —
/// Phase 1 surveys don't have enough test points for that to be meaningful. Nugget = 0 (so
/// kriging reproduces sample values exactly at sample points), sill = sample variance, range =
/// a third of the maximum pairwise sample distance. Both are standard geostatistics rules of
/// thumb for a first-pass model; revisit once real survey data shows whether they hold up.
/// </summary>
public sealed class Variogram
{
    private readonly double _sill;
    private readonly double _range;

    private Variogram(double sill, double range)
    {
        _sill = sill;
        _range = range;
    }

    public static Variogram FitFromSamples(IReadOnlyList<CoverageSample> samples)
    {
        double mean = samples.Average(s => s.ValueDbm);
        double sill = samples.Sum(s => (s.ValueDbm - mean) * (s.ValueDbm - mean)) / samples.Count;

        double maxDistance = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            for (int j = i + 1; j < samples.Count; j++)
            {
                maxDistance = Math.Max(maxDistance, samples[i].Position.DistanceTo(samples[j].Position));
            }
        }

        double range = Math.Max(maxDistance / 3.0, 0.1);

        return new Variogram(sill: Math.Max(sill, 1e-6), range);
    }

    public double Evaluate(double distance)
    {
        if (distance <= 0)
        {
            return 0;
        }

        if (distance >= _range)
        {
            return _sill;
        }

        double ratio = distance / _range;
        return _sill * ((1.5 * ratio) - (0.5 * ratio * ratio * ratio));
    }
}
