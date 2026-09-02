using Pulsemap.App.Core.Abstractions;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeUpdateCheckService : IUpdateCheckService
{
    public UpdateCheckResult ResultToReturn { get; set; } = UpdateCheckResult.NoUpdate;

    public Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultToReturn);
}
