namespace Pulsemap.App.Core.Models;

public sealed class RoomListEntry
{
    public required string Name { get; set; }

    public required double WidthMeters { get; set; }

    public required double LengthMeters { get; set; }
}
