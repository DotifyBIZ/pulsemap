using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Interop;

namespace Pulsemap.App.Services;

/// <summary>
/// Native wlanapi.dll-backed live link state (WlanQueryInterface, wlan_intf_opcode_current_connection).
/// Opens and closes a client handle per call, matching <see cref="WlanAdapterService"/>'s style —
/// diagnostics reads are user-triggered/infrequent, not hot-path.
/// </summary>
public sealed class WlanLinkDiagnosticsService : ILinkDiagnosticsService
{
    public Task<LinkDiagnosticsSnapshot> GetCurrentLinkAsync(Guid adapterId, CancellationToken cancellationToken = default) =>
        Task.Run(() => GetCurrentLink(adapterId), cancellationToken);

    private static LinkDiagnosticsSnapshot GetCurrentLink(Guid adapterId)
    {
        uint openResult = NativeWlan.WlanOpenHandle(NativeWlan.ClientVersion, IntPtr.Zero, out _, out var clientHandle);
        if (openResult != NativeWlan.ErrorSuccess)
        {
            return LinkDiagnosticsSnapshot.Disconnected;
        }

        try
        {
            var interfaceGuid = adapterId;
            uint result = NativeWlan.WlanQueryInterface(
                clientHandle, ref interfaceGuid, NativeWlan.OpcodeCurrentConnection, IntPtr.Zero, out _, out var dataPtr, IntPtr.Zero);

            if (result != NativeWlan.ErrorSuccess || dataPtr == IntPtr.Zero)
            {
                // ERROR_INVALID_STATE (and any other failure) means "not connected" for this
                // opcode, per WlanQueryInterface's own documented behavior — not a real error.
                return LinkDiagnosticsSnapshot.Disconnected;
            }

            try
            {
                var attributes = Marshal.PtrToStructure<NativeWlan.WLAN_CONNECTION_ATTRIBUTES>(dataPtr);
                if (attributes.isState != NativeWlan.WLAN_INTERFACE_STATE.Connected)
                {
                    return LinkDiagnosticsSnapshot.Disconnected;
                }

                int channel = QueryChannelNumber(clientHandle, adapterId);
                return ToSnapshot(attributes, channel);
            }
            finally
            {
                NativeWlan.WlanFreeMemory(dataPtr);
            }
        }
        finally
        {
            _ = NativeWlan.WlanCloseHandle(clientHandle, IntPtr.Zero);
        }
    }

    // wlan_intf_opcode_channel_number returns a plain ULONG — WLAN_ASSOCIATION_ATTRIBUTES itself has
    // no channel field (only uDot11PhyIndex, an index into the driver's internal rate table).
    private static int QueryChannelNumber(IntPtr clientHandle, Guid adapterId)
    {
        var interfaceGuid = adapterId;
        uint result = NativeWlan.WlanQueryInterface(
            clientHandle, ref interfaceGuid, NativeWlan.OpcodeChannelNumber, IntPtr.Zero, out _, out var dataPtr, IntPtr.Zero);

        if (result != NativeWlan.ErrorSuccess || dataPtr == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            return Marshal.ReadInt32(dataPtr);
        }
        finally
        {
            NativeWlan.WlanFreeMemory(dataPtr);
        }
    }

    private static LinkDiagnosticsSnapshot ToSnapshot(NativeWlan.WLAN_CONNECTION_ATTRIBUTES attributes, int channel)
    {
        var association = attributes.wlanAssociationAttributes;
        int ssidLength = Math.Min((int)association.dot11Ssid.uSSIDLength, association.dot11Ssid.ucSSID.Length);
        string ssid = Encoding.UTF8.GetString(association.dot11Ssid.ucSSID, 0, ssidLength);
        string bssid = string.Join(":", association.dot11Bssid.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)));

        return new LinkDiagnosticsSnapshot(
            IsConnected: true,
            Ssid: ssid,
            Bssid: bssid,
            Band: ClassifyBand(channel, association.dot11PhyType),
            Channel: channel,
            SignalPercent: (int)Math.Min(association.wlanSignalQuality, 100),
            PhyType: DisplayPhyType(association.dot11PhyType),
            RxLinkSpeedMbps: association.ulRxRate / 1000.0,
            TxLinkSpeedMbps: association.ulTxRate / 1000.0);
    }

    // Channel numbering alone is ambiguous for 6GHz (whose channels 1/5/9/13/... numerically overlap
    // 2.4GHz's 1-14 range) — real disambiguation needs the center frequency, which this opcode
    // doesn't expose (unlike WlanGetNetworkBssList's WLAN_BSS_ENTRY, used for the interference scan
    // elsewhere in this app). VHT/OFDM PHY types only ever run on 5GHz, so those are unambiguous
    // regardless of channel number; HE/EHT (WiFi 6/7, which do span all three bands) fall back to
    // channel-range heuristics that are only reliable outside the numeric overlap.
    private static Band? ClassifyBand(int channel, NativeWlan.DOT11_PHY_TYPE phyType) => phyType switch
    {
        NativeWlan.DOT11_PHY_TYPE.Ofdm or NativeWlan.DOT11_PHY_TYPE.Vht => Band.FiveGhz,
        NativeWlan.DOT11_PHY_TYPE.Dsss or NativeWlan.DOT11_PHY_TYPE.HrDsss or NativeWlan.DOT11_PHY_TYPE.Erp => Band.TwoPointFourGhz,
        _ => channel switch
        {
            >= 1 and <= 14 => Band.TwoPointFourGhz,
            >= 36 and <= 177 => Band.FiveGhz,
            > 177 => Band.SixGhz,
            _ => null,
        },
    };

    private static string DisplayPhyType(NativeWlan.DOT11_PHY_TYPE phyType) => phyType switch
    {
        NativeWlan.DOT11_PHY_TYPE.Fhss => "FHSS",
        NativeWlan.DOT11_PHY_TYPE.Dsss => "DSSS (802.11)",
        NativeWlan.DOT11_PHY_TYPE.IrBaseband => "IR baseband",
        NativeWlan.DOT11_PHY_TYPE.Ofdm => "OFDM (802.11a)",
        NativeWlan.DOT11_PHY_TYPE.HrDsss => "HRDSSS (802.11b)",
        NativeWlan.DOT11_PHY_TYPE.Erp => "ERP (802.11g)",
        NativeWlan.DOT11_PHY_TYPE.Ht => "HT (802.11n)",
        NativeWlan.DOT11_PHY_TYPE.Vht => "VHT (802.11ac)",
        NativeWlan.DOT11_PHY_TYPE.Dmg => "DMG (802.11ad)",
        NativeWlan.DOT11_PHY_TYPE.He => "HE (802.11ax / WiFi 6)",
        NativeWlan.DOT11_PHY_TYPE.Eht => "EHT (802.11be / WiFi 7)",
        _ => "Unknown",
    };
}
