using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Interpolation;

public readonly record struct CoverageSample(Point2D Position, double ValueDbm);
