using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Services;

namespace Pulsemap.App.ViewModels;

public partial class HomeViewModel(ISurveyLibraryService surveyLibraryService) : ObservableObject
{
    // Home is a dashboard, not the library — the Surveys nav tab shows the full, unlimited list.
    private const int RecentSurveysLimit = 3;

    public ObservableCollection<SurveySummary> Surveys { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

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
        }
        finally
        {
            IsLoading = false;
        }
    }
}
