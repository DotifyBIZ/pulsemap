namespace Pulsemap.App.Core.Settings;

/// <summary>App-level preferences, distinct from per-survey data — persisted locally, never
/// synced anywhere.</summary>
public sealed class AppSettings
{
    /// <summary>Default on: checking is a single unauthenticated GET to GitHub's public releases
    /// API with no identifying data attached, not telemetry — but it's still an outbound network
    /// call, so it stays user-visible and disableable in Settings.</summary>
    public bool CheckForUpdatesEnabled { get; set; } = true;
}
