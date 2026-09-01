using System.Runtime.InteropServices;

namespace Pulsemap.App.Interop;

/// <summary>
/// Raw wlanapi.dll P/Invoke declarations and structs. Struct layouts verified against Microsoft
/// Learn's wlanapi.h reference (WLAN_INTERFACE_INFO_LIST, WLAN_INTERFACE_INFO, WLAN_BSS_LIST,
/// WLAN_BSS_ENTRY, DOT11_SSID, WLAN_RATE_SET) rather than assumed — a wrong field order/size here
/// silently corrupts memory instead of failing loudly. WLAN_INTERFACE_INFO_LIST and WLAN_BSS_LIST
/// each end in a variable-length array of their entry struct; rather than modeling that array in
/// the struct itself, callers read the fixed header, then walk entries by pointer arithmetic using
/// <see cref="Marshal.SizeOf{T}()"/> of the entry type — the standard, safe pattern for this kind
/// of native "count + inline array" structure.
/// </summary>
internal static class NativeWlan
{
    private const string WlanApiDll = "wlanapi.dll";

    internal const uint ClientVersion = 2; // Vista+
    internal const uint Dot11BssTypeAny = 3;
    internal const uint ErrorSuccess = 0;
    internal const uint ErrorAccessDenied = 5;

    [DllImport(WlanApiDll)]
    internal static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanGetNetworkBssList(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        IntPtr pDot11Ssid,
        uint dot11BssType,
        [MarshalAs(UnmanagedType.Bool)] bool bSecurityEnabled,
        IntPtr pReserved,
        out IntPtr ppWlanBssList);

    [DllImport(WlanApiDll)]
    internal static extern void WlanFreeMemory(IntPtr pMemory);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;

        public uint isState;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_INTERFACE_INFO_LIST_HEADER
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DOT11_SSID
    {
        public uint uSSIDLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_RATE_SET
    {
        public uint uRateSetLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 126)]
        public ushort[] usRateSet;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_BSS_ENTRY
    {
        public DOT11_SSID dot11Ssid;
        public uint uPhyId;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] dot11Bssid;

        public uint dot11BssType;
        public uint dot11BssPhyType;
        public int lRssi;
        public uint uLinkQuality;
        public byte bInRegDomain;
        public ushort usBeaconPeriod;
        public ulong ullTimestamp;
        public ulong ullHostTimestamp;
        public ushort usCapabilityInformation;
        public uint ulChCenterFrequency;
        public WLAN_RATE_SET wlanRateSet;
        public uint ulIeOffset;
        public uint ulIeSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_BSS_LIST_HEADER
    {
        public uint dwTotalSize;
        public uint dwNumberOfItems;
    }
}
