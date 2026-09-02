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

    // Conventional SINR margin for "reliable" 802.11 data rates: a predicted signal must clear
    // ReliableCoverageThresholdDbm AND stay this many dB above the strongest nearby measured
    // interference, or a real client would contend with/be degraded by that interferer even
    // though it's not literally silence. Deliberately keyed on band, not a specific channel —
    // AP placement runs before channel assignment (BuildAccessPoint), so no channel is known yet
    // here; this treats "reliable" as robust against the worst plausible nearby interferer on the
    // band rather than modeling exact per-channel SINR; a real joint position+channel optimizer
    // would be a much larger redesign than this deserves.
    private const double SinrMarginDb = 10;

    // A TestPoint's interference readings are only treated as representative of a grid point's RF
    // environment within this radius — walk data from elsewhere on the floor says nothing about
    // one specific spot's local noise floor. Wider than the measurement grid's own 3m spacing so
    // a walked point still counts for its immediate neighborhood.
    private const double InterferenceInfluenceRadiusMeters = 8.0;

    private const double TargetCoverageFraction = 0.95;
    private const int MaxAccessPoints = 8;

    // Matches CoverageGridCalculator's own inter-floor attenuation figure — kept as a separate
    // constant rather than shared across layers, same as WorkspaceViewModel's own duplicate of
    // ReliableCoverageThresholdDbm elsewhere in this codebase.
    private const double InterFloorAttenuationDbPerLevel = 25;

    public IReadOnlyList<AccessPoint> SuggestPlacements(Floor floor, IReadOnlyList<Floor> allFloors, IReadOnlyList<Band> bands, IPropagationModel propagationModel)
    {
        ArgumentNullException.ThrowIfNull(floor);
        ArgumentNullException.ThrowIfNull(allFloors);
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
        var gridPoints = FloorGrid.BuildPoints(floor, GridSpacingMeters);

        if (gridPoints.Count == 0)
        {
            return [];
        }

        var effectiveThresholds = gridPoints
            .Select(point => EffectiveReliabilityThresholdDbm(point, drivingBand, floor.TestPoints))
            .ToArray();

        var covered = new bool[gridPoints.Count];
        var placements = new List<Point2D>();

        while (placements.Count < MaxAccessPoints && CoverageFraction(covered) < TargetCoverageFraction)
        {
            var (bestCandidate, bestCoverageMask) = FindBestCandidate(gridPoints, covered, drivingBand, drivingPower, floor.Walls, propagationModel, effectiveThresholds);

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

        return placements.Select((position, index) => BuildAccessPoint(position, index, bands, floor, allFloors)).ToList();
    }

    private static (Point2D? Candidate, bool[]? CoverageMask) FindBestCandidate(
        List<Point2D> gridPoints, bool[] covered, Band drivingBand, double drivingPower, IReadOnlyList<Wall> walls, IPropagationModel propagationModel, double[] effectiveThresholds)
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
                if (signal >= effectiveThresholds[i])
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

    // Falls back to the plain ReliableCoverageThresholdDbm when there's no nearby measurement to
    // raise it — identical behavior to before any guided walk existed.
    private static double EffectiveReliabilityThresholdDbm(Point2D point, Band band, IReadOnlyList<TestPoint> testPoints)
    {
        TestPoint? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (var testPoint in testPoints)
        {
            double distance = testPoint.Position.DistanceTo(point);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = testPoint;
            }
        }

        if (nearest is null || nearestDistance > InterferenceInfluenceRadiusMeters)
        {
            return ReliableCoverageThresholdDbm;
        }

        double strongestNearbyInterferenceDbm = nearest.InterferenceReadings
            .Where(r => r.Band == band)
            .Select(r => (double?)r.SignalDbm)
            .Max() ?? double.NegativeInfinity;

        return Math.Max(ReliableCoverageThresholdDbm, strongestNearbyInterferenceDbm + SinrMarginDb);
    }

    private static AccessPoint BuildAccessPoint(Point2D position, int index, IReadOnlyList<Band> bands, Floor floor, IReadOnlyList<Floor> allFloors)
    {
        var accessPoint = new AccessPoint
        {
            Position = position,
            Label = $"AP {index + 1}",
        };

        foreach (var band in bands)
        {
            var channels = RankChannelsByInterference(ChannelPlan.ChannelsFor(band), band, floor, allFloors);
            accessPoint.Radios[band] = new BandRadioSettings
            {
                TransmitPowerDbm = ChannelPlan.DefaultTransmitPowerDbm(band),
                Channel = channels[index % channels.Count],
            };
        }

        return accessPoint;
    }

    // Orders a band's candidate channels least-congested first, weighing both this floor's own
    // guided-walk interference readings and channels already assigned to APs on nearby floors
    // (weighted down by distance the same way CoverageGridCalculator discounts cross-floor signal).
    // Round-robin assignment across APs still walks this reordered list, so APs keep getting
    // distinct channels — just biased away from whichever channels are already congested, on this
    // floor or the ones around it. With no measurements and no other floors' APs yet, every channel
    // ties at zero and LINQ's stable OrderBy preserves the original order — identical round-robin
    // behavior to before either signal existed.
    //
    // This only sees APs already placed on other floors at the time this floor's own placement
    // runs — since AP suggestion stays a per-floor, user-triggered action (no joint cross-floor
    // optimizer), a floor suggested before its neighbor won't retroactively account for channels
    // the neighbor picks later. Re-running suggestions on an earlier floor after its neighbors are
    // done picks up their channels same as any other re-run.
    private static List<int> RankChannelsByInterference(IReadOnlyList<int> channels, Band band, Floor floor, IReadOnlyList<Floor> allFloors) =>
        channels
            .OrderBy(channel => MeasuredInterferenceScore(channel, band, floor.TestPoints) + CrossFloorChannelUsageScore(channel, band, floor, allFloors))
            .ToList();

    // Linear-power sum (not a dBm average) so a single strong nearby network dominates the score
    // the way it would dominate real co-channel interference.
    private static double MeasuredInterferenceScore(int channel, Band band, IReadOnlyList<TestPoint> testPoints) =>
        testPoints
            .SelectMany(tp => tp.InterferenceReadings)
            .Where(r => r.Band == band && r.Channel == channel)
            .Sum(r => Math.Pow(10, r.SignalDbm / 10.0));

    // Same skip rules as CoverageGridCalculator (same-level-different-floor and outdoor areas
    // don't participate in the flat "stacked at the same origin" cross-floor model), scored in the
    // same linear-power units as MeasuredInterferenceScore so the two combine meaningfully.
    private static double CrossFloorChannelUsageScore(int channel, Band band, Floor floor, IReadOnlyList<Floor> allFloors)
    {
        double score = 0;

        foreach (var otherFloor in allFloors)
        {
            bool sameFloor = otherFloor.Id == floor.Id;
            int levelDifference = Math.Abs(floor.Level - otherFloor.Level);
            if (sameFloor || levelDifference == 0 || floor.IsOutdoor || otherFloor.IsOutdoor)
            {
                continue;
            }

            foreach (var accessPoint in otherFloor.AccessPoints)
            {
                if (accessPoint.Radios.TryGetValue(band, out var radio) && radio.Channel == channel)
                {
                    double leakedSignalDbm = radio.TransmitPowerDbm - (InterFloorAttenuationDbPerLevel * levelDifference);
                    score += Math.Pow(10, leakedSignalDbm / 10.0);
                }
            }
        }

        return score;
    }

    private static double CoverageFraction(bool[] covered) =>
        covered.Length == 0 ? 1.0 : covered.Count(c => c) / (double)covered.Length;
}
