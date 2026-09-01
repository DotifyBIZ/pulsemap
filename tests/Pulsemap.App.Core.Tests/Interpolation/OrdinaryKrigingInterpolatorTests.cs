using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Tests.Interpolation;

public sealed class OrdinaryKrigingInterpolatorTests
{
    private readonly OrdinaryKrigingInterpolator _sut = new();

    [Fact]
    public void Interpolate_QueryAtSamplePositions_ReproducesSampleValuesExactly()
    {
        CoverageSample[] samples =
        [
            new(new Point2D(0, 0), -40),
            new(new Point2D(10, 0), -55),
            new(new Point2D(0, 10), -48),
            new(new Point2D(10, 10), -62),
            new(new Point2D(5, 15), -50),
        ];

        var queryPositions = samples.Select(s => s.Position).ToArray();
        var results = _sut.Interpolate(samples, queryPositions);

        for (int i = 0; i < samples.Length; i++)
        {
            Assert.Equal(samples[i].ValueDbm, results[i], precision: 6);
        }
    }

    [Fact]
    public void Interpolate_TwoSamplesSymmetricAroundQuery_ReturnsAverage()
    {
        CoverageSample[] samples =
        [
            new(new Point2D(0, 0), -30),
            new(new Point2D(20, 0), -50),
        ];
        var midpoint = new Point2D(10, 0);

        var result = _sut.Interpolate(samples, [midpoint]);

        Assert.Equal(-40, result[0], precision: 6);
    }

    [Fact]
    public void Interpolate_AllSamplesEqual_ReturnsThatConstantEverywhere()
    {
        CoverageSample[] samples =
        [
            new(new Point2D(0, 0), -45),
            new(new Point2D(10, 0), -45),
            new(new Point2D(0, 10), -45),
        ];
        Point2D[] queries = [new Point2D(3, 3), new Point2D(100, 100)];

        var results = _sut.Interpolate(samples, queries);

        Assert.All(results, value => Assert.Equal(-45, value, precision: 9));
    }

    [Fact]
    public void Interpolate_SingleSample_ReturnsItsValueEverywhere()
    {
        CoverageSample[] samples = [new(new Point2D(5, 5), -38)];
        Point2D[] queries = [new Point2D(0, 0), new Point2D(50, 50)];

        var results = _sut.Interpolate(samples, queries);

        Assert.All(results, value => Assert.Equal(-38, value, precision: 9));
    }

    [Fact]
    public void Interpolate_NoSamples_Throws()
    {
        Assert.Throws<ArgumentException>(() => _sut.Interpolate([], [new Point2D(0, 0)]));
    }
}
