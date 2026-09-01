using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pulsemap.App.Controls;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Views;

public sealed partial class WorkspacePage : Page
{
    private readonly IAppLogger _logger = App.Services.GetRequiredService<IAppLogger>();

    public WorkspaceViewModel ViewModel { get; }

    public WorkspacePage()
    {
        ViewModel = App.Services.GetRequiredService<WorkspaceViewModel>();
        InitializeComponent();

        ViewModel.FloorChanged += async (_, _) => await RenderCanvasAsync();
        PlanCanvas.TestPointRequested += async (_, position) => await ViewModel.AddTestPointAsync(position);
        PlanCanvas.WallRequested += async (_, span) => await ViewModel.AddWallAsync(span.Start, span.End);
        PlanCanvas.DeleteRequested += async (_, position) => await ViewModel.DeleteNearestElementAsync(position);
        SelectToolButton.IsChecked = true;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string filePath)
        {
            await ViewModel.LoadAsync(filePath);
            PopulateBandSelector();
            await RenderCanvasAsync();
        }
    }

    private async Task RenderCanvasAsync()
    {
        if (ViewModel.Survey is null)
        {
            return;
        }

        try
        {
            await PlanCanvas.RenderAsync(ViewModel.Survey.Floor, ViewModel.Heatmap, ViewModel.CurrentWalkPoint);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Failed to render the floor plan canvas.", ex);
        }
    }

    private void PopulateBandSelector()
    {
        BandSelector.Items.Clear();
        foreach (var band in ViewModel.AvailableBands)
        {
            BandSelector.Items.Add(new ComboBoxItem { Content = ViewModel.BandDisplayName(band), Tag = band });
        }

        if (BandSelector.Items.Count > 0)
        {
            BandSelector.SelectedIndex = 0;
        }
    }

    private void BandSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [ComboBoxItem { Tag: Band band }, ..])
        {
            ViewModel.SelectedBand = band;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    private void ToolToggle_Checked(object sender, RoutedEventArgs e)
    {
        foreach (var button in new[] { SelectToolButton, AddTestPointToolButton, DrawWallToolButton, DeleteToolButton })
        {
            if (!ReferenceEquals(button, sender))
            {
                button.IsChecked = false;
            }
        }

        PlanCanvas.Tool = ReferenceEquals(sender, AddTestPointToolButton) ? WorkspaceTool.AddTestPoint
            : ReferenceEquals(sender, DrawWallToolButton) ? WorkspaceTool.DrawWall
            : ReferenceEquals(sender, DeleteToolButton) ? WorkspaceTool.DeleteElement
            : WorkspaceTool.Select;
    }
}
