using Pulsemap.App.Core.Settings;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class SettingsViewModelTests
{
    private readonly FakeLocalizationService _localizationService = new();
    private readonly FakeAppLogger _logger = new();
    private readonly FakeAppSettingsService _appSettingsService = new();

    private SettingsViewModel CreateSut() => new(_localizationService, _logger, _appSettingsService);

    [Fact]
    public async Task LoadCommand_ReadsCheckForUpdatesEnabledFromSettings()
    {
        _appSettingsService.SettingsToReturn = new AppSettings { CheckForUpdatesEnabled = false };
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.False(sut.CheckForUpdatesEnabled);
    }

    [Fact]
    public async Task LoadCommand_DoesNotPersistTheValueItJustLoaded()
    {
        _appSettingsService.SettingsToReturn = new AppSettings { CheckForUpdatesEnabled = false };
        var sut = CreateSut();

        await sut.LoadCommand.ExecuteAsync(null);

        Assert.Null(_appSettingsService.LastSaved);
    }

    [Fact]
    public async Task TogglingCheckForUpdatesEnabled_PersistsTheNewValue()
    {
        var sut = CreateSut();
        await sut.LoadCommand.ExecuteAsync(null);

        sut.CheckForUpdatesEnabled = false;

        Assert.NotNull(_appSettingsService.LastSaved);
        Assert.False(_appSettingsService.LastSaved.CheckForUpdatesEnabled);
    }
}
