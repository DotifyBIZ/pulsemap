using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Services;

namespace Pulsemap.App.ViewModels;

public partial class HomeViewModel(ISurveyLibraryService surveyLibraryService) : ObservableObject
{
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
            foreach (var summary in summaries)
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
