namespace Pulsemap.App.Core.Abstractions;

/// <summary>Platform-specific live WLAN link state, implemented in Pulsemap.App via native
/// wlanapi.dll P/Invoke (WlanQueryInterface) — same split as <see cref="IWlanAdapterService"/>.</summary>
public interface ILinkDiagnosticsService
{
    Task<LinkDiagnosticsSnapshot> GetCurrentLinkAsync(Guid adapterId, CancellationToken cancellationToken = default);
}
