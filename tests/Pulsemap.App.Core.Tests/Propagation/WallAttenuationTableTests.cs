using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Core.Tests.Propagation;

public sealed class WallAttenuationTableTests
{
    [Fact]
    public void GetAttenuationDb_NullMaterial_ReturnsGenericDrywallFallback()
    {
        double fallback = WallAttenuationTable.GetAttenuationDb(material: null, thicknessMeters: null, Band.FiveGhz);
        double drywall = WallAttenuationTable.GetAttenuationDb(WallMaterial.Drywall, thicknessMeters: null, Band.FiveGhz);

        Assert.Equal(drywall, fallback);
    }

    [Fact]
    public void GetAttenuationDb_ThicknessDoubleReferenceThickness_DoublesAttenuation()
    {
        double atReference = WallAttenuationTable.GetAttenuationDb(WallMaterial.Concrete, thicknessMeters: 0.15, Band.TwoPointFourGhz);
        double atDoubleReference = WallAttenuationTable.GetAttenuationDb(WallMaterial.Concrete, thicknessMeters: 0.30, Band.TwoPointFourGhz);

        Assert.Equal(atReference * 2, atDoubleReference, precision: 9);
    }

    [Fact]
    public void GetAttenuationDb_NoThicknessGiven_UsesBaseValueAtReferenceThickness()
    {
        double withoutThickness = WallAttenuationTable.GetAttenuationDb(WallMaterial.Brick, thicknessMeters: null, Band.SixGhz);
        double atReferenceThickness = WallAttenuationTable.GetAttenuationDb(WallMaterial.Brick, thicknessMeters: 0.15, Band.SixGhz);

        Assert.Equal(atReferenceThickness, withoutThickness, precision: 9);
    }

    [Theory]
    [InlineData(WallMaterial.GlassStandard, WallMaterial.GlassLowE)]
    [InlineData(WallMaterial.Drywall, WallMaterial.Concrete)]
    [InlineData(WallMaterial.Concrete, WallMaterial.ReinforcedConcrete)]
    public void GetAttenuationDb_DenserMaterial_AttenuatesMoreThanLighterMaterial(WallMaterial lighter, WallMaterial denser)
    {
        foreach (var band in new[] { Band.TwoPointFourGhz, Band.FiveGhz, Band.SixGhz })
        {
            double lighterDb = WallAttenuationTable.GetAttenuationDb(lighter, thicknessMeters: null, band);
            double denserDb = WallAttenuationTable.GetAttenuationDb(denser, thicknessMeters: null, band);

            Assert.True(denserDb > lighterDb, $"{denser} should attenuate more than {lighter} at {band}.");
        }
    }

    [Theory]
    [InlineData(WallMaterial.Drywall)]
    [InlineData(WallMaterial.Concrete)]
    public void GetAttenuationDb_HigherBand_AttenuatesAtLeastAsMuchAsLowerBand(WallMaterial material)
    {
        double at24 = WallAttenuationTable.GetAttenuationDb(material, thicknessMeters: null, Band.TwoPointFourGhz);
        double at5 = WallAttenuationTable.GetAttenuationDb(material, thicknessMeters: null, Band.FiveGhz);
        double at6 = WallAttenuationTable.GetAttenuationDb(material, thicknessMeters: null, Band.SixGhz);

        Assert.True(at5 >= at24);
        Assert.True(at6 >= at5);
    }
}
