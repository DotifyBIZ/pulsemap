using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Placement;

/// <summary>
/// Generic, vendor-neutral non-overlapping channel sets and default transmit powers per band.
/// Deliberately conservative: 5GHz sticks to non-DFS UNII-1/UNII-3 channels, and 6GHz to a
/// representative standard-power subset — regulatory-domain nuances, DFS, and 6GHz AFC rules are
/// out of scope for these generic recommendations (vendor-specific config templates are Phase 3).
/// </summary>
public static class ChannelPlan
{
    public static readonly IReadOnlyList<int> TwoPointFourGhzChannels = [1, 6, 11];
    public static readonly IReadOnlyList<int> FiveGhzChannels = [36, 40, 44, 48, 149, 153, 157, 161];
    public static readonly IReadOnlyList<int> SixGhzChannels = [1, 5, 9, 13, 17, 21, 25, 29];

    public static IReadOnlyList<int> ChannelsFor(Band band) => band switch
    {
        Band.TwoPointFourGhz => TwoPointFourGhzChannels,
        Band.FiveGhz => FiveGhzChannels,
        Band.SixGhz => SixGhzChannels,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    public static double DefaultTransmitPowerDbm(Band band) => band switch
    {
        Band.TwoPointFourGhz => 17,
        Band.FiveGhz => 20,
        Band.SixGhz => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };
}
