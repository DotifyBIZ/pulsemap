using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Diagnostics;
using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Measurement;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Core.Placement;
using Pulsemap.App.Core.Propagation;
using Pulsemap.App.Core.Settings;
using Pulsemap.App.Services;
using Windows.System;

namespace Pulsemap.App.ViewModels;

/// <summary>Drives the Workspace page: loads a survey, renders/edits its floor, and computes the
/// per-band coverage heatmap and reliable-coverage percentage that the canvas and side panel
/// display.</summary>
public partial class WorkspaceViewModel : ObservableObject
{
    // Matches GreedyCoverageApPlacementOptimizer's own reliable-coverage threshold — the
    // conventional -67dBm WiFi planning figure, kept here too since the coverage-percent readout
    // needs the same definition of "reliable" that placement suggestions use.
    private const double ReliableCoverageThresholdDbm = -67;
    private const double HeatmapGridSpacingMeters = 0.5;
    private const double DeleteHitToleranceMeters = 0.5;

    // A new outdoor area's starting extent — resizable/movable afterward via the canvas's drag
    // handle (see FloorPlanCanvas/UpdateOutdoorBoundsAsync), so this is just a reasonable default.
    private const double DefaultOutdoorBoundsSizeMeters = 40;

    private readonly ISurveyFileService _surveyFileService;
    private readonly IPropagationModel _propagationModel;
    private readonly IApPlacementOptimizer _placementOptimizer;
    private readonly IWlanAdapterService _wlanAdapterService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppLogger _logger;
    private readonly ISurveyDataExporter _surveyDataExporter;
    private readonly IReportExporter _reportExporter;
    private readonly ISurveyExportFilePickerService _exportFilePickerService;
    private readonly IKrigingInterpolator _krigingInterpolator;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILinkDiagnosticsService _linkDiagnosticsService;
    private readonly INetworkHealthService _networkHealthService;

    private string? _filePath;

    public string? FilePath => _filePath;

    public WorkspaceViewModel(
        ISurveyFileService surveyFileService,
        IPropagationModel propagationModel,
        IApPlacementOptimizer placementOptimizer,
        IWlanAdapterService wlanAdapterService,
        ILocalizationService localizationService,
        IAppLogger logger,
        ISurveyDataExporter surveyDataExporter,
        IReportExporter reportExporter,
        ISurveyExportFilePickerService exportFilePickerService,
        IKrigingInterpolator krigingInterpolator,
        IAppSettingsService appSettingsService,
        ILinkDiagnosticsService linkDiagnosticsService,
        INetworkHealthService networkHealthService)
    {
        _surveyFileService = surveyFileService;
        _propagationModel = propagationModel;
        _placementOptimizer = placementOptimizer;
        _wlanAdapterService = wlanAdapterService;
        _localizationService = localizationService;
        _logger = logger;
        _surveyDataExporter = surveyDataExporter;
        _reportExporter = reportExporter;
        _exportFilePickerService = exportFilePickerService;
        _krigingInterpolator = krigingInterpolator;
        _appSettingsService = appSettingsService;
        _linkDiagnosticsService = linkDiagnosticsService;
        _networkHealthService = networkHealthService;
    }

    public event EventHandler? FloorChanged;

    [ObservableProperty]
    public partial Survey? Survey { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessPointSummaryDisplay))]
    [NotifyCanExecuteChangedFor(nameof(StartGuidedWalkCommand))]
    public partial Floor? SelectedFloor { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoveragePercentDisplay))]
    public partial double CoveragePercent { get; set; }

    [ObservableProperty]
    public partial Band SelectedBand { get; set; }

    public IReadOnlyList<Band> AvailableBands { get; private set; } = [];

    // Survey.Floors is a plain List<Floor>, not an ObservableCollection — it wouldn't be a Core-
    // layer concern to make it one, so this computed projection is re-signaled manually (see
    // OnPropertyChanged(nameof(Floors)) below) whenever a floor is added.
    public IReadOnlyList<Floor> Floors => Survey?.Floors ?? [];

    public IReadOnlyList<CoverageSample> Heatmap { get; private set; } = [];

    public string SurveyNameDisplay => Survey?.Name ?? string.Empty;

    /// <summary>Same survey-type summary the wizard shows on its final step — repeated here since
    /// nothing in Workspace otherwise reminds a user which mode ("new deployment" vs. "existing
    /// network audit") their survey is in once they've left the wizard.</summary>
    public string SurveyTypeDisplay => Survey switch
    {
        null => string.Empty,
        { Type: SurveyType.NewDeployment } => _localizationService.GetString("WizardSurveyTypeSummaryNewDeployment"),
        { TargetNetworkSsid: null or "" } => _localizationService.GetString("WizardSurveyTypeSummaryExistingAuditNoSsid"),
        _ => string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WizardSurveyTypeSummaryExistingAuditWithSsidFormat"), Survey.TargetNetworkSsid),
    };

    public string CoveragePercentDisplay =>
        string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WorkspaceCoveragePercentFormat"), CoveragePercent);

    public string AccessPointSummaryDisplay
    {
        get
        {
            if (SelectedFloor is null || SelectedFloor.AccessPoints.Count == 0)
            {
                return _localizationService.GetString("WorkspaceNoAccessPointsPlaced");
            }

            string channelAbbreviation = _localizationService.GetString("WorkspaceChannelAbbreviation");
            string summaryFormat = _localizationService.GetString("WorkspaceAccessPointSummaryFormat");
            return string.Join(
                Environment.NewLine,
                SelectedFloor.AccessPoints.Select(ap =>
                {
                    string radios = string.Join(", ", ap.Radios.Select(r => $"{BandDisplayName(r.Key)} {channelAbbreviation}{r.Value.Channel}"));
                    return string.Format(CultureInfo.CurrentCulture, summaryFormat, ap.Label, radios);
                }));
        }
    }

    public string BandDisplayName(Band band) => band switch
    {
        Band.TwoPointFourGhz => _localizationService.GetString("WizardBand24Checkbox.Content"),
        Band.FiveGhz => _localizationService.GetString("WizardBand5Checkbox.Content"),
        Band.SixGhz => _localizationService.GetString("WizardBand6Checkbox.Content"),
        _ => band.ToString(),
    };

    // Adapter tab
    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    public ObservableCollection<WlanNetworkDisplay> ScanResults { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartGuidedWalkCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmWalkPointCommand))]
    [NotifyPropertyChangedFor(nameof(NeedsAdapterForGuidedWalk))]
    public partial NetworkAdapterInfo? SelectedAdapter { get; set; }

    /// <summary>Guided-walk test point suggestions live in the Suggestions tab, but still need an
    /// adapter picked over on the Adapter tab — this note points a user there instead of leaving
    /// the Start Guided Walk button silently disabled with no explanation.</summary>
    /// <remarks>Suppressed when the machine has no WLAN adapter at all — pointing a user at a
    /// picker with nothing in it is worse than the separate "no adapter found" note that case
    /// gets instead.</remarks>
    public bool NeedsAdapterForGuidedWalk => SelectedAdapter is null && !HasNoAdapters;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLocationSettingsLink))]
    public partial WlanScanStatus? LastScanStatus { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScanStatusMessage))]
    public partial string? ScanStatusMessage { get; set; }

    public bool HasScanStatusMessage => ScanStatusMessage is not null;

    public bool ShowLocationSettingsLink => LastScanStatus == WlanScanStatus.LocationAccessDenied;

    // Guided measurement walk
    private Queue<Point2D> _guidedWalkQueue = new();
    private int _guidedWalkTotalPoints;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuidedWalkIdle))]
    [NotifyPropertyChangedFor(nameof(GuidedWalkProgressDisplay))]
    [NotifyCanExecuteChangedFor(nameof(StartGuidedWalkCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmWalkPointCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelGuidedWalkCommand))]
    [NotifyCanExecuteChangedFor(nameof(SkipWalkPointCommand))]
    public partial bool IsGuidedWalkActive { get; set; }

    public bool IsGuidedWalkIdle => !IsGuidedWalkActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuidedWalkProgressDisplay))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmWalkPointCommand))]
    public partial Point2D? CurrentWalkPoint { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmWalkPointCommand))]
    [NotifyCanExecuteChangedFor(nameof(SkipWalkPointCommand))]
    public partial bool IsCapturingWalkPoint { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGuidedWalkStatusMessage))]
    public partial string? GuidedWalkStatusMessage { get; set; }

    public bool HasGuidedWalkStatusMessage => GuidedWalkStatusMessage is not null;

    public string GuidedWalkProgressDisplay => IsGuidedWalkActive && CurrentWalkPoint is { } point
        ? string.Format(
            CultureInfo.CurrentCulture,
            _localizationService.GetString("WorkspaceGuidedWalkProgressFormat"),
            _guidedWalkTotalPoints - _guidedWalkQueue.Count + 1,
            _guidedWalkTotalPoints,
            point.X,
            point.Y)
        : _localizationService.GetString("WorkspaceGuidedWalkNotWalking");

    // Points still queued (after the current one) in an active guided walk, for the canvas to
    // show as upcoming-point markers.
    public IReadOnlyList<Point2D> RemainingWalkPoints => IsGuidedWalkActive ? [.. _guidedWalkQueue.Skip(1)] : [];

    // Wall material editing: the Select tool toggles walls in and out of this set by clicking
    // them, then a batch action applies one material/thickness to everything selected.
    private readonly List<Wall> _selectedWalls = [];

    public IReadOnlyList<Wall> SelectedWalls => _selectedWalls;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWallSelection))]
    [NotifyPropertyChangedFor(nameof(WallSelectionCountDisplay))]
    public partial int SelectedWallCount { get; set; }

    public bool HasWallSelection => SelectedWallCount > 0;

    public string WallSelectionCountDisplay =>
        string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WorkspaceWallSelectionCountFormat"), SelectedWallCount);

    public async Task<bool> ShouldShowOnboardingAsync()
    {
        var settings = await _appSettingsService.LoadAsync();
        return !settings.HasSeenWorkspaceOnboarding;
    }

    public async Task MarkOnboardingSeenAsync()
    {
        var settings = await _appSettingsService.LoadAsync();
        settings.HasSeenWorkspaceOnboarding = true;
        await _appSettingsService.SaveAsync(settings);
    }

    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _filePath = filePath;
        ErrorMessage = null;
        IsLoading = true;
        try
        {
            Survey = await _surveyFileService.LoadAsync(filePath, cancellationToken);
            AvailableBands = Survey.TargetBands;
            SelectedBand = AvailableBands.Count > 0 ? AvailableBands[0] : Band.TwoPointFourGhz;
            SelectedFloor = Survey.Floors.Count > 0 ? Survey.Floors[0] : null;
            OnPropertyChanged(nameof(SurveyNameDisplay));
            OnPropertyChanged(nameof(SurveyTypeDisplay));
            OnPropertyChanged(nameof(AvailableBands));
            OnPropertyChanged(nameof(Floors));
            OnPropertyChanged(nameof(AccessPointSummaryDisplay));
            OnPropertyChanged(nameof(HasSnapshots));
            Recompute();
            await LoadAdaptersAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WorkspaceLoadErrorFormat"), ex.Message);
            await _logger.LogErrorAsync($"Failed to load survey '{filePath}'.", ex, CancellationToken.None);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadAdaptersAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<NetworkAdapterInfo> adapters;
        try
        {
            adapters = await _wlanAdapterService.GetAdaptersAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Enumerating adapters goes through wlanapi.dll; a machine with the WLAN service
            // stopped shouldn't fail the whole survey load.
            await _logger.LogErrorAsync("Failed to enumerate WLAN adapters.", ex, CancellationToken.None);
            adapters = [];
        }

        Adapters.Clear();
        foreach (var adapter in adapters)
        {
            Adapters.Add(adapter);
        }

        SelectedAdapter = Adapters.Count > 0 ? Adapters[0] : null;
        OnPropertyChanged(nameof(HasNoAdapters));
    }

    /// <summary>No WLAN adapter at all (no hardware, WLAN service stopped) — Scan and the guided
    /// walk are both unusable, and a disabled button with no explanation reads as a broken app.</summary>
    public bool HasNoAdapters => Adapters.Count == 0;

    private bool CanScan() => SelectedAdapter is not null && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (SelectedAdapter is null)
        {
            return;
        }

        IsScanning = true;
        ScanStatusMessage = null;
        try
        {
            var result = await SafeScanAsync(SelectedAdapter.Id);
            LastScanStatus = result.Status;
            ScanResults.Clear();

            switch (result.Status)
            {
                case WlanScanStatus.Success:
                    string channelAbbreviation = _localizationService.GetString("WorkspaceChannelAbbreviation");
                    string subtitleFormat = _localizationService.GetString("WorkspaceNetworkSubtitleFormat");
                    string unknownBand = _localizationService.GetString("WorkspaceUnknownBand");
                    string hiddenNetwork = _localizationService.GetString("WorkspaceHiddenNetwork");
                    foreach (var network in result.Networks.OrderByDescending(n => n.SignalDbm))
                    {
                        string bandPart = network.Band is { } band ? BandDisplayName(band) : unknownBand;
                        string ssid = string.IsNullOrEmpty(network.Ssid) ? hiddenNetwork : network.Ssid;
                        ScanResults.Add(new WlanNetworkDisplay(
                            ssid,
                            string.Format(CultureInfo.CurrentCulture, subtitleFormat, network.Bssid, channelAbbreviation, network.Channel, bandPart, network.SignalDbm)));
                    }

                    ScanStatusMessage = result.Networks.Count == 0 ? _localizationService.GetString("WorkspaceNoNetworksFound") : null;
                    break;

                default:
                    ScanStatusMessage = DescribeScanStatus(result.Status);
                    break;
            }
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>Every scan in this view model crosses into wlanapi.dll, and all three callers reach
    /// it from an <c>async void</c> handler or a command — a driver-level failure has to come back
    /// as a reportable <see cref="WlanScanStatus.Failed"/>, never as an unhandled exception.</summary>
    private async Task<WlanScanResult> SafeScanAsync(Guid adapterId)
    {
        try
        {
            return await _wlanAdapterService.ScanAsync(adapterId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.LogErrorAsync("WLAN scan failed.", ex, CancellationToken.None);
            return new WlanScanResult(WlanScanStatus.Failed, []);
        }
    }

    private string DescribeScanStatus(WlanScanStatus status) => status switch
    {
        WlanScanStatus.LocationAccessDenied => _localizationService.GetString("WorkspaceScanStatusLocationDenied"),
        WlanScanStatus.NoAdapter => _localizationService.GetString("WorkspaceScanStatusNoAdapter"),
        _ => _localizationService.GetString("WorkspaceScanStatusFailed"),
    };

    [RelayCommand]
    private static async Task OpenLocationSettingsAsync() =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));

    private bool CanStartGuidedWalk() => !IsGuidedWalkActive && SelectedAdapter is not null && Survey is not null && SelectedFloor is not null;

    [RelayCommand(CanExecute = nameof(CanStartGuidedWalk))]
    private async Task StartGuidedWalkAsync()
    {
        if (Survey is null || SelectedFloor is null)
        {
            return;
        }

        var points = MeasurementPointSuggester.SuggestPoints(SelectedFloor, SelectedBand, _krigingInterpolator);
        if (points.Count == 0)
        {
            GuidedWalkStatusMessage = _localizationService.GetString("WorkspaceNoUnmeasuredPoints");
            return;
        }

        _guidedWalkQueue = new Queue<Point2D>(points);
        _guidedWalkTotalPoints = points.Count;
        GuidedWalkStatusMessage = null;
        IsGuidedWalkActive = true;
        SelectedFloor.PendingGuidedWalkPoints.Clear();
        SelectedFloor.PendingGuidedWalkPoints.AddRange(points);
        SelectedFloor.PendingGuidedWalkBand = SelectedBand;
        await SaveAndRefreshAsync();
        AdvanceGuidedWalk();
    }

    private bool CanConfirmWalkPoint() => IsGuidedWalkActive && !IsCapturingWalkPoint && SelectedAdapter is not null && CurrentWalkPoint is not null;

    [RelayCommand(CanExecute = nameof(CanConfirmWalkPoint))]
    private async Task ConfirmWalkPointAsync()
    {
        if (Survey is null || SelectedFloor is null || SelectedAdapter is null || CurrentWalkPoint is not { } position)
        {
            return;
        }

        IsCapturingWalkPoint = true;
        GuidedWalkStatusMessage = null;
        try
        {
            var scanResult = await SafeScanAsync(SelectedAdapter.Id);
            if (scanResult.Status != WlanScanStatus.Success)
            {
                GuidedWalkStatusMessage = DescribeScanStatus(scanResult.Status);
                return;
            }

            var testPoint = TestPointCapture.BuildTestPoint(position, scanResult, Survey, SelectedAdapter.Name, DateTimeOffset.Now);
            SelectedFloor.TestPoints.Add(testPoint);
            _guidedWalkQueue.Dequeue();
            SelectedFloor.PendingGuidedWalkPoints.Clear();
            SelectedFloor.PendingGuidedWalkPoints.AddRange(_guidedWalkQueue);
            await SaveAndRefreshAsync();

            AdvanceGuidedWalk();
        }
        finally
        {
            IsCapturingWalkPoint = false;
        }
    }

    private bool CanCancelGuidedWalk() => IsGuidedWalkActive;

    [RelayCommand(CanExecute = nameof(CanCancelGuidedWalk))]
    private async Task CancelGuidedWalkAsync()
    {
        _guidedWalkQueue.Clear();
        IsGuidedWalkActive = false;
        CurrentWalkPoint = null;
        GuidedWalkStatusMessage = _localizationService.GetString("WorkspaceGuidedWalkCanceled");
        if (SelectedFloor is not null)
        {
            SelectedFloor.PendingGuidedWalkPoints.Clear();
            await SaveAndRefreshAsync();
        }

        OnPropertyChanged(nameof(RemainingWalkPoints));
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool CanSkipWalkPoint() => IsGuidedWalkActive && !IsCapturingWalkPoint;

    [RelayCommand(CanExecute = nameof(CanSkipWalkPoint))]
    private async Task SkipWalkPointAsync()
    {
        if (SelectedFloor is null || _guidedWalkQueue.Count == 0)
        {
            return;
        }

        _guidedWalkQueue.Dequeue();
        SelectedFloor.PendingGuidedWalkPoints.Clear();
        SelectedFloor.PendingGuidedWalkPoints.AddRange(_guidedWalkQueue);
        await SaveAndRefreshAsync();
        AdvanceGuidedWalk();
    }

    private void AdvanceGuidedWalk()
    {
        if (_guidedWalkQueue.Count == 0)
        {
            IsGuidedWalkActive = false;
            CurrentWalkPoint = null;
            GuidedWalkStatusMessage = _localizationService.GetString("WorkspaceGuidedWalkComplete");
        }
        else
        {
            CurrentWalkPoint = _guidedWalkQueue.Peek();
        }

        OnPropertyChanged(nameof(RemainingWalkPoints));
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task AddTestPointAsync(Point2D position)
    {
        if (SelectedFloor is null)
        {
            return;
        }

        SelectedFloor.TestPoints.Add(new TestPoint { Position = position });
        await SaveAndRefreshAsync();
    }

    public async Task AddWallAsync(Point2D start, Point2D end)
    {
        if (SelectedFloor is null)
        {
            return;
        }

        SelectedFloor.Walls.Add(new Wall { Start = start, End = end });
        await SaveAndRefreshAsync();
    }

    public async Task UpdateOutdoorBoundsAsync(Point2D min, Point2D max)
    {
        if (SelectedFloor is not { IsOutdoor: true } floor)
        {
            return;
        }

        floor.OutdoorBoundsMin = min;
        floor.OutdoorBoundsMax = max;
        await SaveAndRefreshAsync();
    }

    // Single-level undo, scoped to the Delete tool only — the highest-risk edit in the app since
    // it's the one permanent, unconfirmed action on the canvas. Not a general undo/redo stack:
    // overwritten by the next delete, and cleared on floor switch so it never applies somewhere
    // other than where the deletion actually happened.
    private (Floor Floor, object Item)? _lastDeletedElement;

    public bool CanUndoDelete => _lastDeletedElement is not null;

    public string LastDeletedItemDisplay => _lastDeletedElement?.Item switch
    {
        Wall => _localizationService.GetString("WorkspaceUndoDeleteWallMessage"),
        TestPoint => _localizationService.GetString("WorkspaceUndoDeleteTestPointMessage"),
        AccessPoint => _localizationService.GetString("WorkspaceUndoDeleteAccessPointMessage"),
        _ => string.Empty,
    };

    public async Task DeleteNearestElementAsync(Point2D at)
    {
        if (SelectedFloor is null || !TryFindNearestElement(SelectedFloor, at, out var remove, out var removedItem))
        {
            return;
        }

        remove();
        _selectedWalls.RemoveAll(wall => !SelectedFloor.Walls.Contains(wall));
        SelectedWallCount = _selectedWalls.Count;
        _lastDeletedElement = removedItem is not null ? (SelectedFloor, removedItem) : null;
        OnPropertyChanged(nameof(CanUndoDelete));
        OnPropertyChanged(nameof(LastDeletedItemDisplay));
        UndoDeleteCommand.NotifyCanExecuteChanged();
        await SaveAndRefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUndoDelete))]
    private async Task UndoDeleteAsync()
    {
        if (_lastDeletedElement is not { } deleted)
        {
            return;
        }

        switch (deleted.Item)
        {
            case Wall wall:
                deleted.Floor.Walls.Add(wall);
                break;
            case TestPoint testPoint:
                deleted.Floor.TestPoints.Add(testPoint);
                break;
            case AccessPoint accessPoint:
                deleted.Floor.AccessPoints.Add(accessPoint);
                break;
        }

        _lastDeletedElement = null;
        OnPropertyChanged(nameof(CanUndoDelete));
        OnPropertyChanged(nameof(LastDeletedItemDisplay));
        UndoDeleteCommand.NotifyCanExecuteChanged();
        await SaveAndRefreshAsync();
    }

    /// <summary>Nearest wall or test point to a Select-tool click, within the same hit tolerance
    /// the Delete tool uses — a wall is offered for material editing, a test point for recapture.
    /// Access points aren't selectable here; neither has a per-element edit affordance today.</summary>
    public object? FindNearestSelectable(Point2D at)
    {
        if (SelectedFloor is not { } floor)
        {
            return null;
        }

        var (testPoint, testPointDistance) = NearestTestPoint(floor, at);
        var (wall, wallDistance) = NearestWall(floor, at);
        double best = Math.Min(testPointDistance, wallDistance);
        if (best > DeleteHitToleranceMeters)
        {
            return null;
        }

        return best == testPointDistance ? testPoint : wall;
    }

    public void ToggleWallSelection(Wall wall)
    {
        if (!_selectedWalls.Remove(wall))
        {
            _selectedWalls.Add(wall);
        }

        SelectedWallCount = _selectedWalls.Count;
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearWallSelection()
    {
        if (_selectedWalls.Count == 0)
        {
            return;
        }

        _selectedWalls.Clear();
        SelectedWallCount = 0;
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ApplyMaterialToSelectedWallsAsync(WallMaterial? material, double? thicknessMeters)
    {
        if (_selectedWalls.Count == 0)
        {
            return;
        }

        foreach (var wall in _selectedWalls)
        {
            wall.Material = material;
            wall.ThicknessMeters = thicknessMeters;
        }

        ClearWallSelection();
        await SaveAndRefreshAsync();
    }

    public async Task RecaptureTestPointAsync(TestPoint testPoint)
    {
        if (Survey is null || SelectedFloor is null)
        {
            return;
        }

        // The user has already confirmed a recapture dialog by the time we get here — going quiet
        // because no adapter happens to be picked would look like the app simply ignored them.
        if (SelectedAdapter is null)
        {
            ErrorMessage = _localizationService.GetString("WorkspaceNoAdapterSelectedError");
            return;
        }

        var scanResult = await SafeScanAsync(SelectedAdapter.Id);
        if (scanResult.Status != WlanScanStatus.Success)
        {
            ErrorMessage = DescribeScanStatus(scanResult.Status);
            return;
        }

        var rebuilt = TestPointCapture.BuildTestPoint(testPoint.Position, scanResult, Survey, SelectedAdapter.Name, DateTimeOffset.Now);
        SelectedFloor.TestPoints.Remove(testPoint);
        SelectedFloor.TestPoints.Add(rebuilt);
        await SaveAndRefreshAsync();
    }

    public ObservableCollection<DiagnosticFindingDisplay> DiagnoseFindings { get; } = [];

    [ObservableProperty]
    public partial string? DiagnoseSummaryDisplay { get; set; }

    /// <summary>Compares this machine's live link against what the survey's own propagation model
    /// predicted at the clicked point — the Workspace-only half of diagnostics that the standalone
    /// Diagnose page can't offer, since it has no survey/floor to predict against. Reuses the exact
    /// same cross-floor/outdoor skip rules as the coverage heatmap via
    /// <see cref="CoverageGridCalculator.StrongestSignalDbm"/>, so the two never disagree.</summary>
    public async Task DiagnoseAtPointAsync(Point2D position)
    {
        if (Survey is null || SelectedFloor is null)
        {
            return;
        }

        // The page shows a flyout at the clicked point whatever happens here, so every early exit
        // has to leave something readable behind — otherwise the flyout renders last click's
        // findings, or nothing at all.
        if (SelectedAdapter is null)
        {
            DiagnoseFindings.Clear();
            DiagnoseSummaryDisplay = _localizationService.GetString("WorkspaceNoAdapterSelectedError");
            return;
        }

        double? predicted = CoverageGridCalculator.StrongestSignalDbm(position, SelectedFloor, Survey.Floors, SelectedBand, _propagationModel);

        LinkDiagnosticsSnapshot link;
        NetworkHealthSnapshot health;
        try
        {
            link = await _linkDiagnosticsService.GetCurrentLinkAsync(SelectedAdapter.Id);
            health = link.IsConnected ? await _networkHealthService.CheckHealthAsync(SelectedAdapter.Id) : NetworkHealthSnapshot.Unavailable;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _logger.LogErrorAsync("Failed to read live link diagnostics.", ex, CancellationToken.None);
            DiagnoseFindings.Clear();
            DiagnoseSummaryDisplay = _localizationService.GetString("WorkspaceDiagnoseFailedDisplay");
            return;
        }

        DiagnoseSummaryDisplay = predicted is { } predictedDbm
            ? string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WorkspaceDiagnosePredictedFormat"), predictedDbm)
            : _localizationService.GetString("WorkspaceDiagnoseNoPredictionDisplay");

        var findings = LinkDiagnosticsAnalyzer.Analyze(link, health, predicted);
        DiagnoseFindings.Clear();
        foreach (var finding in findings)
        {
            string template = _localizationService.GetString(finding.MessageKey);
            string message = finding.FormatArgs is null ? template : string.Format(CultureInfo.CurrentCulture, template, [.. finding.FormatArgs]);
            DiagnoseFindings.Add(new DiagnosticFindingDisplay(finding.Severity, message));
        }
    }

    /// <summary>Whether running Suggest Placements again would discard previously suggested (not
    /// user-placed) access points on this floor — lets the page ask for confirmation only when
    /// there's actually something to lose, rather than always or never.</summary>
    public bool HasReplaceableSuggestions => SelectedFloor?.AccessPoints.Any(ap => !ap.IsUserOverride) == true;

    [RelayCommand]
    private async Task SuggestPlacementsAsync()
    {
        if (Survey is null || SelectedFloor is null)
        {
            return;
        }

        var floor = SelectedFloor;
        var allFloors = Survey.Floors;
        var bands = Survey.TargetBands;

        // Greedy maximum-coverage is O(candidate points²) per access point placed — seconds of
        // pure CPU on a large floor. Running it inline froze the whole window; the progress ring
        // bound to IsLoading can only actually spin if the work is off the UI thread.
        IsLoading = true;
        try
        {
            var suggestions = await Task.Run(() => _placementOptimizer.SuggestPlacements(floor, allFloors, bands, _propagationModel));
            floor.AccessPoints.RemoveAll(ap => !ap.IsUserOverride);
            floor.AccessPoints.AddRange(suggestions);
        }
        finally
        {
            IsLoading = false;
        }

        await SaveAndRefreshAsync();
    }

    private bool CanAddFloor() => Survey is not null;

    [RelayCommand(CanExecute = nameof(CanAddFloor))]
    private async Task AddFloorAsync((string Name, bool IsOutdoor) args)
    {
        if (Survey is null)
        {
            return;
        }

        var floor = new Floor
        {
            Name = args.Name,
            IsOutdoor = args.IsOutdoor,
            Level = Survey.Floors.Count(f => !f.IsOutdoor),
            OutdoorBoundsMin = args.IsOutdoor ? new Point2D(0, 0) : null,
            OutdoorBoundsMax = args.IsOutdoor ? new Point2D(DefaultOutdoorBoundsSizeMeters, DefaultOutdoorBoundsSizeMeters) : null,
            PlanSource = new RoomListSource(),
        };

        Survey.Floors.Add(floor);
        OnPropertyChanged(nameof(Floors));
        SelectedFloor = floor;
        await SaveAndRefreshAsync();
    }

    private bool CanSaveSnapshot() => Survey is not null;

    [RelayCommand(CanExecute = nameof(CanSaveSnapshot))]
    private async Task SaveSnapshotAsync(string label)
    {
        if (Survey is null)
        {
            return;
        }

        Survey.Snapshots.Add(new SurveySnapshot { Label = label, Floors = CloneFloorsForSnapshot(Survey.Floors) });
        OnPropertyChanged(nameof(HasSnapshots));
        await SaveAndRefreshAsync();
    }

    /// <summary>Whether Compare has anything to compare against. With no saved snapshot the
    /// comparison page can only show "Current" against "Current" — a dead end reached through an
    /// enabled-looking button.</summary>
    public bool HasSnapshots => Survey is { Snapshots.Count: > 0 };

    // A snapshot freezes geometry/measurements only — sharing Wall/TestPoint/AccessPoint object
    // references with the live floor would mean editing the live floor also (invisibly) edits the
    // "frozen" snapshot, since those are mutable classes, not records. PlanSource is intentionally
    // *not* deep-copied: a snapshot always renders over the current floor's live background image,
    // not a duplicated one, per this feature's design (see the plan).
    private static List<Floor> CloneFloorsForSnapshot(IEnumerable<Floor> floors) =>
        [.. floors.Select(floor => new Floor
        {
            Id = floor.Id,
            Name = floor.Name,
            IsOutdoor = floor.IsOutdoor,
            Level = floor.Level,
            OutdoorBoundsMin = floor.OutdoorBoundsMin,
            OutdoorBoundsMax = floor.OutdoorBoundsMax,
            PlanSource = floor.PlanSource,
            Walls = [.. floor.Walls.Select(w => new Wall { Start = w.Start, End = w.End, Material = w.Material, ThicknessMeters = w.ThicknessMeters })],
            TestPoints = [.. floor.TestPoints.Select(tp => new TestPoint
            {
                Id = tp.Id,
                Position = tp.Position,
                Measurements = new Dictionary<Band, BandMeasurement>(tp.Measurements),
                InterferenceReadings = [.. tp.InterferenceReadings],
            })],
            AccessPoints = [.. floor.AccessPoints.Select(ap => new AccessPoint
            {
                Id = ap.Id,
                Position = ap.Position,
                Label = ap.Label,
                IsUserOverride = ap.IsUserOverride,
                Radios = new Dictionary<Band, BandRadioSettings>(ap.Radios),
            })],
        })];

    [RelayCommand]
    private Task ExportTestPointsCsvAsync() =>
        ExportAsync("-testpoints", ".csv", _localizationService.GetString("WorkspaceExportCsvFileType"), _surveyDataExporter.ExportTestPointsCsvAsync);

    [RelayCommand]
    private Task ExportAccessPointsCsvAsync() =>
        ExportAsync("-accesspoints", ".csv", _localizationService.GetString("WorkspaceExportCsvFileType"), _surveyDataExporter.ExportAccessPointsCsvAsync);

    [RelayCommand]
    private Task ExportSurveyJsonAsync() =>
        ExportAsync("-survey", ".json", _localizationService.GetString("WorkspaceExportJsonFileType"), _surveyDataExporter.ExportJsonAsync);

    [RelayCommand]
    private Task ExportCoverageReportPdfAsync() =>
        ExportAsync("-coverage-report", ".pdf", _localizationService.GetString("WorkspaceExportPdfFileType"), _reportExporter.ExportPdfAsync);

    private async Task ExportAsync(string fileNameSuffix, string extension, string fileTypeDescription, Func<Survey, Stream, CancellationToken, Task> exportAsync)
    {
        if (Survey is null)
        {
            return;
        }

        ErrorMessage = null;
        string suggestedFileName = SanitizeFileName(Survey.Name) + fileNameSuffix;
        Stream? stream = await _exportFilePickerService.PickSaveStreamAsync(suggestedFileName, extension, fileTypeDescription);
        if (stream is null)
        {
            return;
        }

        await using (stream)
        {
            try
            {
                await exportAsync(Survey, stream, CancellationToken.None);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorMessage = string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WorkspaceExportErrorFormat"), ex.Message);
                await _logger.LogErrorAsync("Failed to export survey data.", ex);
            }
        }
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    partial void OnSelectedBandChanged(Band value)
    {
        Recompute();
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedFloorChanged(Floor? value)
    {
        ClearWallSelection();

        _lastDeletedElement = null;
        OnPropertyChanged(nameof(CanUndoDelete));
        UndoDeleteCommand.NotifyCanExecuteChanged();

        _guidedWalkQueue.Clear();
        IsGuidedWalkActive = false;
        CurrentWalkPoint = null;
        if (value is { PendingGuidedWalkPoints.Count: > 0 } floor)
        {
            _guidedWalkQueue = new Queue<Point2D>(floor.PendingGuidedWalkPoints);
            _guidedWalkTotalPoints = _guidedWalkQueue.Count;
            SelectedBand = floor.PendingGuidedWalkBand ?? SelectedBand;
            IsGuidedWalkActive = true;
            CurrentWalkPoint = _guidedWalkQueue.Peek();
        }

        OnPropertyChanged(nameof(RemainingWalkPoints));
        Recompute();
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Auto-save is wired to nearly every canvas edit, and those edits arrive through
    /// <c>async void</c> event handlers on the page — an unhandled save failure there kills the
    /// process rather than surfacing anywhere. A failed save is reported in the page's error bar
    /// and logged; the in-memory edit stays, so retrying (or fixing the disk problem) still
    /// works.</summary>
    private async Task SaveAndRefreshAsync()
    {
        if (Survey is null || _filePath is null)
        {
            return;
        }

        try
        {
            await _surveyFileService.SaveAsync(Survey, _filePath);
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WorkspaceSaveErrorFormat"), ex.Message);
            await _logger.LogErrorAsync($"Failed to save survey '{_filePath}'.", ex, CancellationToken.None);
        }

        Recompute();
        OnPropertyChanged(nameof(AccessPointSummaryDisplay));
        FloorChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Recompute()
    {
        if (Survey is null || SelectedFloor is null)
        {
            Heatmap = [];
            CoveragePercent = 0;
            return;
        }

        Heatmap = CoverageGridCalculator.ComputeGrid(SelectedFloor, Survey.Floors, SelectedBand, HeatmapGridSpacingMeters, _propagationModel);
        CoveragePercent = Heatmap.Count == 0
            ? 0
            : Heatmap.Count(s => s.ValueDbm >= ReliableCoverageThresholdDbm) / (double)Heatmap.Count * 100;
    }

    private static bool TryFindNearestElement(Floor floor, Point2D at, out Action remove, out object? removedItem)
    {
        var (nearestTestPoint, nearestTestPointDist) = NearestTestPoint(floor, at);
        var (nearestAccessPoint, nearestAccessPointDist) = NearestAccessPoint(floor, at);
        var (nearestWall, nearestWallDist) = NearestWall(floor, at);

        double best = Math.Min(nearestTestPointDist, Math.Min(nearestAccessPointDist, nearestWallDist));
        if (best > DeleteHitToleranceMeters)
        {
            remove = static () => { };
            removedItem = null;
            return false;
        }

        if (best == nearestTestPointDist && nearestTestPoint is not null)
        {
            remove = () => floor.TestPoints.Remove(nearestTestPoint);
            removedItem = nearestTestPoint;
        }
        else if (best == nearestAccessPointDist && nearestAccessPoint is not null)
        {
            remove = () => floor.AccessPoints.Remove(nearestAccessPoint);
            removedItem = nearestAccessPoint;
        }
        else if (nearestWall is not null)
        {
            remove = () => floor.Walls.Remove(nearestWall);
            removedItem = nearestWall;
        }
        else
        {
            remove = static () => { };
            removedItem = null;
            return false;
        }

        return true;
    }

    private static (TestPoint? Item, double Distance) NearestTestPoint(Floor floor, Point2D at)
    {
        TestPoint? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (var testPoint in floor.TestPoints)
        {
            double distance = testPoint.Position.DistanceTo(at);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = testPoint;
            }
        }

        return (nearest, nearestDistance);
    }

    private static (AccessPoint? Item, double Distance) NearestAccessPoint(Floor floor, Point2D at)
    {
        AccessPoint? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (var accessPoint in floor.AccessPoints)
        {
            double distance = accessPoint.Position.DistanceTo(at);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = accessPoint;
            }
        }

        return (nearest, nearestDistance);
    }

    private static (Wall? Item, double Distance) NearestWall(Floor floor, Point2D at)
    {
        Wall? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (var wall in floor.Walls)
        {
            double distance = DistanceToSegment(at, wall.Start, wall.End);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = wall;
            }
        }

        return (nearest, nearestDistance);
    }

    private static double DistanceToSegment(Point2D point, Point2D segmentStart, Point2D segmentEnd)
    {
        double dx = segmentEnd.X - segmentStart.X;
        double dy = segmentEnd.Y - segmentStart.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared < double.Epsilon)
        {
            return point.DistanceTo(segmentStart);
        }

        double t = (((point.X - segmentStart.X) * dx) + ((point.Y - segmentStart.Y) * dy)) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        var projection = new Point2D(segmentStart.X + (t * dx), segmentStart.Y + (t * dy));
        return point.DistanceTo(projection);
    }
}
