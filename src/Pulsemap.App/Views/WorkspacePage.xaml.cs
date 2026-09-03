using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Pulsemap.App.Controls;
using Pulsemap.App.Core.Diagnostics;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Services;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Views;

public sealed partial class WorkspacePage : Page
{
    private readonly IAppLogger _logger = App.Services.GetRequiredService<IAppLogger>();
    private readonly ILocalizationService _localizationService = App.Services.GetRequiredService<ILocalizationService>();

    public WorkspaceViewModel ViewModel { get; }

    public WorkspacePage()
    {
        ViewModel = App.Services.GetRequiredService<WorkspaceViewModel>();
        InitializeComponent();

        ViewModel.FloorChanged += async (_, _) => await RenderCanvasAsync();
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ViewModel.SelectedBand))
            {
                SyncBandSelectorToViewModel();
            }
        };
        PlanCanvas.TestPointRequested += async (_, position) => await ViewModel.AddTestPointAsync(position);
        PlanCanvas.WallRequested += async (_, span) => await ViewModel.AddWallAsync(span.Start, span.End);
        PlanCanvas.DeleteRequested += async (_, position) => await ViewModel.DeleteNearestElementAsync(position);
        PlanCanvas.WallSelectRequested += async (_, position) => await OnSelectClickAsync(position);
        PlanCanvas.OutdoorBoundsChanged += async (_, bounds) => await ViewModel.UpdateOutdoorBoundsAsync(bounds.Min, bounds.Max);
        PlanCanvas.DiagnosePositionRequested += async (_, position) => await OnDiagnoseClickAsync(position);
        SelectToolButton.IsChecked = true;

        // Escape is the conventional "abandon what I started" key, and the first click of a
        // two-click wall had no other way out.
        KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Escape)
            {
                PlanCanvas.CancelPendingWall();
                args.Handled = true;
            }
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string filePath)
        {
            await ViewModel.LoadAsync(filePath);
            PopulateBandSelector();
            await RenderCanvasAsync();

            if (await ViewModel.ShouldShowOnboardingAsync())
            {
                OnboardingStep1.IsOpen = true;
            }
        }
    }

    private void OnboardingNext_Click(TeachingTip sender, object args)
    {
        sender.IsOpen = false;

        if (ReferenceEquals(sender, OnboardingStep1))
        {
            OnboardingStep2.IsOpen = true;
        }
        else if (ReferenceEquals(sender, OnboardingStep2))
        {
            OnboardingStep3.IsOpen = true;
        }
        else if (ReferenceEquals(sender, OnboardingStep3))
        {
            OnboardingStep4.IsOpen = true;
        }
        else if (ReferenceEquals(sender, OnboardingStep4))
        {
            OnboardingStep5.IsOpen = true;
        }
    }

    private async void OnboardingFinish_Click(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        await ViewModel.MarkOnboardingSeenAsync();
    }

    private async void OnboardingSkip_Click(TeachingTip sender, object args)
    {
        sender.IsOpen = false;
        await ViewModel.MarkOnboardingSeenAsync();
    }

    private async Task RenderCanvasAsync()
    {
        if (ViewModel.Survey is null || ViewModel.SelectedFloor is null)
        {
            return;
        }

        try
        {
            await PlanCanvas.RenderAsync(ViewModel.SelectedFloor, ViewModel.Heatmap, ViewModel.CurrentWalkPoint, ViewModel.SelectedWalls, ViewModel.RemainingWalkPoints);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Failed to render the floor plan canvas.", ex);
        }
    }

    private async Task OnSelectClickAsync(Point2D position)
    {
        switch (ViewModel.FindNearestSelectable(position))
        {
            case Wall wall:
                ViewModel.ToggleWallSelection(wall);
                break;

            case TestPoint testPoint:
                await ConfirmRecaptureAsync(testPoint);
                break;
        }
    }

    private async Task ConfirmRecaptureAsync(TestPoint testPoint)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localizationService.GetString("WorkspaceRecaptureDialogTitle"),
            Content = _localizationService.GetString("WorkspaceRecaptureDialogContent"),
            PrimaryButtonText = _localizationService.GetString("WorkspaceRecaptureDialogPrimaryButton"),
            CloseButtonText = _localizationService.GetString("WorkspaceRecaptureDialogCloseButton"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RecaptureTestPointAsync(testPoint);
        }
    }

    private async void ApplyWallMaterial_Click(object sender, RoutedEventArgs e)
    {
        if (WallMaterialSelector.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        WallMaterial? material = string.IsNullOrEmpty(tag) ? null : Enum.Parse<WallMaterial>(tag);
        double? thickness = double.IsNaN(WallThicknessInput.Value) ? null : WallThicknessInput.Value;
        await ViewModel.ApplyMaterialToSelectedWallsAsync(material, thickness);
    }

    private void ClearWallSelection_Click(object sender, RoutedEventArgs e) => ViewModel.ClearWallSelection();

    private async void SuggestPlacements_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.HasReplaceableSuggestions)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = _localizationService.GetString("WorkspaceReplaceSuggestionsDialogTitle"),
                Content = _localizationService.GetString("WorkspaceReplaceSuggestionsDialogContent"),
                PrimaryButtonText = _localizationService.GetString("WorkspaceReplaceSuggestionsDialogPrimaryButton"),
                CloseButtonText = _localizationService.GetString("WorkspaceReplaceSuggestionsDialogCloseButton"),
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        await ViewModel.SuggestPlacementsCommand.ExecuteAsync(null);
    }

    // Predicted-vs-actual comparison, anchored at the clicked point rather than a docked panel —
    // this is Workspace's own addition on top of the standalone Diagnose page, since only here is
    // there a survey/propagation model to predict against.
    private async Task OnDiagnoseClickAsync(Point2D position)
    {
        await ViewModel.DiagnoseAtPointAsync(position);
        ShowDiagnoseFlyout(position);
    }

    private void ShowDiagnoseFlyout(Point2D position)
    {
        var content = new StackPanel { Spacing = 8, MaxWidth = 320 };
        content.Children.Add(new TextBlock
        {
            Text = ViewModel.DiagnoseSummaryDisplay,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        foreach (var finding in ViewModel.DiagnoseFindings)
        {
            content.Children.Add(new TextBlock
            {
                Text = finding.Message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = SeverityBrush(finding.Severity),
            });
        }

        var flyout = new Flyout { Content = content };
        flyout.ShowAt(PlanCanvas, new FlyoutShowOptions { Position = PlanCanvas.ToScreenPoint(position) });
    }

    private static Brush SeverityBrush(DiagnosticSeverity severity)
    {
        string resourceKey = severity is DiagnosticSeverity.Error ? "DangerBrush" : severity is DiagnosticSeverity.Warning ? "WarningBrush" : "SuccessBrush";
        return (Brush)Application.Current.Resources[resourceKey];
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

    // Resuming a saved guided walk switches the view model's band to whichever one the walk was
    // suggested for. The picker is populated in code (not bound), so without this it kept showing
    // the old band while the heatmap underneath had already changed.
    private void SyncBandSelectorToViewModel()
    {
        for (int i = 0; i < BandSelector.Items.Count; i++)
        {
            if (BandSelector.Items[i] is ComboBoxItem { Tag: Band band } && band == ViewModel.SelectedBand)
            {
                BandSelector.SelectedIndex = i;
                return;
            }
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    private async void AddFloor_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = _localizationService.GetString("WorkspaceAddFloorDialogNameLabel") };
        var outdoorCheckBox = new CheckBox { Content = _localizationService.GetString("WorkspaceAddFloorDialogOutdoorLabel") };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localizationService.GetString("WorkspaceAddFloorDialogTitle"),
            Content = new StackPanel { Spacing = 12, Children = { nameBox, outdoorCheckBox } },
            PrimaryButtonText = _localizationService.GetString("WorkspaceAddFloorDialogPrimaryButton"),
            CloseButtonText = _localizationService.GetString("WorkspaceAddFloorDialogCloseButton"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(nameBox.Text))
        {
            await ViewModel.AddFloorCommand.ExecuteAsync((nameBox.Text.Trim(), outdoorCheckBox.IsChecked == true));
        }
    }

    private async void SaveSnapshot_Click(object sender, RoutedEventArgs e)
    {
        var labelBox = new TextBox { Header = _localizationService.GetString("WorkspaceSaveSnapshotDialogLabelHeader") };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localizationService.GetString("WorkspaceSaveSnapshotDialogTitle"),
            Content = labelBox,
            PrimaryButtonText = _localizationService.GetString("WorkspaceSaveSnapshotDialogPrimaryButton"),
            CloseButtonText = _localizationService.GetString("WorkspaceSaveSnapshotDialogCloseButton"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(labelBox.Text))
        {
            await ViewModel.SaveSnapshotCommand.ExecuteAsync(labelBox.Text.Trim());
        }
    }

    private void CompareSnapshots_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Survey is not null && ViewModel.FilePath is not null)
        {
            Frame.Navigate(typeof(SnapshotComparisonPage), (ViewModel.Survey, ViewModel.FilePath));
        }
    }

    private ToggleButton[] ToolButtons => [SelectToolButton, AddTestPointToolButton, DrawWallToolButton, DeleteToolButton, DiagnoseToolButton];

    // ToggleButtons untoggle themselves when clicked a second time, which left every tool button
    // visibly off while the canvas kept using whichever tool was last set — the toolbar lied about
    // what a click on the plan would do. These behave as a radio group instead: the active tool
    // stays lit until a different one is chosen.
    private void ToolToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && !ToolButtons.Any(other => !ReferenceEquals(other, button) && other.IsChecked == true))
        {
            button.IsChecked = true;
        }
    }

    private void ToolToggle_Checked(object sender, RoutedEventArgs e)
    {
        foreach (var button in ToolButtons)
        {
            if (!ReferenceEquals(button, sender))
            {
                button.IsChecked = false;
            }
        }

        PlanCanvas.Tool = ReferenceEquals(sender, AddTestPointToolButton) ? WorkspaceTool.AddTestPoint
            : ReferenceEquals(sender, DrawWallToolButton) ? WorkspaceTool.DrawWall
            : ReferenceEquals(sender, DeleteToolButton) ? WorkspaceTool.DeleteElement
            : ReferenceEquals(sender, DiagnoseToolButton) ? WorkspaceTool.Diagnose
            : WorkspaceTool.Select;

        if (PlanCanvas.Tool != WorkspaceTool.Select)
        {
            ViewModel.ClearWallSelection();
        }
    }
}
