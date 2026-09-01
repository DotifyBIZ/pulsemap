using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Abstractions;

/// <summary>One network observed in a WLAN scan — every audible SSID/BSSID at the point of the
/// scan, not just the network being surveyed. This is what lets channel planning see real
/// co-channel/adjacent-channel interference.</summary>
/// <param name="Band">Null when the channel's center frequency doesn't fall in a band this app models.</param>
public sealed record WlanNetworkReading(string Ssid, string Bssid, Band? Band, int Channel, double SignalDbm);
