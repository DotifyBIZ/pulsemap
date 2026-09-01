using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Placement;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Tests.Placement;

public sealed class GreedyCoverageApPlacementOptimizerTests
{
    private readonly GreedyCoverageApPlacementOptimizer _sut = new();
    private readonly LogDistancePropagationModel _propagationModel = new();

    [Fact]
    public void SuggestPlacements_SingleSmallRoom_SuggestsExactlyOneAp()
    {
        var floor = SquareRoomFloor(sizeMeters: 10);

        var placements = _sut.SuggestPlacements(floor, [Band.TwoPointFourGhz], _propagationModel);

        Assert.Single(placements);
    }

    [Fact]
    public void SuggestPlacements_ThreeConcreteWalledRoomsAtSixGhz_SuggestsMoreThanOneAp()
    {
        var placements = _sut.SuggestPlacements(ThreeConcreteWalledRoomsFloor(), [Band.SixGhz], _propagationModel);

        Assert.True(placements.Count > 1, $"Expected more than one AP across three concrete-divided rooms at 6GHz, got {placements.Count}.");
    }

    [Fact]
    public void SuggestPlacements_MultipleBandsRequested_EveryPlacementHasRadiosForEachBand()
    {
        var floor = SquareRoomFloor(sizeMeters: 10);
        Band[] bands = [Band.TwoPointFourGhz, Band.FiveGhz, Band.SixGhz];

        var placements = _sut.SuggestPlacements(floor, bands, _propagationModel);

        Assert.NotEmpty(placements);
        foreach (var ap in placements)
        {
            foreach (var band in bands)
            {
                Assert.True(ap.Radios.ContainsKey(band), $"AP '{ap.Label}' is missing radio settings for {band}.");
            }
        }
    }

    [Fact]
    public void SuggestPlacements_MultipleAps_AssignsDistinctChannelsPerBand()
    {
        var placements = _sut.SuggestPlacements(ThreeConcreteWalledRoomsFloor(), [Band.SixGhz], _propagationModel);

        Assert.True(placements.Count > 1, "Test requires more than one AP to be meaningful.");
        var channels = placements.Select(ap => ap.Radios[Band.SixGhz].Channel).ToList();
        Assert.Equal(channels.Count, channels.Distinct().Count());
    }

    [Fact]
    public void SuggestPlacements_NoWalls_ReturnsEmpty()
    {
        var floor = new Floor { PlanSource = new RoomListSource() };

        var placements = _sut.SuggestPlacements(floor, [Band.TwoPointFourGhz], _propagationModel);

        Assert.Empty(placements);
    }

    [Fact]
    public void SuggestPlacements_ChannelWithMeasuredInterference_IsDeprioritized()
    {
        var floor = SquareRoomFloor(sizeMeters: 10);
        floor.TestPoints.Add(new TestPoint
        {
            Position = new Point2D(5, 5),
            InterferenceReadings =
            [
                new WlanNetworkReading("NeighborNet", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 1, -40),
            ],
        });

        var placements = _sut.SuggestPlacements(floor, [Band.TwoPointFourGhz], _propagationModel);

        Assert.Single(placements);
        Assert.NotEqual(1, placements[0].Radios[Band.TwoPointFourGhz].Channel);
    }

    [Fact]
    public void SuggestPlacements_NoMeasurements_KeepsDefaultChannelOrder()
    {
        var floor = SquareRoomFloor(sizeMeters: 10);

        var placements = _sut.SuggestPlacements(floor, [Band.TwoPointFourGhz], _propagationModel);

        Assert.Single(placements);
        Assert.Equal(1, placements[0].Radios[Band.TwoPointFourGhz].Channel);
    }

    // Three 10x10m rooms in a row, separated by concrete dividing walls — enough attenuation at
    // 6GHz that a single AP in one room shouldn't reliably reach the far room.
    private static Floor ThreeConcreteWalledRoomsFloor() => new()
    {
        PlanSource = new RoomListSource(),
        Walls =
        {
            new Wall { Start = new Point2D(0, 0), End = new Point2D(30, 0), Material = WallMaterial.Concrete, ThicknessMeters = 0.15 },
            new Wall { Start = new Point2D(30, 0), End = new Point2D(30, 10), Material = WallMaterial.Concrete, ThicknessMeters = 0.15 },
            new Wall { Start = new Point2D(30, 10), End = new Point2D(0, 10), Material = WallMaterial.Concrete, ThicknessMeters = 0.15 },
            new Wall { Start = new Point2D(0, 10), End = new Point2D(0, 0), Material = WallMaterial.Concrete, ThicknessMeters = 0.15 },
            new Wall { Start = new Point2D(10, 0), End = new Point2D(10, 10), Material = WallMaterial.ReinforcedConcrete, ThicknessMeters = 0.2 },
            new Wall { Start = new Point2D(20, 0), End = new Point2D(20, 10), Material = WallMaterial.ReinforcedConcrete, ThicknessMeters = 0.2 },
        },
    };

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
