using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Services;

namespace Pulsemap.App.ViewModels;

public sealed partial class SurveysViewModel(ISurveyLibraryService surveyLibraryService, ILocalizationService localizationService, IAppLogger logger) : ObservableObject
{
    public ObservableCollection<SurveySummary> Surveys { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;

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

    [RelayCommand]
    private async Task DeleteAsync(SurveySummary summary)
    {
        ErrorMessage = null;
        try
        {
            await surveyLibraryService.DeleteAsync(summary.FilePath);
            Surveys.Remove(summary);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, localizationService.GetString("SurveysDeleteErrorFormat"), ex.Message);
            await logger.LogErrorAsync("Failed to delete survey.", ex);
        }
    }

    [RelayCommand]
    private async Task RenameAsync((SurveySummary Summary, string NewName) args)
    {
        ErrorMessage = null;
        try
        {
            await surveyLibraryService.RenameAsync(args.Summary.FilePath, args.NewName);
            await LoadAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, localizationService.GetString("SurveysRenameErrorFormat"), ex.Message);
            await logger.LogErrorAsync("Failed to rename survey.", ex);
        }
    }
}
