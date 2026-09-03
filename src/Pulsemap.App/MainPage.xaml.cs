using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Pulsemap.App.Views;

namespace Pulsemap.App;

/// <summary>The app shell: a NavigationView rail around a content Frame. Sits directly on the window's Mica backdrop — no solid fill behind the pane, per docs/design-tokens.md.</summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
        ContentFrame.Navigated += ContentFrame_Navigated;
        ContentFrame.Navigate(typeof(HomePage));
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is NavigationViewItem { Tag: string tag })
        {
            NavigateToTag(tag);
        }
    }

    // SelectionChanged doesn't fire for the item that's already selected, and pages reached from
    // inside a section (the wizard and Workspace from Home, the snapshot comparison from
    // Workspace) leave the rail pointing at that section. Without this, clicking the highlighted
    // rail item from one of those pages did nothing at all — a dead end with no way back but the
    // page's own button.
    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem { Tag: string tag })
        {
            NavigateToTag(tag);
        }
    }

    private void NavigateToTag(string tag)
    {
        Type? pageType = tag switch
        {
            "Home" => typeof(HomePage),
            "Settings" => typeof(SettingsPage),
            "Surveys" => typeof(SurveysPage),
            "Diagnose" => typeof(DiagnosticsPage),
            _ => null,
        };

        if (pageType is not null && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    // Keeps the rail's highlight honest when navigation comes from inside a page (Home's New
    // Survey button, a survey card, Workspace's Back button) rather than from the rail itself.
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        string? tag = e.SourcePageType switch
        {
            var t when t == typeof(HomePage) => "Home",
            var t when t == typeof(SettingsPage) => "Settings",
            var t when t == typeof(SurveysPage) => "Surveys",
            var t when t == typeof(DiagnosticsPage) => "Diagnose",
            _ => null,
        };

        Nav.IsBackEnabled = ContentFrame.CanGoBack;

        if (tag is null)
        {
            return;
        }

        foreach (var item in Nav.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag as string == tag)
            {
                Nav.SelectedItem = item;
                return;
            }
        }
    }

    private void Nav_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
        {
            ContentFrame.GoBack();
        }
    }
}
