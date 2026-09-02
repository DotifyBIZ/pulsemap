using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class SurveysViewModelTests
{
    private static SurveysViewModel CreateSut(FakeSurveyLibraryService libraryService, FakeLocalizationService? localizationService = null, FakeAppLogger? logger = null) =>
        new(libraryService, localizationService ?? new FakeLocalizationService(), logger ?? new FakeAppLogger());

    [Fact]
    public async Task LoadCommand_PopulatesEveryReturnedSurvey()
    {
        var libraryService = new FakeSurveyLibraryService
        {
            SurveysToReturn =
            [
                new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now),
                new SurveySummary("C:\\Surveys\\B.pulsemap", "Survey B", null, DateTimeOffset.Now),
            ],
        };
        var sut = CreateSut(libraryService);

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.Surveys.Count);
    }

    [Fact]
    public async Task DeleteCommand_RemovesTheSurveyFromTheListOnSuccess()
    {
        var summary = new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now);
        var libraryService = new FakeSurveyLibraryService { SurveysToReturn = [summary] };
        var sut = CreateSut(libraryService);
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.DeleteCommand.ExecuteAsync(summary);

        Assert.Empty(sut.Surveys);
        Assert.Contains(summary.FilePath, libraryService.DeletedFilePaths);
    }

    [Fact]
    public async Task DeleteCommand_SetsErrorMessageAndLogsOnFailure()
    {
        var summary = new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now);
        var libraryService = new FakeSurveyLibraryService
        {
            SurveysToReturn = [summary],
            ExceptionToThrow = new IOException("disk error"),
        };
        var logger = new FakeAppLogger();
        var sut = CreateSut(libraryService, logger: logger);
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.DeleteCommand.ExecuteAsync(summary);

        Assert.True(sut.HasError);
        Assert.Single(sut.Surveys);
        Assert.Single(logger.ErrorMessages);
    }

    [Fact]
    public async Task RenameCommand_ReloadsTheListOnSuccess()
    {
        var summary = new SurveySummary("C:\\Surveys\\A.pulsemap", "Old Name", null, DateTimeOffset.Now);
        var renamed = new SurveySummary("C:\\Surveys\\A.pulsemap", "New Name", null, DateTimeOffset.Now);
        var libraryService = new FakeSurveyLibraryService { SurveysToReturn = [summary] };
        var sut = CreateSut(libraryService);
        await sut.LoadCommand.ExecuteAsync(null);

        libraryService.SurveysToReturn = [renamed];
        await sut.RenameCommand.ExecuteAsync((summary, "New Name"));

        Assert.Contains(summary.FilePath, libraryService.RenamedSurveys.Select(r => r.FilePath));
        Assert.Equal("New Name", Assert.Single(sut.Surveys).Name);
    }

    [Fact]
    public async Task RenameCommand_SetsErrorMessageAndLogsOnFailure()
    {
        var summary = new SurveySummary("C:\\Surveys\\A.pulsemap", "Old Name", null, DateTimeOffset.Now);
        var libraryService = new FakeSurveyLibraryService
        {
            SurveysToReturn = [summary],
            ExceptionToThrow = new UnauthorizedAccessException("locked"),
        };
        var logger = new FakeAppLogger();
        var sut = CreateSut(libraryService, logger: logger);
        await sut.LoadCommand.ExecuteAsync(null);

        await sut.RenameCommand.ExecuteAsync((summary, "New Name"));

        Assert.True(sut.HasError);
        Assert.Single(logger.ErrorMessages);
    }
}
