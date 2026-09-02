using System.Text;
using System.Text.Json;
using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Export;

public sealed class SurveyDataExporterTests
{
    private readonly SurveyDataExporter _sut = new();

    [Fact]
    public async Task ExportTestPointsCsvAsync_WritesOneRowPerBandMeasurement()
    {
        var survey = SurveyWithMeasuredTestPoint();

        using var stream = new MemoryStream();
        await _sut.ExportTestPointsCsvAsync(survey, stream);

        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("FloorName,TestPointId,X,Y,Band,SignalDbm,MeasuredAt,AdapterName", lines[0].TrimEnd('\r'));
        Assert.Equal(2, lines.Length); // header + one measurement row
        Assert.Contains("TwoPointFourGhz", lines[1]);
        Assert.Contains("-42", lines[1]);
    }

    [Fact]
    public async Task ExportAccessPointsCsvAsync_WritesOneRowPerRadio()
    {
        var survey = SurveyWithAccessPoint();

        using var stream = new MemoryStream();
        await _sut.ExportAccessPointsCsvAsync(survey, stream);

        var lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("FloorName,AccessPointId,Label,X,Y,Band,TransmitPowerDbm,Channel,IsUserOverride", lines[0].TrimEnd('\r'));
        Assert.Equal(3, lines.Length); // header + 2 radios
    }

    [Fact]
    public async Task ExportJsonAsync_ProducesJsonThatDeserializesBackToEquivalentSurvey()
    {
        var survey = SurveyWithAccessPoint();

        using var stream = new MemoryStream();
        await _sut.ExportJsonAsync(survey, stream);
        stream.Position = 0;

        var deserialized = await JsonSerializer.DeserializeAsync<Survey>(stream);

        Assert.NotNull(deserialized);
        Assert.Equal(survey.Id, deserialized!.Id);
        Assert.Equal(survey.Name, deserialized.Name);
        Assert.Single(deserialized.Floors[0].AccessPoints);
    }

    private static Survey SurveyWithMeasuredTestPoint()
    {
        var testPoint = new TestPoint { Position = new Point2D(3, 4) };
        testPoint.Measurements[Band.TwoPointFourGhz] = new BandMeasurement { SignalDbm = -42, MeasuredAt = DateTimeOffset.UtcNow, AdapterName = "Intel AX210" };

        return new Survey
        {
            Name = "Test Survey",
            Type = SurveyType.ExistingNetworkAudit,
            TargetBands = [Band.TwoPointFourGhz],
            Floors = [new Floor { PlanSource = new RoomListSource(), TestPoints = { testPoint } }],
        };
    }

    private static Survey SurveyWithAccessPoint()
    {
        var accessPoint = new AccessPoint { Position = new Point2D(5, 5), Label = "AP 1" };
        accessPoint.Radios[Band.TwoPointFourGhz] = new BandRadioSettings { TransmitPowerDbm = 17, Channel = 6 };
        accessPoint.Radios[Band.FiveGhz] = new BandRadioSettings { TransmitPowerDbm = 20, Channel = 36 };

        return new Survey
        {
            Name = "Test Survey",
            Type = SurveyType.NewDeployment,
            TargetBands = [Band.TwoPointFourGhz],
            Floors = [new Floor { PlanSource = new RoomListSource(), AccessPoints = { accessPoint } }],
        };
    }
}
