using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Measurement;

/// <summary>
/// Turns one WLAN scan into a TestPoint's measurement data — the pure logic behind the guided
/// walk's "confirm arrival and capture" step, kept here so it's testable without a WinUI host.
/// </summary>
public static class TestPointCapture
{
    public static TestPoint BuildTestPoint(Point2D position, WlanScanResult scanResult, Survey survey, string? adapterName, DateTimeOffset measuredAt)
    {
        ArgumentNullException.ThrowIfNull(scanResult);
        ArgumentNullException.ThrowIfNull(survey);

        var testPoint = new TestPoint
        {
            Position = position,
            InterferenceReadings = [.. scanResult.Networks],
        };

        if (survey.Type != SurveyType.ExistingNetworkAudit || string.IsNullOrWhiteSpace(survey.TargetNetworkSsid))
        {
            return testPoint;
        }

        foreach (var band in survey.TargetBands)
        {
            var strongest = scanResult.Networks
                .Where(n => n.Band == band && string.Equals(n.Ssid, survey.TargetNetworkSsid, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(n => n.SignalDbm)
                .FirstOrDefault();

            if (strongest is not null)
            {
                testPoint.Measurements[band] = new BandMeasurement
                {
                    SignalDbm = strongest.SignalDbm,
                    MeasuredAt = measuredAt,
                    AdapterName = adapterName,
                };
            }
        }

        return testPoint;
    }
}
