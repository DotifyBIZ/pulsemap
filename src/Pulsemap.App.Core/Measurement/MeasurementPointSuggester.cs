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

    public static IReadOnlyList<Point2D> SuggestPoints(Floor floor)
    {
        ArgumentNullException.ThrowIfNull(floor);

        return FloorGrid.BuildPoints(floor, SpacingMeters)
            .Where(candidate => !floor.TestPoints.Any(tp => tp.Position.DistanceTo(candidate) < AlreadyMeasuredToleranceMeters))
            .ToList();
    }
}
