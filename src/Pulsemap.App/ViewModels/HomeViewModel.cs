using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Settings;
using Pulsemap.App.Services;
using Windows.System;

namespace Pulsemap.App.ViewModels;

public partial class HomeViewModel(
    ISurveyLibraryService surveyLibraryService,
    IUpdateCheckService updateCheckService,
    IAppSettingsService appSettingsService,
    ILocalizationService localizationService) : ObservableObject
{
    // Home is a dashboard, not the library — the Surveys nav tab shows the full, unlimited list.
    private const int RecentSurveysLimit = 3;

    private string? _releaseUrl;

    public ObservableCollection<SurveySummary> Surveys { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateAvailable))]
    public partial string? UpdateBannerMessage { get; set; }

    public bool HasUpdateAvailable => UpdateBannerMessage is not null;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var summaries = await surveyLibraryService.ListSurveysAsync();
            Surveys.Clear();
            foreach (var summary in summaries.Take(RecentSurveysLimit))
            {
                Surveys.Add(summary);
            }

            await CheckForUpdateAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CheckForUpdateAsync()
    {
        var settings = await appSettingsService.LoadAsync();
        if (!settings.CheckForUpdatesEnabled)
        {
            return;
        }

        var result = await updateCheckService.CheckForUpdateAsync();
        if (result.IsUpdateAvailable)
        {
            UpdateBannerMessage = string.Format(CultureInfo.CurrentCulture, localizationService.GetString("HomeUpdateAvailableFormat"), result.LatestVersion);
            _releaseUrl = result.ReleaseUrl;
        }
    }

    [RelayCommand]
    private async Task OpenReleaseAsync()
    {
        if (_releaseUrl is { } url)
        {
            await Launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
