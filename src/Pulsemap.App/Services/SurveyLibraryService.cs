using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Persistence;

namespace Pulsemap.App.Services;

/// <summary>Lists the surveys saved under the default Pulsemap surveys folder. "Where surveys live by default" is an app-level policy choice; the .pulsemap file format itself is Core's concern (ISurveyFileService).</summary>
public sealed class SurveyLibraryService(ISurveyFileService surveyFileService, IAppLogger logger) : ISurveyLibraryService
{
    public string SurveysDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pulsemap", "Surveys");

    public async Task<IReadOnlyList<SurveySummary>> ListSurveysAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(SurveysDirectory))
        {
            return [];
        }

        var summaries = new List<SurveySummary>();
        foreach (var filePath in Directory.EnumerateFiles(SurveysDirectory, "*.pulsemap"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var survey = await surveyFileService.LoadAsync(filePath, cancellationToken);
                summaries.Add(new SurveySummary(filePath, survey.Name, survey.SiteDescription, survey.ModifiedAt));
            }
            catch (InvalidDataException ex)
            {
                // Not a valid Pulsemap survey — skip it rather than fail the whole list.
                await logger.LogWarningAsync($"Skipped '{filePath}' while listing surveys: {ex.Message}", cancellationToken);
            }
        }

        return summaries.OrderByDescending(summary => summary.ModifiedAt).ToList();
    }
}
