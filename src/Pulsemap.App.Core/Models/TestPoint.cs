namespace Pulsemap.App.Core.Models;

public sealed class TestPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Point2D Position { get; set; }

    /// <summary>Live measurements taken at this point, keyed by band. A band the chosen adapter can't see is simply absent — never a false zero-signal reading.</summary>
    public Dictionary<Band, BandMeasurement> Measurements { get; init; } = [];
}
