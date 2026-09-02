using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Diagnostics;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Diagnostics;

public sealed class LinkDiagnosticsAnalyzerTests
{
    [Fact]
    public void Analyze_NotConnected_ReturnsSingleFindingAndSkipsEverythingElse()
    {
        var findings = LinkDiagnosticsAnalyzer.Analyze(LinkDiagnosticsSnapshot.Disconnected, NetworkHealthSnapshot.Unavailable);

        var finding = Assert.Single(findings);
        Assert.Equal("DiagnosticNotConnected", finding.MessageKey);
        Assert.Equal(DiagnosticSeverity.Error, finding.Severity);
    }

    [Fact]
    public void Analyze_StrongSignalHealthyNetwork_ReturnsNoIssuesFound()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);
        var health = new NetworkHealthSnapshot("192.168.1.1", GatewayPingMs: 5, DnsResolutionMs: 10, DnsSucceeded: true);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health);

        var finding = Assert.Single(findings);
        Assert.Equal("DiagnosticNoIssuesFound", finding.MessageKey);
        Assert.Equal(DiagnosticSeverity.Info, finding.Severity);
    }

    [Theory]
    [InlineData(66, DiagnosticSeverity.Warning, "DiagnosticWeakSignal")]
    [InlineData(33, DiagnosticSeverity.Error, "DiagnosticVeryWeakSignal")]
    public void Analyze_WeakSignal_FlagsBySeverityThreshold(int signalPercent, DiagnosticSeverity expectedSeverity, string expectedKey)
    {
        var link = Connected(signalPercent, band: Band.FiveGhz, rxMbps: 866);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health: null);

        Assert.Contains(findings, f => f.Severity == expectedSeverity && f.MessageKey == expectedKey);
    }

    [Fact]
    public void Analyze_StrongSignal_DoesNotFlagSignal()
    {
        var link = Connected(signalPercent: 67, band: Band.FiveGhz, rxMbps: 866);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health: null);

        Assert.DoesNotContain(findings, f => f.MessageKey is "DiagnosticWeakSignal" or "DiagnosticVeryWeakSignal");
    }

    [Fact]
    public void Analyze_LowPhyRateOn5Ghz_FlagsLegacyFallback()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 11);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health: null);

        Assert.Contains(findings, f => f.MessageKey == "DiagnosticLegacyPhyRate");
    }

    [Fact]
    public void Analyze_LowPhyRateOn24Ghz_DoesNotFlag()
    {
        // 11 Mbps is a normal top rate for legacy 802.11b on 2.4GHz — only a 5/6GHz fallback is suspicious.
        var link = Connected(signalPercent: 90, band: Band.TwoPointFourGhz, rxMbps: 11);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health: null);

        Assert.DoesNotContain(findings, f => f.MessageKey == "DiagnosticLegacyPhyRate");
    }

    [Fact]
    public void Analyze_DnsFailed_FlagsError()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);
        var health = new NetworkHealthSnapshot("192.168.1.1", GatewayPingMs: 5, DnsResolutionMs: null, DnsSucceeded: false);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health);

        Assert.Contains(findings, f => f.Severity == DiagnosticSeverity.Error && f.MessageKey == "DiagnosticDnsFailed");
    }

    [Fact]
    public void Analyze_HighGatewayPing_FlagsWarning()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);
        var health = new NetworkHealthSnapshot("192.168.1.1", GatewayPingMs: 150, DnsResolutionMs: 10, DnsSucceeded: true);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health);

        Assert.Contains(findings, f => f.Severity == DiagnosticSeverity.Warning && f.MessageKey == "DiagnosticHighGatewayPing");
    }

    [Fact]
    public void Analyze_GatewayKnownButUnreachable_FlagsError()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);
        var health = new NetworkHealthSnapshot("192.168.1.1", GatewayPingMs: null, DnsResolutionMs: 10, DnsSucceeded: true);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health);

        Assert.Contains(findings, f => f.Severity == DiagnosticSeverity.Error && f.MessageKey == "DiagnosticGatewayUnreachable");
    }

    [Fact]
    public void Analyze_NoGatewayAddress_DoesNotFlagUnreachable()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);
        var health = new NetworkHealthSnapshot(GatewayAddress: null, GatewayPingMs: null, DnsResolutionMs: 10, DnsSucceeded: true);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health);

        Assert.DoesNotContain(findings, f => f.MessageKey == "DiagnosticGatewayUnreachable");
    }

    [Fact]
    public void Analyze_PredictedSignalMuchStrongerThanActual_FlagsMismatch()
    {
        // 90% signal quality maps to -55dBm; a -30dBm prediction is a 25dB gap.
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health: null, predictedSignalDbm: -30);

        Assert.Contains(findings, f => f.MessageKey == "DiagnosticPredictedVsActualMismatch");
    }

    [Fact]
    public void Analyze_PredictedSignalCloseToActual_DoesNotFlagMismatch()
    {
        var link = Connected(signalPercent: 90, band: Band.FiveGhz, rxMbps: 866);

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health: null, predictedSignalDbm: -58);

        Assert.DoesNotContain(findings, f => f.MessageKey == "DiagnosticPredictedVsActualMismatch");
    }

    private static LinkDiagnosticsSnapshot Connected(int signalPercent, Band band, double rxMbps) =>
        new(IsConnected: true, Ssid: "TestNet", Bssid: "AA:BB:CC:DD:EE:FF", Band: band, Channel: 36, SignalPercent: signalPercent, PhyType: "802.11ac", RxLinkSpeedMbps: rxMbps, TxLinkSpeedMbps: rxMbps);
}
