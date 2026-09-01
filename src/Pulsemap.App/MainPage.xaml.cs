using Microsoft.UI.Xaml.Controls;
using Pulsemap.App.Views;

namespace Pulsemap.App;

/// <summary>The app shell: a NavigationView rail around a content Frame. Sits directly on the window's Mica backdrop — no solid fill behind the pane, per docs/design-tokens.md.</summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        switch (tag)
        {
            case "Home":
                ContentFrame.Navigate(typeof(HomePage));
                break;
            case "Surveys":
            case "Settings":
                // Not built yet — Phase 1 build order picks these up in a later stage.
                break;
        }
    }
}
