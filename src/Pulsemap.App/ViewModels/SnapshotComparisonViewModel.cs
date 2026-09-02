using CommunityToolkit.Mvvm.ComponentModel;
using Pulsemap.App.Core.Interpolation;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;

namespace Pulsemap.App.ViewModels;

/// <summary>Drives the side-by-side snapshot comparison page: two independently-pickable states
/// ("Current" or a saved <see cref="SurveySnapshot"/>) of the same floor, each rendered on its own
/// canvas. Heatmaps are recomputed live from whichever floor/interference data that side's state
/// actually had — nothing is cached from snapshot time, since the propagation engine is pure and
/// deterministic.</summary>
public sealed partial class SnapshotComparisonViewModel(IPropagationModel propagationModel) : ObservableObject
{
    private const double HeatmapGridSpacingMeters = 0.5;

    /// <summary>Fires whenever either side's resolved floor or heatmap changes — the page re-renders
    /// both canvases in response, mirroring WorkspaceViewModel's FloorChanged.</summary>
    public event EventHandler? Changed;

    public Survey? Survey { get; private set; }

    public IReadOnlyList<SnapshotOption> Options { get; private set; } = [];

    public IReadOnlyList<Floor> Floors => Survey?.Floors ?? [];

    public IReadOnlyList<Band> AvailableBands => Survey?.TargetBands ?? [];

    [ObservableProperty]
    public partial SnapshotOption? LeftOption { get; set; }

    [ObservableProperty]
    public partial SnapshotOption? RightOption { get; set; }

    [ObservableProperty]
    public partial Floor? SelectedFloor { get; set; }

    [ObservableProperty]
    public partial Band SelectedBand { get; set; }

    public Floor? LeftFloor { get; private set; }

    public Floor? RightFloor { get; private set; }

    public IReadOnlyList<CoverageSample> LeftHeatmap { get; private set; } = [];

    public IReadOnlyList<CoverageSample> RightHeatmap { get; private set; } = [];

    public void Initialize(Survey survey)
    {
        Survey = survey;

        List<SnapshotOption> options =
        [
            new SnapshotOption(null, "Current", survey.Floors),
            .. survey.Snapshots.Select(snapshot => new SnapshotOption(snapshot.Id, snapshot.Label, snapshot.Floors)),
        ];
        Options = options;

        OnPropertyChanged(nameof(Options));
        OnPropertyChanged(nameof(Floors));
        OnPropertyChanged(nameof(AvailableBands));

        LeftOption = options.Count > 0 ? options[0] : null;
        RightOption = options.Count > 0 ? options[0] : null;
        SelectedFloor = survey.Floors.Count > 0 ? survey.Floors[0] : null;
        SelectedBand = survey.TargetBands.Count > 0 ? survey.TargetBands[0] : Band.TwoPointFourGhz;
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
