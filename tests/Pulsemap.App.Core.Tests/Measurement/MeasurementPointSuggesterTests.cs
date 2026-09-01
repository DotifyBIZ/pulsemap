using Pulsemap.App.Core.Measurement;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Measurement;

public sealed class MeasurementPointSuggesterTests
{
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
