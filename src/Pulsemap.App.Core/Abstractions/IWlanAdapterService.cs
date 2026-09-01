namespace Pulsemap.App.Core.Abstractions;

/// <summary>
/// Platform-specific WLAN scanning, implemented in Pulsemap.App via native wlanapi.dll P/Invoke —
/// see docs/adr/0002-installer-innosetup-over-msix.md for why the WinRT Windows.Devices.WiFi API
/// isn't an option for this unpackaged app.
/// </summary>
public interface IWlanAdapterService
{
    Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default);

    Task<WlanScanResult> ScanAsync(Guid adapterId, CancellationToken cancellationToken = default);
}
