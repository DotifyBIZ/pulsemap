using System.Runtime.InteropServices;

namespace Pulsemap.App.Interop;

/// <summary>
/// Raw wlanapi.dll P/Invoke declarations and structs. Struct layouts verified against Microsoft
/// Learn's wlanapi.h reference (WLAN_INTERFACE_INFO_LIST, WLAN_INTERFACE_INFO, WLAN_BSS_LIST,
/// WLAN_BSS_ENTRY, DOT11_SSID, WLAN_RATE_SET, WLAN_CONNECTION_ATTRIBUTES, WLAN_ASSOCIATION_ATTRIBUTES)
/// rather than assumed — a wrong field order/size here silently corrupts memory instead of failing
/// loudly. WLAN_INTERFACE_INFO_LIST and WLAN_BSS_LIST each end in a variable-length array of their
/// entry struct; rather than modeling that array in the struct itself, callers read the fixed
/// header, then walk entries by pointer arithmetic using <see cref="Marshal.SizeOf{T}()"/> of the
/// entry type — the standard, safe pattern for this kind of native "count + inline array" structure.
/// WLAN_ASSOCIATION_ATTRIBUTES.ulRxRate/ulTxRate are documented by Microsoft's own sample only as
/// "the receiving/transmission rate" with no stated unit; confirmed against the community-maintained
/// ManagedNativeWifi wrapper (which real users have validated produces correct Mbps figures) that
/// the unit is plain Kbps, not the 100Kbps unit some other wlanapi rate fields use.
/// </summary>
internal static class NativeWlan
{
    private const string WlanApiDll = "wlanapi.dll";

    internal const uint ClientVersion = 2; // Vista+
    internal const uint Dot11BssTypeAny = 3;
    internal const uint ErrorSuccess = 0;
    internal const uint ErrorAccessDenied = 5;

    // WLAN_INTF_OPCODE enum members used with WlanQueryInterface.
    internal const uint OpcodeCurrentConnection = 7;
    internal const uint OpcodeChannelNumber = 8;

    [DllImport(WlanApiDll)]
    internal static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport(WlanApiDll)]
    internal static extern uint WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        uint opCode,
        IntPtr pReserved,
        out uint pdwDataSize,
        out IntPtr ppData,
        IntPtr pWlanOpcodeValueType);

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

    // Member names drop the wlanapi.h wlan_interface_state_* prefix (CA1712) — see the mapping in
    // each comment for the native name.
    internal enum WLAN_INTERFACE_STATE : uint
    {
        NotReady = 0, // wlan_interface_state_not_ready
        Connected = 1, // wlan_interface_state_connected
        AdHocNetworkFormed = 2, // wlan_interface_state_ad_hoc_network_formed
        Disconnecting = 3, // wlan_interface_state_disconnecting
        Disconnected = 4, // wlan_interface_state_disconnected
        Associating = 5, // wlan_interface_state_associating
        Discovering = 6, // wlan_interface_state_discovering
        Authenticating = 7, // wlan_interface_state_authenticating
    }

    internal enum WLAN_CONNECTION_MODE : uint
    {
        Profile = 0, // wlan_connection_mode_profile
        TemporaryProfile = 1, // wlan_connection_mode_temporary_profile
        DiscoverySecure = 2, // wlan_connection_mode_discovery_secure
        DiscoveryUnsecure = 3, // wlan_connection_mode_discovery_unsecure
        Auto = 4, // wlan_connection_mode_auto
        Invalid = 5, // wlan_connection_mode_invalid
    }

    internal enum DOT11_PHY_TYPE : uint
    {
        Unknown = 0, // dot11_phy_type_unknown
        Fhss = 1, // dot11_phy_type_fhss
        Dsss = 2, // dot11_phy_type_dsss
        IrBaseband = 3, // dot11_phy_type_irbaseband
        Ofdm = 4, // dot11_phy_type_ofdm
        HrDsss = 5, // dot11_phy_type_hrdsss
        Erp = 6, // dot11_phy_type_erp
        Ht = 7, // dot11_phy_type_ht
        Vht = 8, // dot11_phy_type_vht
        Dmg = 9, // dot11_phy_type_dmg
        He = 10, // dot11_phy_type_he
        Eht = 11, // dot11_phy_type_eht
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public byte[] dot11Bssid;

        public DOT11_PHY_TYPE dot11PhyType;
        public uint uDot11PhyIndex;
        public uint wlanSignalQuality;
        public uint ulRxRate;
        public uint ulTxRate;
    }

    // WLAN_SECURITY_ATTRIBUTES follows wlanAssociationAttributes in WLAN_CONNECTION_ATTRIBUTES but
    // none of its fields (security enabled/802.1X/auth+cipher algorithm) are needed here — modeled
    // only so Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES> reads the right total size.
    [StructLayout(LayoutKind.Sequential)]
    internal struct WLAN_SECURITY_ATTRIBUTES
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bOneXEnabled;

        public uint dot11AuthAlgorithm;
        public uint dot11CipherAlgorithm;
    }

    // WLAN_MAX_NAME_LENGTH, per wlanapi.h.
    private const int WlanMaxNameLength = 256;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WLAN_CONNECTION_ATTRIBUTES
    {
        public WLAN_INTERFACE_STATE isState;
        public WLAN_CONNECTION_MODE wlanConnectionMode;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = WlanMaxNameLength)]
        public string strProfileName;

        public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
        public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
    }
}
