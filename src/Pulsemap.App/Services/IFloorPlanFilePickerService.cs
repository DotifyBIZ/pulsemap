namespace Pulsemap.App.Services;

public interface IFloorPlanFilePickerService
{
    /// <summary>Shows a native file picker for a floor plan image or PDF. Returns null if the user cancels.</summary>
    Task<FloorPlanFilePickResult?> PickFloorPlanFileAsync();
}
