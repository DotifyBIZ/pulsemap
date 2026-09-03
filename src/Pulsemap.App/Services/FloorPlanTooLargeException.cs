namespace Pulsemap.App.Services;

/// <summary>Thrown when a picked floor plan exceeds
/// <see cref="FloorPlanFilePickerService.MaxFloorPlanBytes"/>. A distinct type (rather than a null
/// result) so the wizard can tell "the user cancelled" apart from "that file is too big" and say
/// so, instead of silently doing nothing.</summary>
public sealed class FloorPlanTooLargeException(long actualBytes, long maxBytes)
    : Exception($"The selected floor plan is {actualBytes} bytes; the maximum supported size is {maxBytes} bytes.")
{
    public long ActualBytes { get; } = actualBytes;

    public long MaxBytes { get; } = maxBytes;
}
