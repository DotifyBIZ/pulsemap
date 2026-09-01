namespace Pulsemap.App.Core.Models;

public sealed class Floor
{
    public required FloorPlanSource PlanSource { get; set; }

    public List<Wall> Walls { get; init; } = [];

    public List<TestPoint> TestPoints { get; init; } = [];

    public List<AccessPoint> AccessPoints { get; init; } = [];
}
