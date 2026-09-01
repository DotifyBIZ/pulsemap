using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Core.Placement;
using Pulsemap.App.Core.Propagation;
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

    private readonly ISurveyFileService _surveyFileService;
    private readonly IPropagationModel _propagationModel;
    private readonly IApPlacementOptimizer _placementOptimizer;
    private readonly IWlanAdapterService _wlanAdapterService;

    private string? _filePath;

    public WorkspaceViewModel(
        ISurveyFileService surveyFileService,
        IPropagationModel propagationModel,
        IApPlacementOptimizer placementOptimizer,
        IWlanAdapterService wlanAdapterService)
    {
        _surveyFileService = surveyFileService;
        _propagationModel = propagationModel;
        _placementOptimizer = placementOptimizer;
        _wlanAdapterService = wlanAdapterService;
    }

    public event EventHandler? FloorChanged;

    [ObservableProperty]
    public partial Survey? Survey { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CoveragePercentDisplay))]
    public partial double CoveragePercent { get; set; }

    [ObservableProperty]
    public partial Band SelectedBand { get; set; }

    public IReadOnlyList<Band> AvailableBands { get; private set; } = [];

    public IReadOnlyList<CoverageSample> Heatmap { get; private set; } = [];

    public string SurveyNameDisplay => Survey?.Name ?? string.Empty;

    public string CoveragePercentDisplay => $"{CoveragePercent:0}% of the floor at -67dBm or better";

    public string AccessPointSummaryDisplay
    {
        get
        {
            if (Survey is null || Survey.Floor.AccessPoints.Count == 0)
            {
                return "No access points placed yet.";
            }

            return string.Join(
                Environment.NewLine,
                Survey.Floor.AccessPoints.Select(ap =>
                {
                    string radios = string.Join(", ", ap.Radios.Select(r => $"{BandDisplayName(r.Key)} ch{r.Value.Channel}"));
                    return $"{ap.Label} — {radios}";
                }));
        }
    }

    public static string BandDisplayName(Band band) => band switch
    {
        Band.TwoPointFourGhz => "2.4 GHz",
        Band.FiveGhz => "5 GHz",
        Band.SixGhz => "6 GHz",
        _ => band.ToString(),
    };

    // Adapter tab
    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    public ObservableCollection<WlanNetworkDisplay> ScanResults { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
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
            OnPropertyChanged(nameof(SurveyNameDisplay));
            OnPropertyChanged(nameof(AvailableBands));
            OnPropertyChanged(nameof(AccessPointSummaryDisplay));
            Recompute();
            await LoadAdaptersAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ErrorMessage = $"Couldn't open this survey: {ex.Message}";
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
                    foreach (var network in result.Networks.OrderByDescending(n => n.SignalDbm))
                    {
                        string bandPart = network.Band is { } band ? BandDisplayName(band) : "unknown band";
                        string ssid = string.IsNullOrEmpty(network.Ssid) ? "(hidden network)" : network.Ssid;
                        ScanResults.Add(new WlanNetworkDisplay(
                            ssid,
                            $"{network.Bssid} · ch{network.Channel} · {bandPart} · {network.SignalDbm:0} dBm"));
                    }

                    ScanStatusMessage = result.Networks.Count == 0 ? "No networks found nearby." : null;
                    break;

                case WlanScanStatus.LocationAccessDenied:
                    ScanStatusMessage = "Windows needs Location access to show WiFi scan results for this app.";
                    break;

                case WlanScanStatus.NoAdapter:
                    ScanStatusMessage = "Couldn't reach the WLAN service — is WiFi hardware available and enabled?";
                    break;

                case WlanScanStatus.Failed:
                default:
                    ScanStatusMessage = "The scan didn't complete. Try again.";
                    break;
            }
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private static async Task OpenLocationSettingsAsync() =>
        await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-location"));

    public async Task AddTestPointAsync(Point2D position)
    {
        if (Survey is null)
        {
            return;
        }

        Survey.Floor.TestPoints.Add(new TestPoint { Position = position });
        await SaveAndRefreshAsync();
    }

    public async Task AddWallAsync(Point2D start, Point2D end)
    {
        if (Survey is null)
        {
            return;
        }

        Survey.Floor.Walls.Add(new Wall { Start = start, End = end });
        await SaveAndRefreshAsync();
    }

    public async Task DeleteNearestElementAsync(Point2D at)
    {
        if (Survey is null || !TryFindNearestElement(Survey.Floor, at, out var remove))
        {
            return;
        }

        remove();
        await SaveAndRefreshAsync();
    }

    [RelayCommand]
    private async Task SuggestPlacementsAsync()
    {
        if (Survey is null)
        {
            return;
        }

        Survey.Floor.AccessPoints.RemoveAll(ap => !ap.IsUserOverride);
        var suggestions = _placementOptimizer.SuggestPlacements(Survey.Floor, Survey.TargetBands, _propagationModel);
        Survey.Floor.AccessPoints.AddRange(suggestions);
        await SaveAndRefreshAsync();
    }

    partial void OnSelectedBandChanged(Band value)
    {
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
        if (Survey is null)
        {
            Heatmap = [];
            CoveragePercent = 0;
            return;
        }

        Heatmap = CoverageGridCalculator.ComputeGrid(Survey.Floor, SelectedBand, HeatmapGridSpacingMeters, _propagationModel);
        CoveragePercent = Heatmap.Count == 0
            ? 0
            : Heatmap.Count(s => s.ValueDbm >= ReliableCoverageThresholdDbm) / (double)Heatmap.Count * 100;
    }

    private static bool TryFindNearestElement(Floor floor, Point2D at, out Action remove)
    {
        TestPoint? nearestTestPoint = null;
        double nearestTestPointDist = double.MaxValue;
        foreach (var testPoint in floor.TestPoints)
        {
            double distance = testPoint.Position.DistanceTo(at);
            if (distance < nearestTestPointDist)
            {
                nearestTestPointDist = distance;
                nearestTestPoint = testPoint;
            }
        }

        AccessPoint? nearestAccessPoint = null;
        double nearestAccessPointDist = double.MaxValue;
        foreach (var accessPoint in floor.AccessPoints)
        {
            double distance = accessPoint.Position.DistanceTo(at);
            if (distance < nearestAccessPointDist)
            {
                nearestAccessPointDist = distance;
                nearestAccessPoint = accessPoint;
            }
        }

        Wall? nearestWall = null;
        double nearestWallDist = double.MaxValue;
        foreach (var wall in floor.Walls)
        {
            double distance = DistanceToSegment(at, wall.Start, wall.End);
            if (distance < nearestWallDist)
            {
                nearestWallDist = distance;
                nearestWall = wall;
            }
        }

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
