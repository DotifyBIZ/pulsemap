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

        var samples = CoverageGridCalculator.ComputeGrid(floor, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_NoAccessPoints_ReturnsEmpty()
    {
        var floor = SquareRoomFloor(10);

        var samples = CoverageGridCalculator.ComputeGrid(floor, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
    }

    [Fact]
    public void ComputeGrid_PointCloserToAp_HasStrongerSignalThanFartherPoint()
    {
        var floor = SquareRoomFloor(20);
        floor.AccessPoints.Add(SingleBandAccessPoint(new Point2D(0, 0)));

        var samples = CoverageGridCalculator.ComputeGrid(floor, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

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

        var samples = CoverageGridCalculator.ComputeGrid(floor, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

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

        var samples = CoverageGridCalculator.ComputeGrid(floor, Band.TwoPointFourGhz, gridSpacingMeters: 2, _propagationModel);

        Assert.Empty(samples);
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
