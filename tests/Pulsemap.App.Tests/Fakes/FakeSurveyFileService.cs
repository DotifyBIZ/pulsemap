using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;

namespace Pulsemap.App.Tests.Fakes;

internal sealed class FakeSurveyFileService : ISurveyFileService
{
    public List<(Survey Survey, string FilePath)> SaveCalls { get; } = [];

    public Survey? SurveyToReturn { get; set; }

    public Exception? LoadExceptionToThrow { get; set; }

    public Exception? SaveExceptionToThrow { get; set; }

    public Task SaveAsync(Survey survey, string filePath, CancellationToken cancellationToken = default)
    {
        SaveCalls.Add((survey, filePath));

        if (SaveExceptionToThrow is not null)
        {
            throw SaveExceptionToThrow;
        }

        return Task.CompletedTask;
    }

    public Task<Survey> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (LoadExceptionToThrow is not null)
        {
            throw LoadExceptionToThrow;
        }

        return Task.FromResult(SurveyToReturn ?? throw new InvalidOperationException("SurveyToReturn was not configured for this test."));
    }
}
