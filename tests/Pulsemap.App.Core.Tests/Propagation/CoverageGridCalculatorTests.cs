using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Tests.Propagation;

public sealed class CoverageGridCalculatorTests
{
    private readonly LogDistancePropagationModel _propagationModel = new();

    [Fact]
    public void ComputeGrid_NoWalls_ReturnsEmpty()
    {
        var floor = new Floor { PlanSource = new RoomListSource() };
        floor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(1, 1)));

        var samples = CoverageGridCalculator.ComputeGrid(floor, [floor], Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_NoAccessPoints_ReturnsEmpty()
    {
        var floor = SquareRoomFloor(10);

        var samples = CoverageGridCalculator.ComputeGrid(floor, [floor], Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_PointCloserToAp_HasStrongerSignalThanFartherPoint()
    {
        var floor = SquareRoomFloor(20);
        floor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(0, 0)));

        var samples = CoverageGridCalculator.ComputeGrid(floor, [floor], Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        double near = samples.Single(s => s.Position == new Point2D(2, 0)).ValueDbm;
        double far = samples.Single(s => s.Position == new Point2D(20, 0)).ValueDbm;
        Assert.True(near > far, $"Expected the closer point to read stronger: near={near}dBm, far={far}dBm.");
    }

    [Fact]
    public void ComputeGrid_TwoAps_CellNearSecondApReflectsSecondAp()
    {
        var floor = SquareRoomFloor(30);
        floor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(0, 0)));
        floor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(30, 0)));

        var samples = CoverageGridCalculator.ComputeGrid(floor, [floor], Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        double nearSecondAp = samples.Single(s => s.Position == new Point2D(30, 0)).ValueDbm;
        double fromNearestApOnly = _propagationModel.PredictSignalDbm(new Point2D(30, 0), 17, new Point2D(30, 0), Band.TwoPointFourGhz, floor.Walls);
        Assert.Equal(fromNearestApOnly, nearSecondAp, precision: 6);
    }

    [Fact]
    public void ComputeGrid_ApWithoutRadioForRequestedBand_IsIgnored()
    {
        var floor = SquareRoomFloor(10);
        var apWithoutBand = new AccessPoint { Position = new Point2D(0, 0), Label = "AP 1" };
        apWithoutBand.Radios[Band.FiveGhz] = new BandRadioSettings { TransmitPowerDbm = 20, Channel = 36 };
        floor.AccessPoints.Add(apWithoutBand);

        var samples = CoverageGridCalculator.ComputeGrid(floor, [floor], Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_ApOnFloorAboveAtSameXY_ContributesAttenuatedSignal()
    {
        var groundFloor = SquareRoomFloor(10);
        groundFloor.Level = 0;
        var upperFloor = SquareRoomFloor(10);
        upperFloor.Level = 1;
        upperFloor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(5, 5)));
        List<Floor> allFloors = [groundFloor, upperFloor];

        var samples = CoverageGridCalculator.ComputeGrid(groundFloor, allFloors, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        var atApXY = samples.Single(s => s.Position == new Point2D(4, 4));
        double sameFloorSignal = _propagationModel.PredictSignalDbm(new Point2D(5, 5), 17, new Point2D(4, 4), Band.TwoPointFourGhz, []);
        Assert.True(atApXY.ValueDbm < sameFloorSignal, "Expected the cross-floor reading to be weaker than an equivalent same-floor reading, since a per-level penalty should apply.");
    }

    [Fact]
    public void ComputeGrid_DifferentFloorAtSameLevel_DoesNotContribute()
    {
        var floorA = SquareRoomFloor(10);
        floorA.Level = 0;
        var floorB = SquareRoomFloor(10);
        floorB.Level = 0;
        floorB.AccessPoints.Add(SingleBandAccessPoint(new Point2D(5, 5)));
        List<Floor> allFloors = [floorA, floorB];

        var samples = CoverageGridCalculator.ComputeGrid(floorA, allFloors, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_OutdoorFloor_IgnoresApsOnOtherLevels()
    {
        var outdoorFloor = new Floor
        {
            PlanSource = new RoomListSource(),
            IsOutdoor = true,
            Level = 0,
            OutdoorBoundsMin = new Point2D(0, 0),
            OutdoorBoundsMax = new Point2D(10, 10),
        };
        var indoorFloor = SquareRoomFloor(10);
        indoorFloor.Level = 1;
        indoorFloor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(5, 5)));
        List<Floor> allFloors = [outdoorFloor, indoorFloor];

        var samples = CoverageGridCalculator.ComputeGrid(outdoorFloor, allFloors, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_OutdoorFloorWithOwnAp_ProducesSamples()
    {
        var outdoorFloor = new Floor
        {
            PlanSource = new RoomListSource(),
            IsOutdoor = true,
            OutdoorBoundsMin = new Point2D(0, 0),
            OutdoorBoundsMax = new Point2D(10, 10),
        };
        outdoorFloor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(5, 5)));

        var samples = CoverageGridCalculator.ComputeGrid(outdoorFloor, [outdoorFloor], Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.NotEmpty(samples);
    }

    private static AccessPoint SingleBandAccessPoint(Point2D position)
    {
        var accessPoint = new AccessPoint { Position = position, Label = "AP" };
        accessPoint.Radios[Band.TwoPointFourGhz] = new BandRadioSettings { TransmitPowerDbm = 17, Channel = 1 };
        return accessPoint;
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
