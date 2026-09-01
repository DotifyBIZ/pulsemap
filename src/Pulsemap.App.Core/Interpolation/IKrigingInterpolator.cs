using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Interpolation;

public interface IKrigingInterpolator
{
    /// <summary>Interpolates a value at every position in <paramref name="queryPositions"/> from one shared set of samples.</summary>
    IReadOnlyList<double> Interpolate(IReadOnlyList<CoverageSample> samples, IReadOnlyList<Point2D> queryPositions);
}
