using Pulsemap.App.Core.Abstractions;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeLinkDiagnosticsService : ILinkDiagnosticsService
{
    public LinkDiagnosticsSnapshot SnapshotToReturn { get; set; } = LinkDiagnosticsSnapshot.Disconnected;

    public int CallCount { get; private set; }

    public Task<LinkDiagnosticsSnapshot> GetCurrentLinkAsync(Guid adapterId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(SnapshotToReturn);
    }
}
