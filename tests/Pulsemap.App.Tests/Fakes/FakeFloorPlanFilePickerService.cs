using Pulsemap.App.Services;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeFloorPlanFilePickerService : IFloorPlanFilePickerService
{
    public FloorPlanFilePickResult? ResultToReturn { get; set; }

    public Task<FloorPlanFilePickResult?> PickFloorPlanFileAsync() => Task.FromResult(ResultToReturn);
}
