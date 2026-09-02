using System.Diagnostics;
using System.Net.NetworkInformation;
using Pulsemap.App.Core.Abstractions;

namespace Pulsemap.App.Services;

/// <summary>
/// Local network health (gateway ping, DNS resolution timing) via pure BCL networking — no native
/// P/Invoke needed, unlike the WLAN-specific services in this folder. Every check here is local or
/// to the gateway/DNS the adapter is already configured to use; nothing here is a new outbound call
/// beyond what the OS's own network stack already does for the same adapter.
/// </summary>
public sealed class NetworkHealthService : INetworkHealthService
{
    private const int PingTimeoutMs = 2000;
    private const string DnsProbeHostName = "www.msftconnecttest.com";

    public async Task<NetworkHealthSnapshot> CheckHealthAsync(Guid adapterId, CancellationToken cancellationToken = default)
    {
        string? gatewayAddress = FindGatewayAddress(adapterId);

        double? pingMs = gatewayAddress is null ? null : await PingGatewayAsync(gatewayAddress, cancellationToken).ConfigureAwait(false);
        var (dnsMs, dnsSucceeded) = await ResolveDnsAsync(cancellationToken).ConfigureAwait(false);

        return new NetworkHealthSnapshot(gatewayAddress, pingMs, dnsMs, dnsSucceeded);
    }

    private static string? FindGatewayAddress(Guid adapterId)
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (!Guid.TryParse(nic.Id, out var nicId) || nicId != adapterId)
            {
                continue;
            }

            var gateway = nic.GetIPProperties().GatewayAddresses.FirstOrDefault(g => g.Address is not null);
            return gateway?.Address.ToString();
        }

        return null;
    }

    private static async Task<double?> PingGatewayAsync(string gatewayAddress, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(gatewayAddress, PingTimeoutMs).WaitAsync(cancellationToken).ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (PingException)
        {
            return null;
        }
    }

    private static async Task<(double? Ms, bool Succeeded)> ResolveDnsAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            _ = await System.Net.Dns.GetHostEntryAsync(DnsProbeHostName, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            return (stopwatch.Elapsed.TotalMilliseconds, true);
        }
        catch (System.Net.Sockets.SocketException)
        {
            return (null, false);
        }
    }
}
