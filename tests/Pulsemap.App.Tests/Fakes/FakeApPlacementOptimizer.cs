using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Placement;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeApPlacementOptimizer : IApPlacementOptimizer
{
    public IReadOnlyList<AccessPoint> PlacementsToReturn { get; set; } = [];

    public IReadOnlyList<AccessPoint> SuggestPlacements(Floor floor, IReadOnlyList<Band> bands, IPropagationModel propagationModel) =>
        PlacementsToReturn;
}
