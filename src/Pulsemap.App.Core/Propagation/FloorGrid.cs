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
            // No walls to derive a bounding box from — an outdoor area supplies its extent
            // explicitly instead, since it has no walls by definition. An indoor floor with no
            // walls drawn yet still has no candidate grid.
            return floor.IsOutdoor && floor.OutdoorBoundsMin is { } outdoorMin && floor.OutdoorBoundsMax is { } outdoorMax
                ? BuildGrid(outdoorMin.X, outdoorMax.X, outdoorMin.Y, outdoorMax.Y, spacingMeters)
                : [];
        }

        double minX = floor.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Min();
        double maxX = floor.Walls.SelectMany(w => new[] { w.Start.X, w.End.X }).Max();
        double minY = floor.Walls.SelectMany(w => new[] { w.Start.Y, w.End.Y }).Min();
        double maxY = floor.Walls.SelectMany(w => new[] { w.Start.Y, w.End.Y }).Max();

        return BuildGrid(minX, maxX, minY, maxY, spacingMeters);
    }

    /// <summary>
    /// Ceiling on how many candidate points any one grid may contain. Every consumer of this grid
    /// is at least O(points) — AP placement is O(points²) — and all of them run while the user
    /// waits, so an accidentally huge extent (a wall dragged to an absurd coordinate, a floor plan
    /// with a tiny pixels-per-meter scale) would otherwise turn into an unbounded allocation and a
    /// frozen app rather than a coarse heatmap. Past this point the spacing is widened instead:
    /// the result gets less detailed, never unbounded. 250k at 0.5m spacing still covers roughly a
    /// 250m x 250m floor at full requested resolution.
    /// </summary>
    public const int MaxGridPoints = 250_000;

    private static List<Point2D> BuildGrid(double minX, double maxX, double minY, double maxY, double spacingMeters)
    {
        spacingMeters = ClampSpacingToMaxPoints(maxX - minX, maxY - minY, spacingMeters);

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

    private static double ClampSpacingToMaxPoints(double widthMeters, double heightMeters, double spacingMeters)
    {
        double width = Math.Max(widthMeters, 0);
        double height = Math.Max(heightMeters, 0);

        // Scaling by the square root of the overshoot is the factor that brings a two-dimensional
        // point count back under the ceiling; the loop only re-runs because the per-axis "+1"
        // (the grid includes both endpoints) can leave it fractionally over after one pass.
        while (PointCount(width, height, spacingMeters) > MaxGridPoints)
        {
            spacingMeters *= Math.Sqrt(PointCount(width, height, spacingMeters) / MaxGridPoints);
        }

        return spacingMeters;
    }

    private static double PointCount(double width, double height, double spacingMeters) =>
        (Math.Floor(width / spacingMeters) + 1) * (Math.Floor(height / spacingMeters) + 1);
}
