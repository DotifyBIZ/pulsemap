using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Interpolation;

public interface IKrigingInterpolator
{
    /// <summary>Interpolates a value at every position in <paramref name="queryPositions"/> from one shared set of samples.</summary>
    IReadOnlyList<double> Interpolate(IReadOnlyList<CoverageSample> samples, IReadOnlyList<Point2D> queryPositions);

    /// <summary>Estimation variance (kriging's own measure of how uncertain each interpolated value
    /// is) at every position in <paramref name="queryPositions"/> — higher means less confident.
    /// Drives adaptive test-point suggestion: the next point worth walking to is wherever this is
    /// highest, not a fixed grid.</summary>
    IReadOnlyList<double> InterpolateVariance(IReadOnlyList<CoverageSample> samples, IReadOnlyList<Point2D> queryPositions);
}
