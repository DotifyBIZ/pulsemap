using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Propagation;

/// <summary>
/// Representative wall attenuation values (dB), at a reference thickness per material, drawn from
/// published indoor-propagation measurement studies (am1.us propagation-loss study, ibwave.com,
/// ursamajorlab.com wall-attenuation comparisons). These are realistic reference figures, not a
/// lab-certified model — matching the product's own framing of "realistic" attenuation values.
/// </summary>
public static class WallAttenuationTable
{
    private static readonly Dictionary<WallMaterial, MaterialReference> References = new()
    {
        [WallMaterial.Drywall] = new MaterialReference(ReferenceThicknessMeters: 0.02, TwoPointFourGhz: 4, FiveGhz: 6, SixGhz: 7),
        [WallMaterial.GlassStandard] = new MaterialReference(ReferenceThicknessMeters: 0.01, TwoPointFourGhz: 2, FiveGhz: 4, SixGhz: 5),
        [WallMaterial.GlassLowE] = new MaterialReference(ReferenceThicknessMeters: 0.01, TwoPointFourGhz: 18, FiveGhz: 28, SixGhz: 32),
        [WallMaterial.Wood] = new MaterialReference(ReferenceThicknessMeters: 0.03, TwoPointFourGhz: 4, FiveGhz: 6, SixGhz: 7),
        [WallMaterial.Brick] = new MaterialReference(ReferenceThicknessMeters: 0.15, TwoPointFourGhz: 10, FiveGhz: 15, SixGhz: 18),
        [WallMaterial.Concrete] = new MaterialReference(ReferenceThicknessMeters: 0.15, TwoPointFourGhz: 18, FiveGhz: 28, SixGhz: 32),
        [WallMaterial.ReinforcedConcrete] = new MaterialReference(ReferenceThicknessMeters: 0.20, TwoPointFourGhz: 25, FiveGhz: 35, SixGhz: 40),
    };

    // Plan's documented fallback for a wall with no material specified: a flat, generic
    // per-crossing penalty rather than a material-specific calculation. Drywall — the lightest,
    // most common interior partition — is the reasonable generic default.
    private static readonly MaterialReference GenericFallback = References[WallMaterial.Drywall];

    public static double GetAttenuationDb(WallMaterial? material, double? thicknessMeters, Band band)
    {
        var reference = material is { } knownMaterial ? References[knownMaterial] : GenericFallback;
        double baseDb = BaseAttenuationDb(reference, band);

        // A non-positive or non-finite thickness (corrupt/hand-edited project bundle) would
        // otherwise scale to zero or negative attenuation — RF-transparent or signal-boosting
        // through a wall. Treat it the same as "unspecified" rather than propagating garbage.
        if (material is null || thicknessMeters is not { } thickness || !double.IsFinite(thickness) || thickness <= 0)
        {
            return baseDb;
        }

        double scale = thickness / reference.ReferenceThicknessMeters;
        return baseDb * scale;
    }

    private static double BaseAttenuationDb(MaterialReference reference, Band band) => band switch
    {
        Band.TwoPointFourGhz => reference.TwoPointFourGhz,
        Band.FiveGhz => reference.FiveGhz,
        Band.SixGhz => reference.SixGhz,
        _ => throw new ArgumentOutOfRangeException(nameof(band)),
    };

    private readonly record struct MaterialReference(double ReferenceThicknessMeters, double TwoPointFourGhz, double FiveGhz, double SixGhz);
}
