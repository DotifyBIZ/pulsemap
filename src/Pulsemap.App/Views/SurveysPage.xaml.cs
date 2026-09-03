using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulsemap.App.Services;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Views;

public sealed partial class SurveysPage : Page
{
    private readonly ILocalizationService _localizationService = App.Services.GetRequiredService<ILocalizationService>();

    public SurveysViewModel ViewModel { get; }

    public SurveysPage()
    {
        ViewModel = App.Services.GetRequiredService<SurveysViewModel>();
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadCommand.ExecuteAsync(null);
    }

    private void NewSurvey_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(NewSurveyWizardPage));

    private void SurveysGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SurveySummary summary)
        {
            Frame.Navigate(typeof(WorkspacePage), summary.FilePath);
        }
    }

    private async void RenameButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SurveySummary summary)
        {
            return;
        }

        var textBox = new TextBox { Text = summary.Name };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localizationService.GetString("SurveysRenameDialogTitle"),
            Content = textBox,
            PrimaryButtonText = _localizationService.GetString("SurveysRenameDialogPrimaryButton"),
            CloseButtonText = _localizationService.GetString("SurveysRenameDialogCloseButton"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            await ViewModel.RenameCommand.ExecuteAsync((summary, textBox.Text.Trim()));
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not SurveySummary summary)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _localizationService.GetString("SurveysDeleteDialogTitle"),
            Content = string.Format(System.Globalization.CultureInfo.CurrentCulture, _localizationService.GetString("SurveysDeleteDialogContentFormat"), summary.Name),
            PrimaryButtonText = _localizationService.GetString("SurveysDeleteDialogPrimaryButton"),
            CloseButtonText = _localizationService.GetString("SurveysDeleteDialogCloseButton"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(summary);
        }
    }
}
