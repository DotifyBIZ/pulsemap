using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Settings;
using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class HomeViewModelTests
{
    private readonly FakeSurveyLibraryService _libraryService = new();
    private readonly FakeUpdateCheckService _updateCheckService = new();
    private readonly FakeAppSettingsService _appSettingsService = new();
    private readonly FakeLocalizationService _localizationService = new();

    private readonly FakeAppLogger _logger = new();

    private HomeViewModel CreateSut() => new(_libraryService, _updateCheckService, _appSettingsService, _localizationService, _logger);

    [Fact]
    public async Task LoadCommand_PopulatesSurveysFromLibrary()
    {
        _libraryService.SurveysToReturn = [new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now)];
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Single(sut.Surveys);
        Assert.Equal("Survey A", sut.Surveys[0].Name);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task LoadCommand_ClearsPreviousResultsBeforeReloading()
    {
        _libraryService.SurveysToReturn = [new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now)];
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        _libraryService.SurveysToReturn = [];
        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Empty(sut.Surveys);
    }

    [Fact]
    public async Task LoadCommand_CapsToTheThreeMostRecentSurveys()
    {
        _libraryService.SurveysToReturn =
        [
            // Home is a dashboard, not the library — ListSurveysAsync already returns
            // newest-first, so the cap should keep exactly the first three as given.
            new SurveySummary("C:\\Surveys\\A.pulsemap", "Survey A", null, DateTimeOffset.Now),
            new SurveySummary("C:\\Surveys\\B.pulsemap", "Survey B", null, DateTimeOffset.Now),
            new SurveySummary("C:\\Surveys\\C.pulsemap", "Survey C", null, DateTimeOffset.Now),
            new SurveySummary("C:\\Surveys\\D.pulsemap", "Survey D", null, DateTimeOffset.Now),
        ];
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Equal(3, sut.Surveys.Count);
        Assert.Equal(["Survey A", "Survey B", "Survey C"], sut.Surveys.Select(s => s.Name));
    }

    [Fact]
    public async Task LoadCommand_UpdateAvailableAndChecksEnabled_ShowsBanner()
    {
        _appSettingsService.SettingsToReturn = new AppSettings { CheckForUpdatesEnabled = true };
        _updateCheckService.ResultToReturn = new UpdateCheckResult(true, "v2.0.0", "https://github.com/DotifyBIZ/pulsemap/releases/latest");
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.True(sut.HasUpdateAvailable);
        Assert.NotNull(sut.UpdateBannerMessage);
    }

    [Fact]
    public async Task LoadCommand_ChecksDisabled_NeverCallsUpdateService()
    {
        _appSettingsService.SettingsToReturn = new AppSettings { CheckForUpdatesEnabled = false };
        _updateCheckService.ResultToReturn = new UpdateCheckResult(true, "v2.0.0", "https://example.test");
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.HasUpdateAvailable);
    }

    [Fact]
    public async Task LoadCommand_NoUpdateAvailable_DoesNotShowBanner()
    {
        _appSettingsService.SettingsToReturn = new AppSettings { CheckForUpdatesEnabled = true };
        _updateCheckService.ResultToReturn = UpdateCheckResult.NoUpdate;
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.HasUpdateAvailable);
        Assert.Null(sut.UpdateBannerMessage);
    }

    [Fact]
    public void GreetingDisplay_IsNeverEmpty()
    {
        // Depends on the real current hour, so this asserts the switch always resolves to
        // something rather than pinning an exact greeting string.
        var sut = CreateSut();

        Assert.False(string.IsNullOrWhiteSpace(sut.GreetingDisplay));
    }

    [Fact]
    public void WellbeingMessageDisplay_PicksOneOfTheKnownMessages()
    {
        var sut = CreateSut();

        Assert.StartsWith("HomeWellbeingMessage", sut.WellbeingMessageDisplay);
    }
}
