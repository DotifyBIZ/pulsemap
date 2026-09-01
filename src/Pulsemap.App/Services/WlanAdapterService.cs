using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Interop;

namespace Pulsemap.App.Services;

/// <summary>
/// Native wlanapi.dll-backed WLAN scanning. Opens and closes a client handle per call rather than
/// holding one for the app's lifetime — scans are infrequent (user-triggered), so the simplicity
/// of not managing shared native state outweighs the small per-call handle-open cost.
/// </summary>
public sealed class WlanAdapterService : IWlanAdapterService
{
    // Windows drivers meeting logo requirements must complete a scan within 4 seconds (per the
    // WlanScan documentation); poll a little past that rather than registering a native
    // notification callback — Microsoft's own docs endorse polling as an alternative to
    // WlanRegisterNotification, and polling avoids keeping a pinned delegate alive across a native
    // callback thread. ponytail: switch to WlanRegisterNotification if scan latency ever matters.
    private const int ScanPollIntervalMs = 500;
    private const int ScanTimeoutMs = 4500;

    public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
        Task.Run(GetAdapters, cancellationToken);

    public Task<WlanScanResult> ScanAsync(Guid adapterId, CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(adapterId, cancellationToken), cancellationToken);

    private static IReadOnlyList<NetworkAdapterInfo> GetAdapters()
    {
        uint openResult = NativeWlan.WlanOpenHandle(NativeWlan.ClientVersion, IntPtr.Zero, out _, out var clientHandle);
        if (openResult != NativeWlan.ErrorSuccess)
        {
            return [];
        }

        try
        {
            return EnumerateInterfaces(clientHandle);
        }
        finally
        {
            _ = NativeWlan.WlanCloseHandle(clientHandle, IntPtr.Zero);
        }
    }

    private static List<NetworkAdapterInfo> EnumerateInterfaces(IntPtr clientHandle)
    {
        uint result = NativeWlan.WlanEnumInterfaces(clientHandle, IntPtr.Zero, out var listPtr);
        if (result != NativeWlan.ErrorSuccess || listPtr == IntPtr.Zero)
        {
            return [];
        }

        try
        {
            var header = Marshal.PtrToStructure<NativeWlan.WLAN_INTERFACE_INFO_LIST_HEADER>(listPtr);
            var adapters = new List<NetworkAdapterInfo>((int)header.dwNumberOfItems);
            int entrySize = Marshal.SizeOf<NativeWlan.WLAN_INTERFACE_INFO>();
            nint entriesStart = listPtr + Marshal.SizeOf<NativeWlan.WLAN_INTERFACE_INFO_LIST_HEADER>();

            for (int i = 0; i < header.dwNumberOfItems; i++)
            {
                var entry = Marshal.PtrToStructure<NativeWlan.WLAN_INTERFACE_INFO>(entriesStart + (i * entrySize));
                adapters.Add(new NetworkAdapterInfo(entry.InterfaceGuid, entry.strInterfaceDescription));
            }

            return adapters;
        }
        finally
        {
            NativeWlan.WlanFreeMemory(listPtr);
        }
    }

    private static WlanScanResult Scan(Guid adapterId, CancellationToken cancellationToken)
    {
        uint openResult = NativeWlan.WlanOpenHandle(NativeWlan.ClientVersion, IntPtr.Zero, out _, out var clientHandle);
        if (openResult != NativeWlan.ErrorSuccess)
        {
            return new WlanScanResult(WlanScanStatus.NoAdapter, []);
        }

        try
        {
            var interfaceGuid = adapterId;
            uint scanResult = NativeWlan.WlanScan(clientHandle, ref interfaceGuid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (scanResult == NativeWlan.ErrorAccessDenied)
            {
                return new WlanScanResult(WlanScanStatus.LocationAccessDenied, []);
            }

            if (scanResult != NativeWlan.ErrorSuccess)
            {
                return new WlanScanResult(WlanScanStatus.Failed, []);
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < ScanTimeoutMs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var (status, networks) = GetBssList(clientHandle, adapterId);
                if (status != WlanScanStatus.Success)
                {
                    return new WlanScanResult(status, []);
                }

                if (networks.Count > 0)
                {
                    return new WlanScanResult(WlanScanStatus.Success, networks);
                }

                Thread.Sleep(ScanPollIntervalMs);
            }

            var (finalStatus, finalNetworks) = GetBssList(clientHandle, adapterId);
            return new WlanScanResult(finalStatus, finalNetworks);
        }
        finally
        {
            _ = NativeWlan.WlanCloseHandle(clientHandle, IntPtr.Zero);
        }
    }

    private static (WlanScanStatus Status, IReadOnlyList<WlanNetworkReading> Networks) GetBssList(IntPtr clientHandle, Guid adapterId)
    {
        var interfaceGuid = adapterId;
        uint result = NativeWlan.WlanGetNetworkBssList(
            clientHandle, ref interfaceGuid, IntPtr.Zero, NativeWlan.Dot11BssTypeAny, false, IntPtr.Zero, out var listPtr);

        if (result == NativeWlan.ErrorAccessDenied)
        {
            return (WlanScanStatus.LocationAccessDenied, []);
        }

        if (result != NativeWlan.ErrorSuccess || listPtr == IntPtr.Zero)
        {
            return (WlanScanStatus.Failed, []);
        }

        try
        {
            var header = Marshal.PtrToStructure<NativeWlan.WLAN_BSS_LIST_HEADER>(listPtr);
            var networks = new List<WlanNetworkReading>((int)header.dwNumberOfItems);
            int entrySize = Marshal.SizeOf<NativeWlan.WLAN_BSS_ENTRY>();
            nint entriesStart = listPtr + Marshal.SizeOf<NativeWlan.WLAN_BSS_LIST_HEADER>();

            for (int i = 0; i < header.dwNumberOfItems; i++)
            {
                var entry = Marshal.PtrToStructure<NativeWlan.WLAN_BSS_ENTRY>(entriesStart + (i * entrySize));
                networks.Add(ToReading(entry));
            }

            return (WlanScanStatus.Success, networks);
        }
        finally
        {
            NativeWlan.WlanFreeMemory(listPtr);
        }
    }

    private static WlanNetworkReading ToReading(NativeWlan.WLAN_BSS_ENTRY entry)
    {
        int ssidLength = Math.Min((int)entry.dot11Ssid.uSSIDLength, entry.dot11Ssid.ucSSID.Length);
        string ssid = Encoding.UTF8.GetString(entry.dot11Ssid.ucSSID, 0, ssidLength);
        string bssid = string.Join(":", entry.dot11Bssid.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));
        var (band, channel) = ClassifyFrequency(entry.ulChCenterFrequency);
        return new WlanNetworkReading(ssid, bssid, band, channel, entry.lRssi);
    }

    private static (Band? Band, int Channel) ClassifyFrequency(uint centerFrequencyKhz)
    {
        double mhz = centerFrequencyKhz / 1000.0;
        return mhz switch
        {
            >= 2400 and < 2500 => (Band.TwoPointFourGhz, (int)Math.Round((mhz - 2407) / 5)),
            >= 5000 and < 5900 => (Band.FiveGhz, (int)Math.Round((mhz - 5000) / 5)),
            >= 5900 and < 7200 => (Band.SixGhz, (int)Math.Round((mhz - 5950) / 5)),
            _ => (null, 0),
        };
    }
}
