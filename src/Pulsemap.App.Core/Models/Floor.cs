namespace Pulsemap.App.Core.Models;

public sealed class Floor
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; set; } = "Floor 1";

    /// <summary>An open-air area (parking lot, courtyard, ...) instead of an indoor floor — uses
    /// free-space propagation (an empty <see cref="Walls"/> list already produces this with no
    /// engine changes) and, since there are no walls to derive a bounding box from,
    /// <see cref="OutdoorBoundsMin"/>/<see cref="OutdoorBoundsMax"/> for its candidate-grid extent
    /// instead.</summary>
    public bool IsOutdoor { get; set; }

    /// <summary>Which physical level this is, relative to 0 — used only to estimate inter-floor
    /// signal leakage (floors are assumed to stack at the same local X/Y origin); not a real
    /// height/elevation.</summary>
    public int Level { get; set; }

    public Point2D? OutdoorBoundsMin { get; set; }

    public Point2D? OutdoorBoundsMax { get; set; }

    public required FloorPlanSource PlanSource { get; set; }

    public List<Wall> Walls { get; init; } = [];

    public List<TestPoint> TestPoints { get; init; } = [];

    public List<AccessPoint> AccessPoints { get; init; } = [];
}
