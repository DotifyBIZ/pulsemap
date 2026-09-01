using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Placement;

public interface IApPlacementOptimizer
{
    /// <summary>Suggests AP count and placement for a floor. Always a starting point — every result is user-overridable, never authoritative.</summary>
    IReadOnlyList<AccessPoint> SuggestPlacements(Floor floor, IReadOnlyList<Band> bands, IPropagationModel propagationModel);
}
