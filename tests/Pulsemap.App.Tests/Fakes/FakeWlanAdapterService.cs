using Pulsemap.App.Core.Abstractions;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeWlanAdapterService : IWlanAdapterService
{
    public IReadOnlyList<NetworkAdapterInfo> AdaptersToReturn { get; set; } = [];

    public Queue<WlanScanResult> ScanResultsQueue { get; } = new();

    public WlanScanResult DefaultScanResult { get; set; } = new(WlanScanStatus.Success, []);

    public int ScanCallCount { get; private set; }

    public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AdaptersToReturn);

    public Task<WlanScanResult> ScanAsync(Guid adapterId, CancellationToken cancellationToken = default)
    {
        ScanCallCount++;
        return Task.FromResult(ScanResultsQueue.Count > 0 ? ScanResultsQueue.Dequeue() : DefaultScanResult);
    }
}
