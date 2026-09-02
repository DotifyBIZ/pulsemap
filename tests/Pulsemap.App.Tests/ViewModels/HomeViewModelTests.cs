using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class HomeViewModelTests
{
    [Fact]
    public async Task LoadCommand_PopulatesSurveysFromLibrary()
    {
        var libraryService = new FakeSurveyLibraryService
        {
            SurveysToReturn = [new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now)],
        };
        var sut = new HomeViewModel(libraryService);

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Single(sut.Surveys);
        Assert.Equal("Survey A", sut.Surveys[0].Name);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task LoadCommand_ClearsPreviousResultsBeforeReloading()
    {
        var libraryService = new FakeSurveyLibraryService
        {
            SurveysToReturn = [new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now)],
        };
        var sut = new HomeViewModel(libraryService);
        await sut.LoadCommand.ExecuteAsync(null);

        libraryService.SurveysToReturn = [];
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Empty(sut.Surveys);
    }

    [Fact]
    public async Task LoadCommand_CapsToTheThreeMostRecentSurveys()
    {
        var libraryService = new FakeSurveyLibraryService
        {
            // Home is a dashboard, not the library — ListSurveysAsync already returns
            // newest-first, so the cap should keep exactly the first three as given.
            SurveysToReturn =
            [
                new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now),
                new SurveySummary("C:\\Surveys\\B.pulsemap", "Survey B", null, DateTimeOffset.Now),
                new SurveySummary("C:\\Surveys\\C.pulsemap", "Survey C", null, DateTimeOffset.Now),
                new SurveySummary("C:\\Surveys\\D.pulsemap", "Survey D", null, DateTimeOffset.Now),
            ],
        };
        var sut = new HomeViewModel(libraryService);

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, sut.Surveys.Count);
        Assert.Equal(["Survey A", "Survey B", "Survey C"], sut.Surveys.Select(s => s.Name));
    }
}
