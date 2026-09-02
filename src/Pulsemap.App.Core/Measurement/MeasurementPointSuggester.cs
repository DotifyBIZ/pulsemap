using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Measurement;

/// <summary>
/// Suggests where the guided measurement walk should stop next — reuses the same candidate-grid
/// approach as AP placement, at a wider spacing since a human has to walk to each point rather
/// than it just being a math candidate.
/// </summary>
public static class MeasurementPointSuggester
{
    private const double SpacingMeters = 3.0;

    // A candidate within this radius of an already-captured TestPoint is treated as covered — no
    // need to send the surveyor back to (almost) the same spot.
    private const double AlreadyMeasuredToleranceMeters = 1.0;

    // Kriging needs at least 2 distinct measurements before its variance says anything meaningful
    // about where the model is uncertain; with fewer, adaptive ordering falls back to the plain
    // grid-scan order below.
    private const int MinimumMeasurementsForAdaptiveOrdering = 2;

    public static IReadOnlyList<Point2D> SuggestPoints(Floor floor)
    {
        ArgumentNullException.ThrowIfNull(floor);

        return UnmeasuredCandidates(floor);
    }

    /// <summary>Same candidate points as <see cref="SuggestPoints(Floor)"/>, but ordered by
    /// descending Kriging interpolation uncertainty for <paramref name="band"/> instead of grid-scan
    /// order — the guided walk visits wherever the model knows least first. Falls back to the plain
    /// candidate order untouched when there aren't yet enough same-band measurements to interpolate
    /// anything from.</summary>
    public static IReadOnlyList<Point2D> SuggestPoints(Floor floor, Band band, IKrigingInterpolator krigingInterpolator)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(krigingInterpolator);

        var candidates = UnmeasuredCandidates(floor);
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var samples = floor.TestPoints
            .Where(tp => tp.Measurements.ContainsKey(band))
            .Select(tp => new CoverageSample(tp.Position, tp.Measurements[band].SignalDbm))
            .ToList();

        if (samples.Count < MinimumMeasurementsForAdaptiveOrdering)
        {
            return candidates;
        }

        var variances = krigingInterpolator.InterpolateVariance(samples, candidates);
        return candidates
            .Zip(variances, (point, variance) => (Point: point, Variance: variance))
            .OrderByDescending(pair => pair.Variance)
            .Select(pair => pair.Point)
            .ToList();
    }

    private static List<Point2D> UnmeasuredCandidates(Floor floor) =>
        FloorGrid.BuildPoints(floor, SpacingMeters)
            .Where(candidate => !floor.TestPoints.Any(tp => tp.Position.DistanceTo(candidate) < AlreadyMeasuredToleranceMeters))
            .ToList();
}
