using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Diagnostics;
using Pulsemap.App.Services;

namespace Pulsemap.App.ViewModels;

/// <summary>Standalone "Swiss army knife" WLAN troubleshooting — no survey required. Reads this
/// machine's own live link state, checks local network health, and runs both through
/// <see cref="LinkDiagnosticsAnalyzer"/>. Workspace's own diagnostics (Phase 2, not this class) adds
/// a predicted-vs-actual comparison once a survey's propagation model is available.</summary>
public sealed partial class DiagnosticsViewModel(
    IWlanAdapterService wlanAdapterService,
    ILinkDiagnosticsService linkDiagnosticsService,
    INetworkHealthService networkHealthService,
    ILocalizationService localizationService) : ObservableObject, IDisposable
{
    private static readonly TimeSpan MonitoringInterval = TimeSpan.FromSeconds(5);

    // ~15 minutes of history at the interval above — enough to see a pattern, bounded enough that
    // an all-day monitoring session doesn't grow without limit.
    private const int MaxMonitoringSamples = 180;

    private CancellationTokenSource? _monitoringCts;

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    public ObservableCollection<DiagnosticFindingDisplay> Findings { get; } = [];

    public ObservableCollection<MonitoringSampleDisplay> MonitoringSamples { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunDiagnosticsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleMonitoringCommand))]
    public partial NetworkAdapterInfo? SelectedAdapter { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleMonitoringButtonText))]
    public partial bool IsMonitoring { get; set; }

    [ObservableProperty]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMonitoringError))]
    public partial string? MonitoringErrorMessage { get; set; }

    public bool HasMonitoringError => MonitoringErrorMessage is not null;

    /// <summary>No WLAN adapter at all — every button on this page is disabled, so without this
    /// the page reads as broken rather than as "this machine has no WiFi".</summary>
    public bool HasNoAdapters => Adapters.Count == 0;

    [ObservableProperty]
    public partial string? SsidDisplay { get; set; }

    [ObservableProperty]
    public partial string? BandAndChannelDisplay { get; set; }

    [ObservableProperty]
    public partial string? LinkSummaryDisplay { get; set; }

    [ObservableProperty]
    public partial string? NetworkHealthSummaryDisplay { get; set; }

    public string ToggleMonitoringButtonText => localizationService.GetString(IsMonitoring ? "DiagnosticsStopMonitoringButton" : "DiagnosticsStartMonitoringButton");

    [RelayCommand]
    private async Task LoadAdaptersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<NetworkAdapterInfo> adapters;
        try
        {
            adapters = await wlanAdapterService.GetAdaptersAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Called from the page's async void OnNavigatedTo — a WLAN service failure has to
            // land in the page, not in the process's unhandled-exception handler.
            MonitoringErrorMessage = ex.Message;
            adapters = [];
        }

        Adapters.Clear();
        foreach (var adapter in adapters)
        {
            Adapters.Add(adapter);
        }

        SelectedAdapter = Adapters.Count > 0 ? Adapters[0] : null;
        OnPropertyChanged(nameof(HasNoAdapters));
    }

    private bool CanRunDiagnostics() => SelectedAdapter is not null && !IsRunning;

    [RelayCommand(CanExecute = nameof(CanRunDiagnostics))]
    private async Task RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        if (SelectedAdapter is not { } adapter)
        {
            return;
        }

        IsRunning = true;
        MonitoringErrorMessage = null;
        try
        {
            var (link, health) = await CaptureAsync(adapter.Id, cancellationToken);
            ApplySnapshot(link, health);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MonitoringErrorMessage = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task<(LinkDiagnosticsSnapshot Link, NetworkHealthSnapshot Health)> CaptureAsync(Guid adapterId, CancellationToken cancellationToken)
    {
        var link = await linkDiagnosticsService.GetCurrentLinkAsync(adapterId, cancellationToken);
        var health = link.IsConnected
            ? await networkHealthService.CheckHealthAsync(adapterId, cancellationToken)
            : NetworkHealthSnapshot.Unavailable;

        return (link, health);
    }

    private void ApplySnapshot(LinkDiagnosticsSnapshot link, NetworkHealthSnapshot health)
    {
        HasResult = true;
        SsidDisplay = link.IsConnected ? link.Ssid : localizationService.GetString("DiagnosticsNotConnectedDisplay");
        BandAndChannelDisplay = link.IsConnected
            ? string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsBandAndChannelFormat"), BandDisplayName(link.Band), link.Channel)
            : null;
        LinkSummaryDisplay = link.IsConnected
            ? string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsLinkSummaryFormat"), link.SignalPercent, link.PhyType, link.RxLinkSpeedMbps, link.TxLinkSpeedMbps)
            : null;
        NetworkHealthSummaryDisplay = BuildNetworkHealthSummary(health);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health);
        Findings.Clear();
        foreach (var finding in findings)
        {
            Findings.Add(new DiagnosticFindingDisplay(finding.Severity, FormatFinding(finding)));
        }
    }

    private string? BuildNetworkHealthSummary(NetworkHealthSnapshot health)
    {
        if (health.GatewayAddress is null)
        {
            return null;
        }

        string pingDisplay = health.GatewayPingMs is { } pingMs
            ? string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsPingMsFormat"), pingMs)
            : localizationService.GetString("DiagnosticsNoPingReplyDisplay");
        string dnsDisplay = health.DnsSucceeded && health.DnsResolutionMs is { } dnsMs
            ? string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsDnsMsFormat"), dnsMs)
            : localizationService.GetString("DiagnosticsDnsFailedDisplay");

        return string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsNetworkHealthSummaryFormat"), health.GatewayAddress, pingDisplay, dnsDisplay);
    }

    private string FormatFinding(DiagnosticFinding finding)
    {
        string template = localizationService.GetString(finding.MessageKey);
        return finding.FormatArgs is null ? template : string.Format(CultureInfo.CurrentCulture, template, [.. finding.FormatArgs]);
    }

    private string BandDisplayName(Core.Models.Band? band) => band switch
    {
        Core.Models.Band.TwoPointFourGhz => "2.4 GHz",
        Core.Models.Band.FiveGhz => "5 GHz",
        Core.Models.Band.SixGhz => "6 GHz",
        _ => localizationService.GetString("DiagnosticsUnknownBandDisplay"),
    };

    private bool CanToggleMonitoring() => SelectedAdapter is not null;

    [RelayCommand(CanExecute = nameof(CanToggleMonitoring))]
    private void ToggleMonitoring()
    {
        if (IsMonitoring)
        {
            StopMonitoring();
        }
        else
        {
            StartMonitoring();
        }
    }

    private void StartMonitoring()
    {
        if (SelectedAdapter is not { } adapter || IsMonitoring)
        {
            return;
        }

        MonitoringSamples.Clear();
        MonitoringErrorMessage = null;
        _monitoringCts = new CancellationTokenSource();
        IsMonitoring = true;
        _ = MonitorLoopAsync(adapter.Id, _monitoringCts.Token);
    }

    public void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        IsMonitoring = false;
    }

    private async Task MonitorLoopAsync(Guid adapterId, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(MonitoringInterval);
        try
        {
            do
            {
                var (link, health) = await CaptureAsync(adapterId, cancellationToken);
                MonitoringSamples.Add(new MonitoringSampleDisplay(
                    DateTimeOffset.Now.ToString("T", CultureInfo.CurrentCulture),
                    string.Format(CultureInfo.CurrentCulture, "{0}%", link.SignalPercent),
                    string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsMbpsFormat"), link.RxLinkSpeedMbps),
                    health.GatewayPingMs is { } pingMs ? string.Format(CultureInfo.CurrentCulture, localizationService.GetString("DiagnosticsPingMsFormat"), pingMs) : localizationService.GetString("DiagnosticsNoPingReplyDisplay")));

                // Monitoring is meant to be left running for a long time; without a ceiling this
                // list grows by 12 rows a minute for as long as the page is open.
                while (MonitoringSamples.Count > MaxMonitoringSamples)
                {
                    MonitoringSamples.RemoveAt(0);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Expected when StopMonitoring cancels the token — not an error.
        }
        catch (Exception ex)
        {
            // Fire-and-forget loop: anything escaping here would be an unobserved exception that
            // silently stops sampling while the button still reads "Stop monitoring".
            MonitoringErrorMessage = ex.Message;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                StopMonitoring();
            }
        }
    }

    public void Dispose()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
    }
}
