using Pulsemap.App.Services;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeSurveyLibraryService : ISurveyLibraryService
{
    public string SurveysDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "PulsemapTests");

    public IReadOnlyList<SurveySummary> SurveysToReturn { get; set; } = [];

    public Task<IReadOnlyList<SurveySummary>> ListSurveysAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SurveysToReturn);
}
