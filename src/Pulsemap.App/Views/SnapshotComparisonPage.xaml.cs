using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Services;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Views;

public sealed partial class SnapshotComparisonPage : Page
{
    private readonly IAppLogger _logger = App.Services.GetRequiredService<IAppLogger>();
    private readonly ILocalizationService _localizationService = App.Services.GetRequiredService<ILocalizationService>();

    public SnapshotComparisonViewModel ViewModel { get; }

    public SnapshotComparisonPage()
    {
        ViewModel = App.Services.GetRequiredService<SnapshotComparisonViewModel>();
        InitializeComponent();

        ViewModel.Changed += async (_, _) => await RenderCanvasesAsync();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is (Survey survey, string filePath))
        {
            ViewModel.Initialize(survey, filePath);
        }
    }

    private async void DeleteLeftSnapshot_Click(object sender, RoutedEventArgs e) => await ConfirmAndDeleteSnapshotAsync(ViewModel.DeleteLeftSnapshotCommand, ViewModel.LeftOption?.Label);

    private async void DeleteRightSnapshot_Click(object sender, RoutedEventArgs e) => await ConfirmAndDeleteSnapshotAsync(ViewModel.DeleteRightSnapshotCommand, ViewModel.RightOption?.Label);

    private async Task ConfirmAndDeleteSnapshotAsync(CommunityToolkit.Mvvm.Input.IAsyncRelayCommand deleteCommand, string? snapshotLabel)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localizationService.GetString("SnapshotComparisonDeleteDialogTitle"),
            Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, _localizationService.GetString("SnapshotComparisonDeleteDialogContentFormat"), snapshotLabel),
            PrimaryButtonText = _localizationService.GetString("SnapshotComparisonDeleteDialogPrimaryButton"),
            CloseButtonText = _localizationService.GetString("SnapshotComparisonDeleteDialogCloseButton"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await deleteCommand.ExecuteAsync(null);
        }
    }

    private async Task RenderCanvasesAsync()
    {
        try
        {
            await Task.WhenAll(
                ViewModel.LeftFloor is { } leftFloor ? LeftCanvas.RenderAsync(leftFloor, ViewModel.LeftHeatmap) : Task.CompletedTask,
                ViewModel.RightFloor is { } rightFloor ? RightCanvas.RenderAsync(rightFloor, ViewModel.RightHeatmap) : Task.CompletedTask);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Failed to render the snapshot comparison canvases.", ex);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();
}
