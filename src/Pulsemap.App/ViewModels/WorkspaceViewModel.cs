using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Export;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Measurement;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Core.Placement;
using Pulsemap.App.Core.Propagation;
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

    private string? _filePath;

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
        IKrigingInterpolator krigingInterpolator)
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
    public partial NetworkAdapterInfo? SelectedAdapter { get; set; }

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
            OnPropertyChanged(nameof(AvailableBands));
            OnPropertyChanged(nameof(Floors));
            OnPropertyChanged(nameof(AccessPointSummaryDisplay));
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
        var adapters = await _wlanAdapterService.GetAdaptersAsync(cancellationToken);
        Adapters.Clear();
        foreach (var adapter in adapters)
        {
            Adapters.Add(adapter);
        }

        SelectedAdapter = Adapters.Count > 0 ? Adapters[0] : null;
    }

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
            var result = await _wlanAdapterService.ScanAsync(SelectedAdapter.Id);
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
            var scanResult = await _wlanAdapterService.ScanAsync(SelectedAdapter.Id);
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

    public async Task DeleteNearestElementAsync(Point2D at)
    {
        if (SelectedFloor is null || !TryFindNearestElement(SelectedFloor, at, out var remove))
        {
            return;
        }

        remove();
        _selectedWalls.RemoveAll(wall => !SelectedFloor.Walls.Contains(wall));
        SelectedWallCount = _selectedWalls.Count;
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
        if (Survey is null || SelectedFloor is null || SelectedAdapter is null)
        {
            return;
        }

        var scanResult = await _wlanAdapterService.ScanAsync(SelectedAdapter.Id);
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

    [RelayCommand]
    private async Task SuggestPlacementsAsync()
    {
        if (Survey is null || SelectedFloor is null)
        {
            return;
        }

        SelectedFloor.AccessPoints.RemoveAll(ap => !ap.IsUserOverride);
        var suggestions = _placementOptimizer.SuggestPlacements(SelectedFloor, Survey.Floors, Survey.TargetBands, _propagationModel);
        SelectedFloor.AccessPoints.AddRange(suggestions);
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
        await SaveAndRefreshAsync();
    }

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

    private async Task SaveAndRefreshAsync()
    {
        if (Survey is null || _filePath is null)
        {
            return;
        }

        await _surveyFileService.SaveAsync(Survey, _filePath);
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

    private static bool TryFindNearestElement(Floor floor, Point2D at, out Action remove)
    {
        var (nearestTestPoint, nearestTestPointDist) = NearestTestPoint(floor, at);
        var (nearestAccessPoint, nearestAccessPointDist) = NearestAccessPoint(floor, at);
        var (nearestWall, nearestWallDist) = NearestWall(floor, at);

        double best = Math.Min(nearestTestPointDist, Math.Min(nearestAccessPointDist, nearestWallDist));
        if (best > DeleteHitToleranceMeters)
        {
            remove = static () => { };
            return false;
        }

        if (best == nearestTestPointDist && nearestTestPoint is not null)
        {
            remove = () => floor.TestPoints.Remove(nearestTestPoint);
        }
        else if (best == nearestAccessPointDist && nearestAccessPoint is not null)
        {
            remove = () => floor.AccessPoints.Remove(nearestAccessPoint);
        }
        else if (nearestWall is not null)
        {
            remove = () => floor.Walls.Remove(nearestWall);
        }
        else
        {
            remove = static () => { };
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
