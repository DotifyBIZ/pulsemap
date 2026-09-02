namespace Pulsemap.App.Core.Abstractions;

/// <summary>Local network health checks (gateway ping, DNS timing) for the given adapter. Pure BCL
/// networking (System.Net.NetworkInformation) — no native P/Invoke needed, unlike the WLAN-specific
/// abstractions in this folder.</summary>
public interface INetworkHealthService
{
    Task<NetworkHealthSnapshot> CheckHealthAsync(Guid adapterId, CancellationToken cancellationToken = default);
}
