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
}
