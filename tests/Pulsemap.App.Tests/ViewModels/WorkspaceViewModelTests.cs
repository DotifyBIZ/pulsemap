using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Measurement;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class WorkspaceViewModelTests
{
    private const string FilePath = "C:\\FakeSurveys\\Test.pulsemap";

    private readonly FakeSurveyFileService _surveyFileService = new();
    private readonly FakePropagationModel _propagationModel = new();
    private readonly FakeApPlacementOptimizer _placementOptimizer = new();
    private readonly FakeWlanAdapterService _wlanAdapterService = new();
    private readonly FakeLocalizationService _localizationService = new();
    private readonly FakeAppLogger _logger = new();

    private WorkspaceViewModel CreateSut() => new(_surveyFileService, _propagationModel, _placementOptimizer, _wlanAdapterService, _localizationService, _logger);

    [Fact]
    public async Task LoadAsync_ValidSurvey_PopulatesSurveyAndBands()
    {
        _surveyFileService.SurveyToReturn = BuildSurvey(SquareRoomFloor(10));
        var sut = CreateSut();

        await sut.LoadAsync(FilePath);

        Assert.NotNull(sut.Survey);
        Assert.Equal("Test Survey", sut.SurveyNameDisplay);
        Assert.Equal(Band.TwoPointFourGhz, sut.SelectedBand);
        Assert.Contains(Band.TwoPointFourGhz, sut.AvailableBands);
    }

    [Fact]
    public async Task LoadAsync_LoadThrowsIOException_SetsErrorMessage()
    {
        _surveyFileService.LoadExceptionToThrow = new IOException("disk error");
        var sut = CreateSut();

        await sut.LoadAsync(FilePath);

        Assert.Null(sut.Survey);
        Assert.NotNull(sut.ErrorMessage);
    }

    [Fact]
    public async Task LoadAsync_PopulatesAdaptersAndSelectsFirst()
    {
        _surveyFileService.SurveyToReturn = BuildSurvey(SquareRoomFloor(10));
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Test Adapter");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        var sut = CreateSut();

        await sut.LoadAsync(FilePath);

        Assert.Single(sut.Adapters);
        Assert.Equal(adapter, sut.SelectedAdapter);
    }

    [Fact]
    public async Task AddTestPointAsync_AddsPointAndSavesSurvey()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));

        await sut.AddTestPointAsync(new Point2D(1, 1));

        Assert.Single(sut.Survey!.Floor.TestPoints);
        Assert.Single(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task AddWallAsync_AddsWallAndSavesSurvey()
    {
        var sut = await LoadedViewModelAsync(new Floor { PlanSource = new RoomListSource() });

        await sut.AddWallAsync(new Point2D(0, 0), new Point2D(1, 0));

        Assert.Single(sut.Survey!.Floor.Walls);
        Assert.Single(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task DeleteNearestElementAsync_WithinTolerance_RemovesNearestTestPoint()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var sut = await LoadedViewModelAsync(floor);

        await sut.DeleteNearestElementAsync(new Point2D(5.1, 5.1));

        Assert.Empty(sut.Survey!.Floor.TestPoints);
    }

    [Fact]
    public async Task DeleteNearestElementAsync_OutsideTolerance_DoesNothing()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var sut = await LoadedViewModelAsync(floor);

        await sut.DeleteNearestElementAsync(new Point2D(9, 9));

        Assert.Single(sut.Survey!.Floor.TestPoints);
    }

    [Fact]
    public async Task SuggestPlacementsCommand_RemovesNonOverrideApsButKeepsUserOverrides()
    {
        var floor = SquareRoomFloor(10);
        var overrideAp = new AccessPoint { Position = new Point2D(1, 1), Label = "Manual AP", IsUserOverride = true };
        var suggestedAp = new AccessPoint { Position = new Point2D(2, 2), Label = "Old Suggestion" };
        floor.AccessPoints.Add(overrideAp);
        floor.AccessPoints.Add(suggestedAp);
        var sut = await LoadedViewModelAsync(floor);

        var newSuggestion = new AccessPoint { Position = new Point2D(3, 3), Label = "New Suggestion" };
        _placementOptimizer.PlacementsToReturn = [newSuggestion];

        await sut.SuggestPlacementsCommand.ExecuteAsync(null);

        Assert.Contains(overrideAp, sut.Survey!.Floor.AccessPoints);
        Assert.DoesNotContain(suggestedAp, sut.Survey.Floor.AccessPoints);
        Assert.Contains(newSuggestion, sut.Survey.Floor.AccessPoints);
    }

    [Fact]
    public async Task ScanCommand_Success_PopulatesScanResults()
    {
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Test Adapter");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        _wlanAdapterService.DefaultScanResult = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("SomeNetwork", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 6, -50),
        ]);
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));

        await sut.ScanCommand.ExecuteAsync(null);

        Assert.Single(sut.ScanResults);
        Assert.Equal("SomeNetwork", sut.ScanResults[0].Ssid);
    }

    [Fact]
    public async Task ScanCommand_LocationAccessDenied_SetsStatusMessageAndShowsSettingsLink()
    {
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Test Adapter");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        _wlanAdapterService.DefaultScanResult = new WlanScanResult(WlanScanStatus.LocationAccessDenied, []);
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));

        await sut.ScanCommand.ExecuteAsync(null);

        Assert.True(sut.HasScanStatusMessage);
        Assert.True(sut.ShowLocationSettingsLink);
    }

    [Fact]
    public async Task ScanCommand_CanExecute_FalseWithoutSelectedAdapter()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));

        Assert.False(sut.ScanCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartGuidedWalkCommand_CanExecute_FalseWithoutSelectedAdapter()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));

        Assert.False(sut.StartGuidedWalkCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartGuidedWalkCommand_NoWalls_SetsStatusMessageAndStaysIdle()
    {
        var sut = await LoadedViewModelWithAdapterAsync(new Floor { PlanSource = new RoomListSource() });

        sut.StartGuidedWalkCommand.Execute(null);

        Assert.False(sut.IsGuidedWalkActive);
        Assert.True(sut.HasGuidedWalkStatusMessage);
    }

    [Fact]
    public async Task StartGuidedWalkCommand_WithWalls_ActivatesWalkAndShowsCorrectProgress()
    {
        var floor = SquareRoomFloor(10);
        var expectedPoints = MeasurementPointSuggester.SuggestPoints(floor);
        var sut = await LoadedViewModelWithAdapterAsync(floor);

        sut.StartGuidedWalkCommand.Execute(null);

        Assert.True(sut.IsGuidedWalkActive);
        Assert.Equal(expectedPoints[0], sut.CurrentWalkPoint);
        Assert.Equal(
            $"Point 1 of {expectedPoints.Count} — walk to ({expectedPoints[0].X:0.0}m, {expectedPoints[0].Y:0.0}m) and confirm.",
            sut.GuidedWalkProgressDisplay);
    }

    // Regression test: ConfirmWalkPointCommand stayed permanently disabled after starting a walk
    // because CurrentWalkPoint's setter didn't notify the command's CanExecute — the walk would
    // activate and show a target, but the confirm button never became clickable. Caught via real
    // UI automation, not a test, the first time; this is the test that should have caught it.
    [Fact]
    public async Task StartGuidedWalkCommand_ConfirmWalkPointCommand_CanExecuteImmediatelyAfterStart()
    {
        var sut = await LoadedViewModelWithAdapterAsync(SquareRoomFloor(10));

        sut.StartGuidedWalkCommand.Execute(null);

        Assert.True(sut.ConfirmWalkPointCommand.CanExecute(null));
    }

    [Fact]
    public async Task ConfirmWalkPointCommand_Success_CapturesTestPointSavesAndAdvances()
    {
        var floor = SquareRoomFloor(10);
        var expectedPoints = MeasurementPointSuggester.SuggestPoints(floor);
        _wlanAdapterService.DefaultScanResult = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("Neighbor", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 6, -60),
        ]);
        var sut = await LoadedViewModelWithAdapterAsync(floor);
        sut.StartGuidedWalkCommand.Execute(null);

        await sut.ConfirmWalkPointCommand.ExecuteAsync(null);

        Assert.Single(sut.Survey!.Floor.TestPoints);
        Assert.Equal(expectedPoints[0], sut.Survey.Floor.TestPoints[0].Position);
        Assert.Single(sut.Survey.Floor.TestPoints[0].InterferenceReadings);
        Assert.NotEmpty(_surveyFileService.SaveCalls);
        Assert.True(sut.IsGuidedWalkActive);
        Assert.Equal(expectedPoints[1], sut.CurrentWalkPoint);
    }

    [Fact]
    public async Task ConfirmWalkPointCommand_ScanFails_DoesNotAdvanceAndShowsStatus()
    {
        var floor = SquareRoomFloor(10);
        var expectedPoints = MeasurementPointSuggester.SuggestPoints(floor);
        _wlanAdapterService.DefaultScanResult = new WlanScanResult(WlanScanStatus.NoAdapter, []);
        var sut = await LoadedViewModelWithAdapterAsync(floor);
        sut.StartGuidedWalkCommand.Execute(null);

        await sut.ConfirmWalkPointCommand.ExecuteAsync(null);

        Assert.Empty(sut.Survey!.Floor.TestPoints);
        Assert.True(sut.IsGuidedWalkActive);
        Assert.Equal(expectedPoints[0], sut.CurrentWalkPoint);
        Assert.True(sut.HasGuidedWalkStatusMessage);
    }

    [Fact]
    public async Task ConfirmWalkPointCommand_LastPoint_CompletesWalkAndDeactivates()
    {
        // A 1x1m floor collapses MeasurementPointSuggester's 3m grid to exactly one candidate.
        var floor = SquareRoomFloor(1);
        var sut = await LoadedViewModelWithAdapterAsync(floor);
        sut.StartGuidedWalkCommand.Execute(null);

        await sut.ConfirmWalkPointCommand.ExecuteAsync(null);

        Assert.False(sut.IsGuidedWalkActive);
        Assert.Null(sut.CurrentWalkPoint);
        Assert.Single(sut.Survey!.Floor.TestPoints);
    }

    [Fact]
    public async Task CancelGuidedWalkCommand_StopsWalkAndKeepsCapturedPoints()
    {
        var floor = SquareRoomFloor(10);
        var sut = await LoadedViewModelWithAdapterAsync(floor);
        sut.StartGuidedWalkCommand.Execute(null);
        await sut.ConfirmWalkPointCommand.ExecuteAsync(null);

        sut.CancelGuidedWalkCommand.Execute(null);

        Assert.False(sut.IsGuidedWalkActive);
        Assert.Single(sut.Survey!.Floor.TestPoints);
        Assert.True(sut.HasGuidedWalkStatusMessage);
    }

    private async Task<WorkspaceViewModel> LoadedViewModelAsync(Floor floor)
    {
        _surveyFileService.SurveyToReturn = BuildSurvey(floor);
        var sut = CreateSut();
        await sut.LoadAsync(FilePath);
        return sut;
    }

    private async Task<WorkspaceViewModel> LoadedViewModelWithAdapterAsync(Floor floor)
    {
        _wlanAdapterService.AdaptersToReturn = [new NetworkAdapterInfo(Guid.NewGuid(), "Test Adapter")];
        return await LoadedViewModelAsync(floor);
    }

    private static Survey BuildSurvey(Floor floor) => new()
    {
        Name = "Test Survey",
        Type = SurveyType.NewDeployment,
        TargetBands = [Band.TwoPointFourGhz],
        Floor = floor,
    };

    private static Floor SquareRoomFloor(double sizeMeters) => new()
    {
        PlanSource = new RoomListSource(),
        Walls =
        {
            new Wall { Start = new Point2D(0, 0), End = new Point2D(sizeMeters, 0) },
            new Wall { Start = new Point2D(sizeMeters, 0), End = new Point2D(sizeMeters, sizeMeters) },
            new Wall { Start = new Point2D(sizeMeters, sizeMeters), End = new Point2D(0, sizeMeters) },
            new Wall { Start = new Point2D(0, sizeMeters), End = new Point2D(0, 0) },
        },
    };
}
