using Pulsemap.App.Core.Updates;

namespace Pulsemap.App.Core.Tests.Updates;

public sealed class SemanticVersionComparerTests
{
    [Theory]
    [InlineData("1.2.3", "v1.3.0")]
    [InlineData("1.2.3", "v2.0.0")]
    [InlineData("v1.2.3", "V1.2.4")]
    public void IsNewer_CandidateAhead_ReturnsTrue(string current, string candidate)
    {
        Assert.True(SemanticVersionComparer.IsNewer(current, candidate));
    }

    [Theory]
    [InlineData("1.2.3", "v1.2.3")]
    [InlineData("1.2.3", "v1.2.2")]
    [InlineData("2.0.0", "v1.9.9")]
    public void IsNewer_CandidateNotAhead_ReturnsFalse(string current, string candidate)
    {
        Assert.False(SemanticVersionComparer.IsNewer(current, candidate));
    }

    [Fact]
    public void IsNewer_FourPartAssemblyVersionAgainstThreePartTag_ExactMatchIsNotNewer()
    {
        // MSBuild always pads a 3-part <Version> to a 4-part AssemblyVersion (X.Y.Z.0) — the same
        // release should never appear "newer than itself" just because of that padding.
        Assert.False(SemanticVersionComparer.IsNewer("1.2.3.0", "v1.2.3"));
    }

    [Theory]
    [InlineData("not-a-version", "v1.0.0")]
    [InlineData("1.0.0", "not-a-version")]
    [InlineData("", "")]
    public void IsNewer_UnparseableInput_ReturnsFalse(string current, string candidate)
    {
        Assert.False(SemanticVersionComparer.IsNewer(current, candidate));
    }
}
