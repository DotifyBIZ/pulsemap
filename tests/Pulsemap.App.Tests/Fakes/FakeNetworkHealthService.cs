using Pulsemap.App.Core.Abstractions;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeNetworkHealthService : INetworkHealthService
{
    public NetworkHealthSnapshot SnapshotToReturn { get; set; } = NetworkHealthSnapshot.Unavailable;

    public Task<NetworkHealthSnapshot> CheckHealthAsync(Guid adapterId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SnapshotToReturn);
}
