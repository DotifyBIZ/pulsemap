using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Measurement;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Measurement;

public sealed class MeasurementPointSuggesterTests
{
    private readonly OrdinaryKrigingInterpolator _krigingInterpolator = new();


    [Fact]
    public void SuggestPoints_NoWalls_ReturnsEmpty()
    {
        var floor = new Floor { PlanSource = new RoomListSource() };

        var points = MeasurementPointSuggester.SuggestPoints(floor);

        Assert.Empty(points);
    }

    [Fact]
    public void SuggestPoints_WithWalls_ReturnsGridPoints()
    {
        var floor = SquareRoomFloor(9);

        var points = MeasurementPointSuggester.SuggestPoints(floor);

        Assert.NotEmpty(points);
    }

    [Fact]
    public void SuggestPoints_CandidateNearExistingTestPoint_IsExcluded()
    {
        var floor = SquareRoomFloor(9);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(0, 0) });

        var points = MeasurementPointSuggester.SuggestPoints(floor);

        Assert.DoesNotContain(points, p => p.DistanceTo(new Point2D(0, 0)) < 1.0);
    }

    [Fact]
    public void SuggestPoints_Adaptive_FewerThanTwoMeasurements_MatchesPlainSuggestion()
    {
        var floor = SquareRoomFloor(9);
        var testPoint = new TestPoint { Position = new Point2D(0, 0) };
        testPoint.Measurements[Band.TwoPointFourGhz] = new BandMeasurement { SignalDbm = -50, MeasuredAt = DateTimeOffset.UtcNow };
        floor.TestPoints.Add(testPoint);

        var plain = MeasurementPointSuggester.SuggestPoints(floor);
        var adaptive = MeasurementPointSuggester.SuggestPoints(floor, Band.TwoPointFourGhz, _krigingInterpolator);

        Assert.Equal(plain, adaptive);
    }

    [Fact]
    public void SuggestPoints_Adaptive_OrdersCandidatesByNonIncreasingUncertainty()
    {
        var floor = SquareRoomFloor(30);
        AddMeasurement(floor, new Point2D(0, 0), -50);
        AddMeasurement(floor, new Point2D(30, 0), -52);
        AddMeasurement(floor, new Point2D(0, 30), -48);
        AddMeasurement(floor, new Point2D(30, 30), -60);

        var points = MeasurementPointSuggester.SuggestPoints(floor, Band.TwoPointFourGhz, _krigingInterpolator);

        var samples = floor.TestPoints.Select(tp => new CoverageSample(tp.Position, tp.Measurements[Band.TwoPointFourGhz].SignalDbm)).ToList();
        var variances = _krigingInterpolator.InterpolateVariance(samples, points);

        Assert.True(variances.Count > 1, "Expected more than one candidate point so ordering is actually meaningful.");
        for (int i = 1; i < variances.Count; i++)
        {
            Assert.True(variances[i - 1] >= variances[i] - 1e-9, $"Expected non-increasing uncertainty: index {i - 1} had {variances[i - 1]}, index {i} had {variances[i]}.");
        }
    }

    private static void AddMeasurement(Floor floor, Point2D position, double signalDbm)
    {
        var testPoint = new TestPoint { Position = position };
        testPoint.Measurements[Band.TwoPointFourGhz] = new BandMeasurement { SignalDbm = signalDbm, MeasuredAt = DateTimeOffset.UtcNow };
        floor.TestPoints.Add(testPoint);
    }

    private static Floor SquareRoomFloor(double sizeMeters) => new()
    {
        PlanSource = new RoomListSource(),
        Walls =
        {
            new Wall { Start = new Point2D(0, 0), End = new Point2D(sizeMeters, 0) },
            new Wall { Start = new Point2D(sizeMeters, 0), End = new Point2D(sizeMeters, sizeMeters) },
            new Wall { Start = new Point2D(sizeMeters, sizeMeters), End = new Point2D(0, sizeMeters) },
            new Wall { Start = new Point2D(0, sizeMeters), End = new Point2D(0, 0) },
        },
    };
}
