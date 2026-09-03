using Pulsemap.App.Services;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeFloorPlanFilePickerService : IFloorPlanFilePickerService
{
    public FloorPlanFilePickResult? ResultToReturn { get; set; }

    public Exception? ExceptionToThrow { get; set; }

    public Task<FloorPlanFilePickResult?> PickFloorPlanFileAsync(CancellationToken cancellationToken = default) =>
        ExceptionToThrow is not null ? Task.FromException<FloorPlanFilePickResult?>(ExceptionToThrow) : Task.FromResult(ResultToReturn);
}
