using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Services;
using Windows.System;

namespace Pulsemap.App.ViewModels;

/// <summary>Pulsemap follows Windows' own display language setting rather than offering an
/// in-app override — see ILocalizationService for why.</summary>
public sealed partial class SettingsViewModel(ILocalizationService localizationService, IAppLogger logger)
{
    public string CurrentLanguageDisplay =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, localizationService.GetString("SettingsCurrentLanguageFormat"), localizationService.CurrentLanguage);

    [RelayCommand]
    private static async Task OpenLanguageSettingsAsync() =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage-setdisplaylanguage"));

    public string LogDirectoryDisplay => logger.LogDirectory;

    [RelayCommand]
    private async Task OpenLogsFolderAsync() =>
        await Launcher.LaunchFolderPathAsync(logger.LogDirectory);
}
