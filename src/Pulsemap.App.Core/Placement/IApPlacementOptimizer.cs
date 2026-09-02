using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Placement;

public interface IApPlacementOptimizer
{
    /// <summary>Suggests AP count and placement for a floor. Always a starting point — every result is user-overridable, never authoritative.
    /// <paramref name="allFloors"/> lets channel selection avoid reusing a channel a nearby floor's AP already has — placement itself stays per-floor-only (see GreedyCoverageApPlacementOptimizer's remarks).</summary>
    IReadOnlyList<AccessPoint> SuggestPlacements(Floor floor, IReadOnlyList<Floor> allFloors, IReadOnlyList<Band> bands, IPropagationModel propagationModel);
}
