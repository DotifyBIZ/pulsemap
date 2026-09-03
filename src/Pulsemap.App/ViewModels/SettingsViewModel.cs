using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Settings;
using Pulsemap.App.Services;
using Windows.System;

namespace Pulsemap.App.ViewModels;

/// <summary>Pulsemap follows Windows' own display language setting rather than offering an
/// in-app override — see ILocalizationService for why.</summary>
public sealed partial class SettingsViewModel(ILocalizationService localizationService, IAppLogger logger, IAppSettingsService appSettingsService) : ObservableObject
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

    // Pulsemap's only network call (see docs/adr/0004-update-check-network-call.md) — a version
    // check against GitHub, on by default but visibly disableable here.
    private bool _suppressSave;

    [ObservableProperty]
    public partial bool CheckForUpdatesEnabled { get; set; } = true;

    [RelayCommand]
    private async Task LoadAsync()
    {
        var settings = await appSettingsService.LoadAsync();
        _suppressSave = true;
        CheckForUpdatesEnabled = settings.CheckForUpdatesEnabled;
        _suppressSave = false;
    }

    partial void OnCheckForUpdatesEnabledChanged(bool value)
    {
        if (_suppressSave)
        {
            return;
        }

        _ = SaveCheckForUpdatesAsync(value);
    }

    // Load-modify-save, not save-a-fresh-AppSettings: settings.json holds more than this one
    // preference (HasSeenWorkspaceOnboarding lives there too), and writing a default-constructed
    // AppSettings silently reset everything else — toggling this switch replayed the Workspace
    // first-run tour.
    private async Task SaveCheckForUpdatesAsync(bool value)
    {
        var settings = await appSettingsService.LoadAsync();
        settings.CheckForUpdatesEnabled = value;
        await appSettingsService.SaveAsync(settings);
    }
}
