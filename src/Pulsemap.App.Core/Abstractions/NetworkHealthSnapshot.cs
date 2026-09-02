namespace Pulsemap.App.Core.Abstractions;

/// <summary>Local network health independent of the WLAN link itself — isolates "it's not your WiFi,
/// it's upstream" from link-quality problems. Null timing fields mean the corresponding check
/// couldn't run at all (no gateway found, no network path), distinct from a check that ran and
/// found a problem (<see cref="DnsSucceeded"/> false, or a very high ping).</summary>
public sealed record NetworkHealthSnapshot(string? GatewayAddress, double? GatewayPingMs, double? DnsResolutionMs, bool DnsSucceeded)
{
    public static NetworkHealthSnapshot Unavailable { get; } = new(null, null, null, false);
}
