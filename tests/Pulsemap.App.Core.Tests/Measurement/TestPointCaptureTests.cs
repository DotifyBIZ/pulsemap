using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Measurement;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Measurement;

public sealed class TestPointCaptureTests
{
    private static readonly Point2D Position = new(1, 2);
    private static readonly DateTimeOffset MeasuredAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BuildTestPoint_NewDeploymentSurvey_CapturesInterferenceButNoMeasurements()
    {
        var survey = BuildSurvey(SurveyType.NewDeployment, targetSsid: null, [Band.TwoPointFourGhz]);
        var scan = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("SomeNetwork", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 1, -50),
        ]);

        var testPoint = TestPointCapture.BuildTestPoint(Position, scan, survey, "Adapter", MeasuredAt);

        Assert.Empty(testPoint.Measurements);
        Assert.Single(testPoint.InterferenceReadings);
    }

    [Fact]
    public void BuildTestPoint_AuditWithMatchingSsidAndBand_CapturesStrongestReadingAsMeasurement()
    {
        var survey = BuildSurvey(SurveyType.ExistingNetworkAudit, "OfficeNet", [Band.FiveGhz]);
        var scan = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("OfficeNet", "AA:AA:AA:AA:AA:AA", Band.FiveGhz, 36, -65),
            new WlanNetworkReading("OfficeNet", "BB:BB:BB:BB:BB:BB", Band.FiveGhz, 40, -50),
            new WlanNetworkReading("NeighborNet", "CC:CC:CC:CC:CC:CC", Band.FiveGhz, 44, -40),
        ]);

        var testPoint = TestPointCapture.BuildTestPoint(Position, scan, survey, "Adapter", MeasuredAt);

        Assert.Equal(-50, testPoint.Measurements[Band.FiveGhz].SignalDbm);
        Assert.Equal(MeasuredAt, testPoint.Measurements[Band.FiveGhz].MeasuredAt);
        Assert.Equal(3, testPoint.InterferenceReadings.Count);
    }

    [Fact]
    public void BuildTestPoint_AuditSsidMatchIsCaseInsensitive()
    {
        var survey = BuildSurvey(SurveyType.ExistingNetworkAudit, "OfficeNet", [Band.TwoPointFourGhz]);
        var scan = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("officenet", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 6, -55),
        ]);

        var testPoint = TestPointCapture.BuildTestPoint(Position, scan, survey, "Adapter", MeasuredAt);

        Assert.Equal(-55, testPoint.Measurements[Band.TwoPointFourGhz].SignalDbm);
    }

    [Fact]
    public void BuildTestPoint_AuditTargetSsidNotSeenForBand_LeavesThatBandAbsent()
    {
        var survey = BuildSurvey(SurveyType.ExistingNetworkAudit, "OfficeNet", [Band.TwoPointFourGhz, Band.FiveGhz]);
        var scan = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("OfficeNet", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 6, -55),
        ]);

        var testPoint = TestPointCapture.BuildTestPoint(Position, scan, survey, "Adapter", MeasuredAt);

        Assert.True(testPoint.Measurements.ContainsKey(Band.TwoPointFourGhz));
        Assert.False(testPoint.Measurements.ContainsKey(Band.FiveGhz));
    }

    [Fact]
    public void BuildTestPoint_AuditWithNoTargetSsidSet_CapturesNoMeasurements()
    {
        var survey = BuildSurvey(SurveyType.ExistingNetworkAudit, targetSsid: null, [Band.TwoPointFourGhz]);
        var scan = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("OfficeNet", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 6, -55),
        ]);

        var testPoint = TestPointCapture.BuildTestPoint(Position, scan, survey, "Adapter", MeasuredAt);

        Assert.Empty(testPoint.Measurements);
    }

    private static Survey BuildSurvey(SurveyType type, string? targetSsid, List<Band> targetBands) => new()
    {
        Name = "Test Survey",
        Type = type,
        TargetNetworkSsid = targetSsid,
        TargetBands = targetBands,
        Floors = [new Floor { PlanSource = new RoomListSource() }],
    };
}
