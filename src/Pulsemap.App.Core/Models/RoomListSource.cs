namespace Pulsemap.App.Core.Models;

/// <summary>A structured room/zone list used with no floor plan image at all.</summary>
public sealed class RoomListSource : FloorPlanSource
{
    public List<RoomListEntry> Rooms { get; init; } = [];
}
