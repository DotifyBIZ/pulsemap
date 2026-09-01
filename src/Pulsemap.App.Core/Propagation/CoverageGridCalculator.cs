using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Propagation;

/// <summary>
/// Predicts signal strength across a regular grid spanning a floor, for coverage visualization.
/// Each cell takes the strongest signal among all placed access points on the given band —
/// predictive only (from placement + propagation model), not measurement-based; that's Kriging's
/// job once a survey has real <see cref="TestPoint"/> readings to interpolate from.
/// </summary>
public static class CoverageGridCalculator
{
    public static IReadOnlyList<CoverageSample> ComputeGrid(Floor floor, Band band, double gridSpacingMeters, IPropagationModel propagationModel)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(propagationModel);

        if (floor.Walls.Count == 0 || floor.AccessPoints.Count == 0)
        {
            return [];
        }

        var gridPoints = FloorGrid.BuildPoints(floor, gridSpacingMeters);
        var samples = new List<CoverageSample>(gridPoints.Count);

        foreach (var point in gridPoints)
        {
            double? strongest = null;

            foreach (var accessPoint in floor.AccessPoints)
            {
                if (!accessPoint.Radios.TryGetValue(band, out var radio))
                {
                    continue;
                }

                double signal = propagationModel.PredictSignalDbm(accessPoint.Position, radio.TransmitPowerDbm, point, band, floor.Walls);
                if (strongest is null || signal > strongest)
                {
                    strongest = signal;
                }
            }

            if (strongest is { } value)
            {
                samples.Add(new CoverageSample(point, value));
            }
        }

        return samples;
    }
}
