using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Core.Placement;
using Pulsemap.App.Core.Propagation;
using Pulsemap.App.Services;
using Pulsemap.App.ViewModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Pulsemap.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// The main application window. Use <c>App.Window</c> from any class that needs
    /// the window reference (for dialogs, pickers, interop, etc.).
    /// </summary>
    public static Window Window { get; private set; } = null!;

    /// <summary>
    /// The UI thread dispatcher. Use <c>App.DispatcherQueue</c> to marshal calls
    /// to the UI thread. Fully qualified to avoid CS0104 ambiguity with
    /// <see cref="Windows.System.DispatcherQueue"/>.
    /// </summary>
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;

    /// <summary>
    /// The native window handle (HWND). Use for file pickers,
    /// <c>DataTransferManager</c>, and any WinRT interop that requires
    /// <c>InitializeWithWindow</c>.
    /// </summary>
    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>Resolves Core services and ViewModels — registered once in <see cref="ConfigureServices"/>.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Initializes the singleton application object.
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        Window = new MainWindow();
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Window.Activate();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core — stateless, safe as singletons.
        services.AddSingleton<ISurveyFileService, ZipSurveyFileService>();
        services.AddSingleton<IPropagationModel, LogDistancePropagationModel>();
        services.AddSingleton<IKrigingInterpolator, OrdinaryKrigingInterpolator>();
        services.AddSingleton<IApPlacementOptimizer, GreedyCoverageApPlacementOptimizer>();
        services.AddSingleton<ISurveyDataExporter, SurveyDataExporter>();
        services.AddSingleton<IReportExporter, PdfReportExporter>();

        // App
        services.AddSingleton<ISurveyLibraryService, SurveyLibraryService>();

        // ViewModels — transient, recreated per navigation.
        services.AddTransient<HomeViewModel>();

        return services.BuildServiceProvider();
    }
}
