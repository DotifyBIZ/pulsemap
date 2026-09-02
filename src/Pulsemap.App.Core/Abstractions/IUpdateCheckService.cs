namespace Pulsemap.App.Core.Abstractions;

/// <summary>
/// Checks GitHub Releases for a newer published version than this build — Pulsemap's only
/// outbound network call. Gated behind AppSettings.CheckForUpdatesEnabled, since the project
/// otherwise commits to "nothing phones home"; see docs/adr/0004-update-check-network-call.md.
/// Implemented in Pulsemap.App via IHttpClientFactory.
/// </summary>
public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}
