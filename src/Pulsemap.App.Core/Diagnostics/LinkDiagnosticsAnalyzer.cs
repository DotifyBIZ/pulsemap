using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Diagnostics;

/// <summary>Pure heuristics turning a link/network snapshot into plain-language troubleshooting
/// findings — no I/O, so this is where the "why is WiFi slow" reasoning actually lives and can be
/// fully unit tested, independent of the native WLAN calls that produce its inputs.</summary>
public static class LinkDiagnosticsAnalyzer
{
    // Same -67dBm "reliable data" convention as GreedyCoverageApPlacementOptimizer, expressed in
    // WLAN_ASSOCIATION_ATTRIBUTES.wlanSignalQuality's percent scale via its own documented linear
    // mapping (0% = -100dBm, 100% = -50dBm): percent = (dBm + 100) / 0.5.
    private const int WeakSignalPercentThreshold = 66;
    private const int VeryWeakSignalPercentThreshold = 33;

    // A link stuck at or below this negotiated rate on 5GHz/6GHz strongly implies a fallback to a
    // legacy/compatibility PHY rate rather than a real 802.11n/ac/ax/be link — a classic cause of
    // "fast WAN, painfully slow WiFi" that signal strength alone wouldn't explain.
    private const double LowPhyRateMbpsOn5Or6Ghz = 11;

    private const double HighGatewayPingMs = 100;
    private const double SlowDnsResolutionMs = 150;

    // How far below the predicted signal an actual reading has to fall before it's flagged as a
    // real mismatch rather than ordinary measurement/model noise (Phase 2 only).
    private const double PredictedVsActualMismatchDb = 15;

    public static IReadOnlyList<DiagnosticFinding> Analyze(LinkDiagnosticsSnapshot link, NetworkHealthSnapshot? health, double? predictedSignalDbm = null)
    {
        ArgumentNullException.ThrowIfNull(link);

        if (!link.IsConnected)
        {
            return [new DiagnosticFinding(DiagnosticSeverity.Error, "DiagnosticNotConnected")];
        }

        var findings = new List<DiagnosticFinding>();

        AnalyzeSignal(link, findings);
        AnalyzePhyRate(link, findings);
        AnalyzeNetworkHealth(health, findings);
        AnalyzePredictedVsActual(link, predictedSignalDbm, findings);

        if (findings.Count == 0)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Info, "DiagnosticNoIssuesFound"));
        }

        return findings;
    }

    private static void AnalyzeSignal(LinkDiagnosticsSnapshot link, List<DiagnosticFinding> findings)
    {
        if (link.SignalPercent <= VeryWeakSignalPercentThreshold)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "DiagnosticVeryWeakSignal", [link.SignalPercent]));
        }
        else if (link.SignalPercent <= WeakSignalPercentThreshold)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "DiagnosticWeakSignal", [link.SignalPercent]));
        }
    }

    private static void AnalyzePhyRate(LinkDiagnosticsSnapshot link, List<DiagnosticFinding> findings)
    {
        if (link.Band is Band.FiveGhz or Band.SixGhz && link.RxLinkSpeedMbps > 0 && link.RxLinkSpeedMbps <= LowPhyRateMbpsOn5Or6Ghz)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "DiagnosticLegacyPhyRate", [link.RxLinkSpeedMbps]));
        }
    }

    private static void AnalyzeNetworkHealth(NetworkHealthSnapshot? health, List<DiagnosticFinding> findings)
    {
        if (health is null)
        {
            return;
        }

        if (!health.DnsSucceeded)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "DiagnosticDnsFailed"));
        }
        else if (health.DnsResolutionMs is { } dnsMs && dnsMs >= SlowDnsResolutionMs)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "DiagnosticSlowDns", [dnsMs]));
        }

        if (health.GatewayPingMs is { } pingMs)
        {
            if (pingMs >= HighGatewayPingMs)
            {
                findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "DiagnosticHighGatewayPing", [pingMs]));
            }
        }
        else if (health.GatewayAddress is not null)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Error, "DiagnosticGatewayUnreachable"));
        }
    }

    private static void AnalyzePredictedVsActual(LinkDiagnosticsSnapshot link, double? predictedSignalDbm, List<DiagnosticFinding> findings)
    {
        if (predictedSignalDbm is not { } predicted)
        {
            return;
        }

        double actualDbm = PercentToDbm(link.SignalPercent);
        double gap = predicted - actualDbm;
        if (gap >= PredictedVsActualMismatchDb)
        {
            findings.Add(new DiagnosticFinding(DiagnosticSeverity.Warning, "DiagnosticPredictedVsActualMismatch", [Math.Round(predicted, 1), Math.Round(actualDbm, 1)]));
        }
    }

    private static double PercentToDbm(int signalPercent) => -100 + (signalPercent * 0.5);
}
