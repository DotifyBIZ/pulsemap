using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Propagation;

/// <summary>
/// Regular grid of candidate points spanning a floor's wall extents, at a caller-chosen spacing.
/// Shared by coverage sampling, AP placement candidates, and measurement-point suggestion — they
/// only differ in how far apart the points need to be.
/// </summary>
public static class FloorGrid
{
    public static List<Point2D> BuildPoints(Floor floor, double spacingMeters)
    {
        ArgumentNullException.ThrowIfNull(floor);

        if (spacingMeters <= 0 || !double.IsFinite(spacingMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(spacingMeters), spacingMeters, "Grid spacing must be a positive, finite value.");
        }

        if (floor.Walls.Count == 0)
        {
            return [];
        }

        double minX = floor.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Min();
        double maxX = floor.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Max();
        double minY = floor.Walls.SelectMany(w => new[] { w.Start.Y, w.End.Y }).Min();
        double maxY = floor.Walls.SelectMany(w => new[] { w.Start.Y, w.End.Y }).Max();

        var points = new List<Point2D>();
        for (double x = minX; x <= maxX; x += spacingMeters)
        {
            for (double y = minY; y <= maxY; y += spacingMeters)
            {
                points.Add(new Point2D(x, y));
            }
        }

        return points;
    }
}
