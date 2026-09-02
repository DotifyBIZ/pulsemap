namespace Pulsemap.App.Core.Models;

/// <summary>A frozen copy of a survey's floors at a point in time, for before/after comparison.
/// Only geometry and measurements are frozen (<see cref="Floors"/>' walls/test points/access
/// points) — a floor's plan image is not duplicated per snapshot; comparison renders a snapshot's
/// data over the *current* floor's background image.</summary>
public sealed class SurveySnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Label { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public required List<Floor> Floors { get; set; }
}
