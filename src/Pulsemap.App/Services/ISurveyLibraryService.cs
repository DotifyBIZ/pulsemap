namespace Pulsemap.App.Services;

public interface ISurveyLibraryService
{
    string SurveysDirectory { get; }

    Task<IReadOnlyList<SurveySummary>> ListSurveysAsync(CancellationToken cancellationToken = default);
}
