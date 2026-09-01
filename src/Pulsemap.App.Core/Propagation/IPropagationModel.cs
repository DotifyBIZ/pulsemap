using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Propagation;

public interface IPropagationModel
{
    /// <summary>Predicts received signal strength in dBm at <paramref name="receiverPosition"/>, accounting for free-space path loss and every wall the direct line to the transmitter crosses.</summary>
    double PredictSignalDbm(Point2D transmitterPosition, double transmitPowerDbm, Point2D receiverPosition, Band band, IReadOnlyList<Wall> walls);
}
