using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Abstractions;

/// <summary>A point-in-time reading of this machine's current WLAN association — live link state,
/// not a scan of nearby networks (that's <see cref="WlanScanResult"/>). <paramref name="RxLinkSpeedMbps"/>/
/// <paramref name="TxLinkSpeedMbps"/> are the negotiated PHY rate, not throughput actually achieved.</summary>
public sealed record LinkDiagnosticsSnapshot(
    bool IsConnected,
    string? Ssid,
    string? Bssid,
    Band? Band,
    int Channel,
    int SignalPercent,
    string? PhyType,
    double RxLinkSpeedMbps,
    double TxLinkSpeedMbps)
{
    public static LinkDiagnosticsSnapshot Disconnected { get; } = new(false, null, null, null, 0, 0, null, 0, 0);
}
