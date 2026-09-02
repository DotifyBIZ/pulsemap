using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakePropagationModel : IPropagationModel
{
    public double SignalDbmToReturn { get; set; } = -50;

    public double PredictSignalDbm(Point2D transmitterPosition, double transmitPowerDbm, Point2D receiverPosition, Band band, IReadOnlyList<Wall> walls) =>
        SignalDbmToReturn;
}
