using Pulsemap.App.Core.Models;

namespace Pulsemap.App.Core.Persistence;

public interface ISurveyFileService
{
    Task SaveAsync(Survey survey, string filePath, CancellationToken cancellationToken = default);

    Task<Survey> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
