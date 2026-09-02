using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Pulsemap.App.Services;

/// <summary>Wraps the native file-save picker — unpackaged apps need the owning HWND wired in via InitializeWithWindow before the picker can show.</summary>
public sealed class SurveyExportFilePickerService : ISurveyExportFilePickerService
{
    public async Task<Stream?> PickSaveStreamAsync(string suggestedFileNameWithoutExtension, string extension, string fileTypeDescription, CancellationToken cancellationToken = default)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, App.WindowHandle);

        picker.SuggestedFileName = suggestedFileNameWithoutExtension;
        picker.DefaultFileExtension = extension;
        picker.FileTypeChoices.Add(fileTypeDescription, [extension]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return null;
        }

        // Same pattern as FloorPlanFilePickerService: pull the plain path out of the picker
        // result and use ordinary System.IO rather than StorageFile's own stream-opening APIs.
        return new FileStream(file.Path, FileMode.Create, FileAccess.Write);
    }
}
