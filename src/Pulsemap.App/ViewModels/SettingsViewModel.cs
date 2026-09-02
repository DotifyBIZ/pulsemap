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
    private async Task OpenLogsFolderAsync()
    {
        try
        {
            // FileAppLogger only creates this folder lazily, on its first write — on a fresh
            // install with no errors logged yet, it doesn't exist, and LaunchFolderPathAsync
            // fails outright on a path that isn't there. Ensure it exists before launching.
            Directory.CreateDirectory(logger.LogDirectory);
            await Launcher.LaunchFolderPathAsync(logger.LogDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await logger.LogErrorAsync("Failed to open the logs folder.", ex);
        }
    }
}
