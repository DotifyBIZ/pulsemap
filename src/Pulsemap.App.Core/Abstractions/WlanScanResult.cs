namespace Pulsemap.App.Core.Abstractions;

public sealed record WlanScanResult(WlanScanStatus Status, IReadOnlyList<WlanNetworkReading> Networks);
