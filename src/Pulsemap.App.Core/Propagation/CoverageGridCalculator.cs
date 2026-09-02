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
    // A typical concrete-floor-plus-ceiling assembly's attenuation, applied once per level crossed.
    // Deliberately flat rather than per-material (unlike WallAttenuationTable) and assumes every
    // floor shares the same local X/Y origin (so "the same spot one level up" is a real, if
    // simplified, concept) — modeling true 3D geometry (floor height, differently-shaped floors,
    // oblique paths) is a bigger, more speculative design than this deserves.
    private const double InterFloorAttenuationDbPerLevel = 25;

    private static readonly IReadOnlyList<Wall> NoWalls = [];

    public static IReadOnlyList<CoverageSample> ComputeGrid(Floor floor, IReadOnlyList<Floor> allFloors, Band band, double gridSpacingMeters, IPropagationModel propagationModel)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(allFloors);
        ArgumentNullException.ThrowIfNull(propagationModel);

        var gridPoints = FloorGrid.BuildPoints(floor, gridSpacingMeters);
        if (gridPoints.Count == 0)
        {
            return [];
        }

        var samples = new List<CoverageSample>(gridPoints.Count);

        foreach (var point in gridPoints)
        {
            if (StrongestSignalDbm(point, floor, allFloors, band, propagationModel) is { } value)
            {
                samples.Add(new CoverageSample(point, value));
            }
        }

        return samples;
    }

    /// <summary>The strongest predicted signal at a single point, across every access point on
    /// <paramref name="floor"/> and (subject to the same skip rules <see cref="ComputeGrid"/> uses)
    /// every other floor in <paramref name="allFloors"/>. Shared by the coverage grid (one call per
    /// grid point) and by Workspace's single-point diagnostics comparison, so the two never diverge
    /// on cross-floor/outdoor skip rules or the per-level attenuation figure.</summary>
    public static double? StrongestSignalDbm(Point2D point, Floor floor, IReadOnlyList<Floor> allFloors, Band band, IPropagationModel propagationModel)
    {
        double? strongest = null;

        foreach (var otherFloor in allFloors)
        {
            bool sameFloor = otherFloor.Id == floor.Id;
            int levelDifference = Math.Abs(floor.Level - otherFloor.Level);
            if (!sameFloor && (levelDifference == 0 || floor.IsOutdoor || otherFloor.IsOutdoor))
            {
                // Skip cross-floor contribution when: a different floor shares this one's
                // Level (Level is the only signal we have for floor-above/below relationships,
                // so an unrelated same-level area shouldn't silently leak in), or either side is
                // outdoor — the flat "stacked at the same origin" model this constant represents
                // doesn't meaningfully describe an open-air area's relationship to an indoor
                // floor, so outdoor areas' coverage stays isolated to their own access points.
                continue;
            }

            foreach (var accessPoint in otherFloor.AccessPoints)
            {
                if (!accessPoint.Radios.TryGetValue(band, out var radio))
                {
                    continue;
                }

                // Cross-floor: the other floor's walls aren't in this floor's horizontal path,
                // so free-space-only plus a flat per-level penalty stands in for the ceiling/
                // floor slab loss a real building would add.
                double signal = sameFloor
                    ? propagationModel.PredictSignalDbm(accessPoint.Position, radio.TransmitPowerDbm, point, band, floor.Walls)
                    : propagationModel.PredictSignalDbm(accessPoint.Position, radio.TransmitPowerDbm, point, band, NoWalls) - (InterFloorAttenuationDbPerLevel * levelDifference);

                if (strongest is null || signal > strongest)
                {
                    strongest = signal;
                }
            }
        }

        return strongest;
    }
}
