using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Views;

public sealed partial class SnapshotComparisonPage : Page
{
    private readonly IAppLogger _logger = App.Services.GetRequiredService<IAppLogger>();

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

        if (e.Parameter is Survey survey)
        {
            ViewModel.Initialize(survey);
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
