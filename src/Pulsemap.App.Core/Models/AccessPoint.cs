namespace Pulsemap.App.Core.Models;

public sealed class AccessPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Point2D Position { get; set; }

    public required string Label { get; set; }

    /// <summary>True once a user has moved, added, or edited this AP by hand — placement suggestions never overwrite it again.</summary>
    public bool IsUserOverride { get; set; }

    public Dictionary<Band, BandRadioSettings> Radios { get; init; } = [];
}
