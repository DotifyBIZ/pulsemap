using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Tests.Propagation;

public sealed class FloorGridTests
{
    [Fact]
    public void BuildPoints_IndoorFloorWithNoWalls_ReturnsEmpty()
    {
        var floor = new Floor { PlanSource = new RoomListSource() };

        var points = FloorGrid.BuildPoints(floor, spacingMeters: 2);

        Assert.Empty(points);
    }

    [Fact]
    public void BuildPoints_OutdoorFloorWithNoBounds_ReturnsEmpty()
    {
        var floor = new Floor { PlanSource = new RoomListSource(), IsOutdoor = true };

        var points = FloorGrid.BuildPoints(floor, spacingMeters: 2);

        Assert.Empty(points);
    }

    [Fact]
    public void BuildPoints_OutdoorFloorWithBounds_ReturnsGridWithinBounds()
    {
        var floor = new Floor
        {
            PlanSource = new RoomListSource(),
            IsOutdoor = true,
            OutdoorBoundsMin = new Point2D(0, 0),
            OutdoorBoundsMax = new Point2D(10, 6),
        };

        var points = FloorGrid.BuildPoints(floor, spacingMeters: 2);

        Assert.NotEmpty(points);
        Assert.All(points, p => Assert.InRange(p.X, 0, 10));
        Assert.All(points, p => Assert.InRange(p.Y, 0, 6));
    }

    // A wall dragged to an absurd coordinate (or a corrupt file) used to produce a grid whose
    // point count grew without limit, allocating until the app froze or died. The spacing widens
    // instead: the result is coarse, never unbounded.
    [Fact]
    public void BuildPoints_ExtentFarTooLargeForTheRequestedSpacing_WidensSpacingInsteadOfAllocatingWithoutLimit()
    {
        var floor = new Floor
        {
            PlanSource = new RoomListSource(),
            Walls =
            {
                new Wall { Start = new Point2D(0, 0), End = new Point2D(50_000, 50_000) },
            },
        };

        var points = FloorGrid.BuildPoints(floor, spacingMeters: 0.5);

        Assert.NotEmpty(points);
        Assert.True(points.Count <= FloorGrid.MaxGridPoints, $"Expected at most {FloorGrid.MaxGridPoints} points, got {points.Count}.");
    }

    [Fact]
    public void BuildPoints_ExtentWithinTheCeiling_KeepsTheRequestedSpacingExactly()
    {
        var floor = new Floor
        {
            PlanSource = new RoomListSource(),
            Walls =
            {
                new Wall { Start = new Point2D(0, 0), End = new Point2D(10, 10) },
            },
        };

        var points = FloorGrid.BuildPoints(floor, spacingMeters: 0.5);

        // 21 x 21, inclusive of both endpoints — untouched by the ceiling.
        Assert.Equal(441, points.Count);
    }
}
