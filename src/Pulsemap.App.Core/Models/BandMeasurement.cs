namespace Pulsemap.App.Core.Models;

public sealed class BandMeasurement
{
    public required double SignalDbm { get; set; }

    public required DateTimeOffset MeasuredAt { get; set; }

    public string? AdapterName { get; set; }
}
