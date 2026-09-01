namespace Pulsemap.App.Core.Models;

public sealed class BandRadioSettings
{
    public required double TransmitPowerDbm { get; set; }

    public required int Channel { get; set; }
}
