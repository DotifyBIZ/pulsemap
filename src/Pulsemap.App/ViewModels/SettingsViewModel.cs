using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Services;
using Windows.System;

namespace Pulsemap.App.ViewModels;

/// <summary>Pulsemap follows Windows' own display language setting rather than offering an
/// in-app override — see ILocalizationService for why.</summary>
public sealed partial class SettingsViewModel(ILocalizationService localizationService)
{
    public string CurrentLanguageDisplay =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, localizationService.GetString("SettingsCurrentLanguageFormat"), localizationService.CurrentLanguage);

    [RelayCommand]
    private static async Task OpenLanguageSettingsAsync() =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:regionlanguage-setdisplaylanguage"));
}
