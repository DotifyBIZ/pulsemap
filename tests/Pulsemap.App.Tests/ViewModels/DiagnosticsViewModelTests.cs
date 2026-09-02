using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class DiagnosticsViewModelTests
{
    private readonly FakeWlanAdapterService _wlanAdapterService = new();
    private readonly FakeLinkDiagnosticsService _linkDiagnosticsService = new();
    private readonly FakeNetworkHealthService _networkHealthService = new();
    private readonly FakeLocalizationService _localizationService = new();

    private DiagnosticsViewModel CreateSut() => new(_wlanAdapterService, _linkDiagnosticsService, _networkHealthService, _localizationService);

    [Fact]
    public async Task LoadAdaptersCommand_PopulatesAdaptersAndSelectsFirst()
    {
        _wlanAdapterService.AdaptersToReturn = [new NetworkAdapterInfo(Guid.NewGuid(), "Wi-Fi")];
        var sut = CreateSut();

        await sut.LoadAdaptersCommand.ExecuteAsync(null);

        Assert.Single(sut.Adapters);
        Assert.Equal(sut.Adapters[0], sut.SelectedAdapter);
    }

    [Fact]
    public async Task RunDiagnosticsCommand_NotConnected_ReportsNotConnectedFinding()
    {
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Wi-Fi");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        _linkDiagnosticsService.SnapshotToReturn = LinkDiagnosticsSnapshot.Disconnected;
        var sut = CreateSut();
        await sut.LoadAdaptersCommand.ExecuteAsync(null);

        await sut.RunDiagnosticsCommand.ExecuteAsync(null);

        Assert.True(sut.HasResult);
        Assert.Contains(sut.Findings, f => f.Message == "DiagnosticNotConnected");
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public async Task RunDiagnosticsCommand_Connected_PopulatesLinkDisplaysAndSkipsHealthCheckWhenDisconnectedOnly()
    {
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Wi-Fi");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        _linkDiagnosticsService.SnapshotToReturn = new LinkDiagnosticsSnapshot(
            IsConnected: true, Ssid: "HomeNet", Bssid: "AA:BB:CC:DD:EE:FF", Band: Band.FiveGhz, Channel: 36,
            SignalPercent: 90, PhyType: "VHT (802.11ac)", RxLinkSpeedMbps: 866, TxLinkSpeedMbps: 866);
        _networkHealthService.SnapshotToReturn = new NetworkHealthSnapshot("192.168.1.1", 5, 10, true);
        var sut = CreateSut();
        await sut.LoadAdaptersCommand.ExecuteAsync(null);

        await sut.RunDiagnosticsCommand.ExecuteAsync(null);

        Assert.Equal("HomeNet", sut.SsidDisplay);
        Assert.NotNull(sut.LinkSummaryDisplay);
        Assert.NotNull(sut.NetworkHealthSummaryDisplay);
        Assert.Contains(sut.Findings, f => f.Message == "DiagnosticNoIssuesFound");
    }

    [Fact]
    public void ToggleMonitoringCommand_NoAdapterSelected_CannotExecute()
    {
        var sut = CreateSut();

        Assert.False(sut.ToggleMonitoringCommand.CanExecute(null));
    }

    [Fact]
    public async Task ToggleMonitoringCommand_StartsThenStopsMonitoring()
    {
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Wi-Fi");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        var sut = CreateSut();
        await sut.LoadAdaptersCommand.ExecuteAsync(null);

        sut.ToggleMonitoringCommand.Execute(null);
        Assert.True(sut.IsMonitoring);

        sut.ToggleMonitoringCommand.Execute(null);
        Assert.False(sut.IsMonitoring);

        sut.Dispose();
    }
}
