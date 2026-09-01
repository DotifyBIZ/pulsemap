using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pulsemap.App.Services;

/// <summary>Wraps the native file-open picker — unpackaged apps need the owning HWND wired in via InitializeWithWindow before the picker can show.</summary>
public sealed class FloorPlanFilePickerService : IFloorPlanFilePickerService
{
    public async Task<FloorPlanFilePickResult?> PickFloorPlanFileAsync()
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, App.WindowHandle);

        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".pdf");

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        byte[] imageData = await File.ReadAllBytesAsync(file.Path);
        return new FloorPlanFilePickResult(file.Name, Path.GetExtension(file.Path), imageData);
    }
}
