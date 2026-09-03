using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Logging;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Core.Propagation;
using Pulsemap.App.Services;

namespace Pulsemap.App.ViewModels;

/// <summary>Drives the side-by-side snapshot comparison page: two independently-pickable states
/// ("Current" or a saved <see cref="SurveySnapshot"/>) of the same floor, each rendered on its own
/// canvas. Heatmaps are recomputed live from whichever floor/interference data that side's state
/// actually had — nothing is cached from snapshot time, since the propagation engine is pure and
/// deterministic.</summary>
public sealed partial class SnapshotComparisonViewModel(
    IPropagationModel propagationModel,
    ISurveyFileService surveyFileService,
    ILocalizationService localizationService,
    IAppLogger logger) : ObservableObject
{
    private const double HeatmapGridSpacingMeters = 0.5;

    private string? _filePath;

    /// <summary>Fires whenever either side's resolved floor or heatmap changes — the page re-renders
    /// both canvases in response, mirroring WorkspaceViewModel's FloorChanged.</summary>
    public event EventHandler? Changed;

    public Survey? Survey { get; private set; }

    public IReadOnlyList<SnapshotOption> Options { get; private set; } = [];

    public IReadOnlyList<Floor> Floors => Survey?.Floors ?? [];

    /// <summary>Band pickers elsewhere in the app show localized names; binding the bare enum here
    /// put raw identifiers ("TwoPointFourGhz") in front of the user. Projected into a display
    /// record so the ComboBox can still bind a real selection back to a <see cref="Band"/>.</summary>
    /// <remarks>Built once in <see cref="Initialize"/> rather than projected on each get: the
    /// ComboBox re-reads this whenever the property signals, and handing it a fresh list every
    /// time makes it drop and re-resolve its selection for no reason.</remarks>
    public IReadOnlyList<BandChoice> AvailableBands { get; private set; } = [];

    private string BandDisplayName(Band band) => band switch
    {
        Band.TwoPointFourGhz => localizationService.GetString("WizardBand24Checkbox.Content"),
        Band.FiveGhz => localizationService.GetString("WizardBand5Checkbox.Content"),
        Band.SixGhz => localizationService.GetString("WizardBand6Checkbox.Content"),
        _ => band.ToString(),
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteLeftSnapshot))]
    [NotifyCanExecuteChangedFor(nameof(DeleteLeftSnapshotCommand))]
    public partial SnapshotOption? LeftOption { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteRightSnapshot))]
    [NotifyCanExecuteChangedFor(nameof(DeleteRightSnapshotCommand))]
    public partial SnapshotOption? RightOption { get; set; }

    [ObservableProperty]
    public partial Floor? SelectedFloor { get; set; }

    [ObservableProperty]
    public partial BandChoice? SelectedBand { get; set; }

    public Floor? LeftFloor { get; private set; }

    public Floor? RightFloor { get; private set; }

    public IReadOnlyList<CoverageSample> LeftHeatmap { get; private set; } = [];

    public IReadOnlyList<CoverageSample> RightHeatmap { get; private set; } = [];

    /// <summary>"Current" (SnapshotId null) isn't a real, deletable snapshot — only an actual saved
    /// <see cref="SurveySnapshot"/> is.</summary>
    public bool CanDeleteLeftSnapshot => LeftOption?.SnapshotId is not null;

    public bool CanDeleteRightSnapshot => RightOption?.SnapshotId is not null;

    public void Initialize(Survey survey, string filePath)
    {
        Survey = survey;
        _filePath = filePath;
        AvailableBands = [.. survey.TargetBands.Select(band => new BandChoice(band, BandDisplayName(band)))];

        RefreshOptions();

        SelectedFloor = survey.Floors.Count > 0 ? survey.Floors[0] : null;
        SelectedBand = AvailableBands.Count > 0 ? AvailableBands[0] : null;
    }

    private void RefreshOptions(Guid? preferredLeftId = null, Guid? preferredRightId = null)
    {
        if (Survey is not { } survey)
        {
            return;
        }

        List<SnapshotOption> options =
        [
            new SnapshotOption(null, localizationService.GetString("SnapshotComparisonCurrentOptionLabel"), survey.Floors),
            .. survey.Snapshots.Select(snapshot => new SnapshotOption(snapshot.Id, snapshot.Label, snapshot.Floors)),
        ];
        Options = options;

        OnPropertyChanged(nameof(Options));
        OnPropertyChanged(nameof(Floors));
        OnPropertyChanged(nameof(AvailableBands));

        LeftOption = options.FirstOrDefault(o => o.SnapshotId == preferredLeftId) ?? (options.Count > 0 ? options[0] : null);
        RightOption = options.FirstOrDefault(o => o.SnapshotId == preferredRightId) ?? (options.Count > 0 ? options[0] : null);
    }

    [RelayCommand(CanExecute = nameof(CanDeleteLeftSnapshot))]
    private async Task DeleteLeftSnapshotAsync() => await DeleteSnapshotAsync(LeftOption, keepRightId: RightOption?.SnapshotId, keepLeftId: null);

    [RelayCommand(CanExecute = nameof(CanDeleteRightSnapshot))]
    private async Task DeleteRightSnapshotAsync() => await DeleteSnapshotAsync(RightOption, keepRightId: null, keepLeftId: LeftOption?.SnapshotId);

    private async Task DeleteSnapshotAsync(SnapshotOption? toDelete, Guid? keepLeftId, Guid? keepRightId)
    {
        if (Survey is not { } survey || _filePath is null || toDelete?.SnapshotId is not { } snapshotId)
        {
            return;
        }

        int removedIndex = survey.Snapshots.FindIndex(s => s.Id == snapshotId);
        if (removedIndex < 0)
        {
            return;
        }

        var removed = survey.Snapshots[removedIndex];
        survey.Snapshots.RemoveAt(removedIndex);
        try
        {
            await surveyFileService.SaveAsync(survey, _filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reached from an async void click handler — report it, and put the snapshot back so
            // the in-memory survey still matches the file that's actually on disk.
            survey.Snapshots.Insert(removedIndex, removed);
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, localizationService.GetString("SnapshotComparisonDeleteErrorFormat"), ex.Message);
            await logger.LogErrorAsync("Failed to delete a survey snapshot.", ex);
            return;
        }

        ErrorMessage = null;

        // The side that wasn't just deleted keeps pointing at whatever it already had (falling back
        // to "Current" only if that snapshot was also removed, e.g. both sides had it selected); the
        // deleted side always falls back to "Current" via RefreshOptions' own null-preferred-id default.
        RefreshOptions(preferredLeftId: keepLeftId, preferredRightId: keepRightId);
    }

    partial void OnLeftOptionChanged(SnapshotOption? value) => Recompute();

    partial void OnRightOptionChanged(SnapshotOption? value) => Recompute();

    partial void OnSelectedFloorChanged(Floor? value) => Recompute();

    partial void OnSelectedBandChanged(BandChoice? value) => Recompute();

    private void Recompute()
    {
        LeftFloor = ResolveFloor(LeftOption);
        RightFloor = ResolveFloor(RightOption);

        var band = SelectedBand?.Band;

        LeftHeatmap = band is { } leftBand && LeftFloor is { } leftFloor && LeftOption is { } leftOption
            ? CoverageGridCalculator.ComputeGrid(leftFloor, leftOption.Floors, leftBand, HeatmapGridSpacingMeters, propagationModel)
            : [];
        RightHeatmap = band is { } rightBand && RightFloor is { } rightFloor && RightOption is { } rightOption
            ? CoverageGridCalculator.ComputeGrid(rightFloor, rightOption.Floors, rightBand, HeatmapGridSpacingMeters, propagationModel)
            : [];

        OnPropertyChanged(nameof(LeftFloor));
        OnPropertyChanged(nameof(RightFloor));
        OnPropertyChanged(nameof(LeftHeatmap));
        OnPropertyChanged(nameof(RightHeatmap));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Floor? ResolveFloor(SnapshotOption? option)
    {
        if (option is null || SelectedFloor is null)
        {
            return null;
        }

        return option.Floors.FirstOrDefault(floor => floor.Id == SelectedFloor.Id);
    }
}
