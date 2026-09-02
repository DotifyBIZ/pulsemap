using Pulsemap.App.Services;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeSurveyLibraryService : ISurveyLibraryService
{
    public string SurveysDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "PulsemapTests");

    public IReadOnlyList<SurveySummary> SurveysToReturn { get; set; } = [];

    public Exception? ExceptionToThrow { get; set; }

    public List<string> DeletedFilePaths { get; } = [];

    public List<(string FilePath, string NewName)> RenamedSurveys { get; } = [];

    public Task<IReadOnlyList<SurveySummary>> ListSurveysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SurveysToReturn);

    public Task DeleteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        DeletedFilePaths.Add(filePath);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string filePath, string newName, CancellationToken cancellationToken = default)
    {
        if (ExceptionToThrow is not null)
        {
            throw ExceptionToThrow;
        }

        RenamedSurveys.Add((filePath, newName));
        return Task.CompletedTask;
    }
}
