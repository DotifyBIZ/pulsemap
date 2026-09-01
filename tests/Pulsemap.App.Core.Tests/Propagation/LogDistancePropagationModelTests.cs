using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Tests.Propagation;

public sealed class LogDistancePropagationModelTests
{
    private readonly LogDistancePropagationModel _sut = new();

    [Theory]
    [InlineData(10, Band.TwoPointFourGhz, 60.04)] // 20log10(10) + 20log10(2.4) + 32.44
    [InlineData(10, Band.FiveGhz, 66.42)] // 20log10(10) + 20log10(5.0) + 32.44
    [InlineData(1, Band.TwoPointFourGhz, 40.04)] // 20log10(1) + 20log10(2.4) + 32.44
    public void PredictSignalDbm_NoWalls_MatchesFriisFreeSpaceFormula(double distanceMeters, Band band, double expectedLossDb)
    {
        const double transmitPowerDbm = 20;
        var transmitter = new Point2D(0, 0);
        var receiver = new Point2D(distanceMeters, 0);

        double signal = _sut.PredictSignalDbm(transmitter, transmitPowerDbm, receiver, band, []);

        Assert.Equal(transmitPowerDbm - expectedLossDb, signal, precision: 1);
    }

    [Fact]
    public void PredictSignalDbm_WallCrossingDirectPath_AddsMaterialAttenuation()
    {
        var transmitter = new Point2D(0, 0);
        var receiver = new Point2D(20, 0);
        var crossingWall = new Wall { Start = new Point2D(10, -5), End = new Point2D(10, 5), Material = WallMaterial.Concrete };

        double signalWithoutWall = _sut.PredictSignalDbm(transmitter, 20, receiver, Band.FiveGhz, []);
        double signalWithWall = _sut.PredictSignalDbm(transmitter, 20, receiver, Band.FiveGhz, [crossingWall]);

        double expectedConcreteAttenuation = WallAttenuationTable.GetAttenuationDb(WallMaterial.Concrete, thicknessMeters: null, Band.FiveGhz);
        Assert.Equal(signalWithoutWall - expectedConcreteAttenuation, signalWithWall, precision: 6);
    }

    [Fact]
    public void PredictSignalDbm_WallNotCrossingDirectPath_AddsNoAttenuation()
    {
        var transmitter = new Point2D(0, 0);
        var receiver = new Point2D(20, 0);
        var parallelWall = new Wall { Start = new Point2D(0, 10), End = new Point2D(20, 10), Material = WallMaterial.Concrete };

        double signalWithoutWall = _sut.PredictSignalDbm(transmitter, 20, receiver, Band.FiveGhz, []);
        double signalWithWall = _sut.PredictSignalDbm(transmitter, 20, receiver, Band.FiveGhz, [parallelWall]);

        Assert.Equal(signalWithoutWall, signalWithWall, precision: 9);
    }

    [Fact]
    public void PredictSignalDbm_MultipleCrossingWalls_SumsAttenuation()
    {
        var transmitter = new Point2D(0, 0);
        var receiver = new Point2D(30, 0);
        var walls = new List<Wall>
        {
            new() { Start = new Point2D(10, -5), End = new Point2D(10, 5), Material = WallMaterial.Drywall },
            new() { Start = new Point2D(20, -5), End = new Point2D(20, 5), Material = WallMaterial.Drywall },
        };

        double signalOneWall = _sut.PredictSignalDbm(transmitter, 20, receiver, Band.TwoPointFourGhz, [walls[0]]);
        double signalTwoWalls = _sut.PredictSignalDbm(transmitter, 20, receiver, Band.TwoPointFourGhz, walls);

        double drywallAttenuation = WallAttenuationTable.GetAttenuationDb(WallMaterial.Drywall, thicknessMeters: null, Band.TwoPointFourGhz);
        Assert.Equal(signalOneWall - drywallAttenuation, signalTwoWalls, precision: 6);
    }
}
