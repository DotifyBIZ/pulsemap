namespace Pulsemap.App.Core.Models;

public sealed class Wall
{
    public required Point2D Start { get; set; }

    public required Point2D End { get; set; }

    /// <summary>Null when the user hasn't specified a material — propagation falls back to a flat per-wall penalty.</summary>
    public WallMaterial? Material { get; set; }

    public double? ThicknessMeters { get; set; }
}
