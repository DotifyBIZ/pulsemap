namespace Pulsemap.App.Services;

public interface ISurveyLibraryService
{
    string SurveysDirectory { get; }

    Task<IReadOnlyList<SurveySummary>> ListSurveysAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(string filePath, CancellationToken cancellationToken = default);

    Task RenameAsync(string filePath, string newName, CancellationToken cancellationToken = default);
}
