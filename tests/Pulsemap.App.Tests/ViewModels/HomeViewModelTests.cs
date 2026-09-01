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
}
