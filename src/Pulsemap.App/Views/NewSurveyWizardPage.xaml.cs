using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pulsemap.App.Core.Models;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Views;

public sealed partial class NewSurveyWizardPage : Page
{
    public NewSurveyWizardViewModel ViewModel { get; }

    public NewSurveyWizardPage()
    {
        ViewModel = App.Services.GetRequiredService<NewSurveyWizardViewModel>();
        InitializeComponent();
        ViewModel.SurveyCreated += (_, filePath) => Frame.Navigate(typeof(WorkspacePage), filePath);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    private void SurveyTypeRadio_Checked(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedSurveyType = ReferenceEquals(sender, NewDeploymentRadio)
            ? SurveyType.NewDeployment
            : SurveyType.ExistingNetworkAudit;

    private void FloorPlanStyleRadio_Checked(object sender, RoutedEventArgs e) =>
        ViewModel.SelectedFloorPlanStyle = ReferenceEquals(sender, ImageRadio)
            ? FloorPlanStyleChoice.Image
            : FloorPlanStyleChoice.RoomList;

    private void RemoveRoom_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RoomListEntry room })
        {
            ViewModel.RemoveRoomCommand.Execute(room);
        }
    }

    private void WallMaterialComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [ComboBoxItem { Tag: string tag }, ..])
        {
            ViewModel.DefaultWallMaterial = Enum.Parse<WallMaterial>(tag);
        }
    }
}
