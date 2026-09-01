using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Propagation;

/// <summary>
/// Free-space (Friis) path loss plus summed wall attenuation for every wall the direct line
/// between transmitter and receiver crosses.
/// </summary>
public sealed class LogDistancePropagationModel : IPropagationModel
{
    // Avoids log10(0) when transmitter and receiver coincide.
    private const double MinimumDistanceMeters = 0.1;

    public double PredictSignalDbm(Point2D transmitterPosition, double transmitPowerDbm, Point2D receiverPosition, Band band, IReadOnlyList<Wall> walls)
    {
        ArgumentNullException.ThrowIfNull(walls);

        double distanceMeters = Math.Max(transmitterPosition.DistanceTo(receiverPosition), MinimumDistanceMeters);
        double freeSpaceLossDb = FreeSpacePathLossDb(distanceMeters, FrequencyGhz(band));

        double wallLossDb = 0;
        foreach (var wall in walls)
        {
            if (SegmentsIntersect(transmitterPosition, receiverPosition, wall.Start, wall.End))
            {
                wallLossDb += WallAttenuationTable.GetAttenuationDb(wall.Material, wall.ThicknessMeters, band);
            }
        }

        return transmitPowerDbm - freeSpaceLossDb - wallLossDb;
    }

    // FSPL(dB) = 20log10(d_m) + 20log10(f_GHz) + 32.44 — the standard Friis free-space formula
    // (20log10(4*pi*d*f/c)), re-derived for meters/GHz units and cross-checked against the more
    // commonly tabulated (km, MHz) form, which uses the same +32.44 constant.
    private static double FreeSpacePathLossDb(double distanceMeters, double frequencyGhz) =>
        (20 * Math.Log10(distanceMeters)) + (20 * Math.Log10(frequencyGhz)) + 32.44;

    private static double FrequencyGhz(Band band) => band switch
    {
        Band.TwoPointFourGhz => 2.4,
        Band.FiveGhz => 5.0,
        Band.SixGhz => 6.0,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    // General-position segment intersection via orientation tests (Cormen et al.) — collinear/
    // touching-endpoint edge cases are ignored as not meaningful for RF wall-crossing purposes.
    private static bool SegmentsIntersect(Point2D a1, Point2D a2, Point2D b1, Point2D b2)
    {
        double d1 = Direction(b1, b2, a1);
        double d2 = Direction(b1, b2, a2);
        double d3 = Direction(a1, a2, b1);
        double d4 = Direction(a1, a2, b2);

        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
               ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Direction(Point2D pi, Point2D pj, Point2D pk) =>
        ((pk.X - pi.X) * (pj.Y - pi.Y)) - ((pk.Y - pi.Y) * (pj.X - pi.X));
}
