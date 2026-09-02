namespace Pulsemap.App.ViewModels;

/// <summary>One row of continuous-monitoring output — deliberately a flat display record (not the
/// raw Core snapshots) so <see cref="Views.DiagnosticsPage"/> can bind a plain <c>ListView</c> table
/// with no value converters.</summary>
public sealed record MonitoringSampleDisplay(string TimestampDisplay, string SignalPercentDisplay, string RxLinkSpeedDisplay, string GatewayPingDisplay);
