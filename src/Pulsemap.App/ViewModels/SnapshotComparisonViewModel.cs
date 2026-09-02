using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.ViewModels;

/// <summary>Drives the side-by-side snapshot comparison page: two independently-pickable states
/// ("Current" or a saved <see cref="SurveySnapshot"/>) of the same floor, each rendered on its own
/// canvas. Heatmaps are recomputed live from whichever floor/interference data that side's state
/// actually had — nothing is cached from snapshot time, since the propagation engine is pure and
/// deterministic.</summary>
public sealed partial class SnapshotComparisonViewModel(IPropagationModel propagationModel, ISurveyFileService surveyFileService) : ObservableObject
{
    private const double HeatmapGridSpacingMeters = 0.5;

    private string? _filePath;

    /// <summary>Fires whenever either side's resolved floor or heatmap changes — the page re-renders
    /// both canvases in response, mirroring WorkspaceViewModel's FloorChanged.</summary>
    public event EventHandler? Changed;

    public Survey? Survey { get; private set; }

    public IReadOnlyList<SnapshotOption> Options { get; private set; } = [];

    public IReadOnlyList<Floor> Floors => Survey?.Floors ?? [];

    public IReadOnlyList<Band> AvailableBands => Survey?.TargetBands ?? [];

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
    public partial Band SelectedBand { get; set; }

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

        RefreshOptions();

        SelectedFloor = survey.Floors.Count > 0 ? survey.Floors[0] : null;
        SelectedBand = survey.TargetBands.Count > 0 ? survey.TargetBands[0] : Band.TwoPointFourGhz;
    }

    private void RefreshOptions(Guid? preferredLeftId = null, Guid? preferredRightId = null)
    {
        if (Survey is not { } survey)
        {
            return;
        }

        List<SnapshotOption> options =
        [
            new SnapshotOption(null, "Current", survey.Floors),
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

        survey.Snapshots.RemoveAll(s => s.Id == snapshotId);
        await surveyFileService.SaveAsync(survey, _filePath);

        // The side that wasn't just deleted keeps pointing at whatever it already had (falling back
        // to "Current" only if that snapshot was also removed, e.g. both sides had it selected); the
        // deleted side always falls back to "Current" via RefreshOptions' own null-preferred-id default.
        RefreshOptions(preferredLeftId: keepLeftId, preferredRightId: keepRightId);
    }

    partial void OnLeftOptionChanged(SnapshotOption? value) => Recompute();

    partial void OnRightOptionChanged(SnapshotOption? value) => Recompute();

    partial void OnSelectedFloorChanged(Floor? value) => Recompute();

    partial void OnSelectedBandChanged(Band value) => Recompute();

    private void Recompute()
    {
        LeftFloor = ResolveFloor(LeftOption);
        RightFloor = ResolveFloor(RightOption);

        LeftHeatmap = LeftFloor is { } leftFloor && LeftOption is { } leftOption
            ? CoverageGridCalculator.ComputeGrid(leftFloor, leftOption.Floors, SelectedBand, HeatmapGridSpacingMeters, propagationModel)
            : [];
        RightHeatmap = RightFloor is { } rightFloor && RightOption is { } rightOption
            ? CoverageGridCalculator.ComputeGrid(rightFloor, rightOption.Floors, SelectedBand, HeatmapGridSpacingMeters, propagationModel)
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
