using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Updates;

namespace Pulsemap.App.Services;

/// <summary>
/// Queries GitHub's public Releases API for DotifyBIZ/pulsemap's latest tag and compares it
/// against this build's own assembly version. A network/parse failure is treated the same as "no
/// update available" — this check must never surface an error to the user or block anything it's
/// called from.
/// </summary>
public sealed class GitHubUpdateCheckService(IHttpClientFactory httpClientFactory, IAppLogger logger) : IUpdateCheckService
{
    private const string ReleasesApiUrl = "https://api.github.com/repos/DotifyBIZ/pulsemap/releases/latest";
    private const string ReleasesPageUrl = "https://github.com/DotifyBIZ/pulsemap/releases/latest";

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(GitHubUpdateCheckService));
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Pulsemap-App");
            var release = await client.GetFromJsonAsync<GitHubRelease>(ReleasesApiUrl, cancellationToken);

            string currentVersion = typeof(GitHubUpdateCheckService).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            if (release?.TagName is not { } tag || !SemanticVersionComparer.IsNewer(currentVersion, tag))
            {
                return UpdateCheckResult.NoUpdate;
            }

            return new UpdateCheckResult(true, tag, ReleasesPageUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            await logger.LogWarningAsync($"Update check failed (treated as no update available): {ex.Message}", cancellationToken);
            return UpdateCheckResult.NoUpdate;
        }
    }

    private sealed record GitHubRelease([property: JsonPropertyName("tag_name")] string? TagName);
}
