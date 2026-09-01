using Pulsemap.App.Core.Abstractions;

namespace Pulsemap.App.Core.Models;

public sealed class TestPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Point2D Position { get; set; }

    /// <summary>Live measurements taken at this point, keyed by band. A band the chosen adapter can't see is simply absent — never a false zero-signal reading.</summary>
    public Dictionary<Band, BandMeasurement> Measurements { get; init; } = [];

    /// <summary>Every other network observed at this point during a guided walk capture — the full
    /// BSS list, not just the target network — so channel planning can see real co-channel and
    /// adjacent-channel interference.</summary>
    public List<WlanNetworkReading> InterferenceReadings { get; init; } = [];
}
