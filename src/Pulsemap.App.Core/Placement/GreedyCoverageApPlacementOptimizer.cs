using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Placement;

/// <summary>
/// Greedy maximum-coverage placement (the standard approximation to the facility-location
/// "maximal coverage location problem"): repeatedly place an AP at whichever candidate grid
/// position covers the most currently-uncovered area, until a target coverage fraction or a
/// safety cap on AP count is reached. Physical positions are chosen against the requested band
/// with the shortest range (highest frequency) — the same position then serves every other
/// requested band too, since a real AP is one multi-radio device, and lower bands reach further
/// from the same spot than the driving band does.
/// </summary>
public sealed class GreedyCoverageApPlacementOptimizer : IApPlacementOptimizer
{
    private const double GridSpacingMeters = 2.0;

    // -67dBm is the conventional "reliable data" WiFi planning threshold (well above the noise
    // floor, enough margin for real-world fading) — not a hard regulatory figure.
    private const double ReliableCoverageThresholdDbm = -67;

    private const double TargetCoverageFraction = 0.95;
    private const int MaxAccessPoints = 8;

    public IReadOnlyList<AccessPoint> SuggestPlacements(Floor floor, IReadOnlyList<Band> bands, IPropagationModel propagationModel)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(bands);
        ArgumentNullException.ThrowIfNull(propagationModel);

        if (bands.Count == 0)
        {
            throw new ArgumentException("At least one band is required.", nameof(bands));
        }

        // Band's enum ordinal increases with frequency (2.4 < 5 < 6GHz), so Max() is the
        // shortest-range, hardest-to-cover requested band.
        var drivingBand = bands.Max();
        double drivingPower = ChannelPlan.DefaultTransmitPowerDbm(drivingBand);
        var gridPoints = BuildGrid(floor);

        if (gridPoints.Count == 0)
        {
            return [];
        }

        var covered = new bool[gridPoints.Count];
        var placements = new List<Point2D>();

        while (placements.Count < MaxAccessPoints && CoverageFraction(covered) < TargetCoverageFraction)
        {
            var (bestCandidate, bestCoverageMask) = FindBestCandidate(gridPoints, covered, drivingBand, drivingPower, floor.Walls, propagationModel);

            if (bestCandidate is not { } chosen || bestCoverageMask is null)
            {
                break; // no remaining candidate covers any new area — more APs wouldn't help
            }

            placements.Add(chosen);
            for (int i = 0; i < gridPoints.Count; i++)
            {
                covered[i] |= bestCoverageMask[i];
            }
        }

        return placements.Select((position, index) => BuildAccessPoint(position, index, bands)).ToList();
    }

    private static (Point2D? Candidate, bool[]? CoverageMask) FindBestCandidate(
        List<Point2D> gridPoints, bool[] covered, Band drivingBand, double drivingPower, IReadOnlyList<Wall> walls, IPropagationModel propagationModel)
    {
        Point2D? bestCandidate = null;
        bool[]? bestCoverageMask = null;
        int bestNewlyCovered = 0;

        foreach (var candidate in gridPoints)
        {
            var candidateCoverage = new bool[gridPoints.Count];
            int newlyCovered = 0;

            for (int i = 0; i < gridPoints.Count; i++)
            {
                if (covered[i])
                {
                    continue;
                }

                double signal = propagationModel.PredictSignalDbm(candidate, drivingPower, gridPoints[i], drivingBand, walls);
                if (signal >= ReliableCoverageThresholdDbm)
                {
                    candidateCoverage[i] = true;
                    newlyCovered++;
                }
            }

            if (newlyCovered > bestNewlyCovered)
            {
                bestNewlyCovered = newlyCovered;
                bestCandidate = candidate;
                bestCoverageMask = candidateCoverage;
            }
        }

        return (bestCandidate, bestCoverageMask);
    }

    private static AccessPoint BuildAccessPoint(Point2D position, int index, IReadOnlyList<Band> bands)
    {
        var accessPoint = new AccessPoint
        {
            Position = position,
            Label = $"AP {index + 1}",
        };

        foreach (var band in bands)
        {
            var channels = ChannelPlan.ChannelsFor(band);
            accessPoint.Radios[band] = new BandRadioSettings
            {
                TransmitPowerDbm = ChannelPlan.DefaultTransmitPowerDbm(band),
                Channel = channels[index % channels.Count],
            };
        }

        return accessPoint;
    }

    private static double CoverageFraction(bool[] covered) =>
        covered.Length == 0 ? 1.0 : covered.Count(c => c) / (double)covered.Length;

    private static List<Point2D> BuildGrid(Floor floor)
    {
        if (floor.Walls.Count == 0)
        {
            return [];
        }

        double minX = floor.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Min();
        double maxX = floor.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Max();
        double minY = floor.Walls.SelectMany(w => new[] { w.Start.Y, w.End.Y }).Min();
        double maxY = floor.Walls.SelectMany(w => new[] { w.Start.Y, w.End.Y }).Max();

        var points = new List<Point2D>();
        for (double x = minX; x <= maxX; x += GridSpacingMeters)
        {
            for (double y = minY; y <= maxY; y += GridSpacingMeters)
            {
                points.Add(new Point2D(x, y));
            }
        }

        return points;
    }
}
