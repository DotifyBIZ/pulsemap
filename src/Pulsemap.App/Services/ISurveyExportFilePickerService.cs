namespace Pulsemap.App.Services;

public interface ISurveyExportFilePickerService
{
    /// <summary>Shows a native save-file picker for an export. Returns an open, writable stream
    /// ready for the caller to write to, or null if the user cancels.</summary>
    Task<Stream?> PickSaveStreamAsync(string suggestedFileNameWithoutExtension, string extension, string fileTypeDescription, CancellationToken cancellationToken = default);
}
