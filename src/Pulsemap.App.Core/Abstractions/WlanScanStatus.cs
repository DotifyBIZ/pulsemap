namespace Pulsemap.App.Core.Abstractions;

/// <summary>Why a scan did or didn't return results — surfaced honestly rather than collapsing
/// every failure into an empty network list.</summary>
public enum WlanScanStatus
{
    Success,

    /// <summary>Windows requires Location consent for WlanScan/WlanGetNetworkBssList. Windows shows
    /// its own one-time system prompt on first use; this status means the user hasn't granted it
    /// (or revoked it) — direct them to Settings &gt; Privacy &amp; security &gt; Location.</summary>
    LocationAccessDenied,

    NoAdapter,

    Failed,
}
