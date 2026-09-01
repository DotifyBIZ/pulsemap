using Pulsemap.App.Services;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeSurveyExportFilePickerService : ISurveyExportFilePickerService
{
    public Stream? StreamToReturn { get; set; }

    public string? LastSuggestedFileName { get; private set; }

    public string? LastExtension { get; private set; }

    public Task<Stream?> PickSaveStreamAsync(string suggestedFileNameWithoutExtension, string extension, string fileTypeDescription, CancellationToken cancellationToken = default)
    {
        LastSuggestedFileName = suggestedFileNameWithoutExtension;
        LastExtension = extension;
        return Task.FromResult(StreamToReturn);
    }
}
