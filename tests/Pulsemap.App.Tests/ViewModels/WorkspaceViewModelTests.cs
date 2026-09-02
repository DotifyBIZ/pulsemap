using Pulsemap.App.Core.Abstractions;
using Pulsemap.App.Core.Interpolation;
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
    private readonly FakeSurveyDataExporter _surveyDataExporter = new();
    private readonly FakeReportExporter _reportExporter = new();
    private readonly FakeSurveyExportFilePickerService _exportFilePickerService = new();
    private readonly FakeAppSettingsService _appSettingsService = new();
    private readonly FakeLinkDiagnosticsService _linkDiagnosticsService = new();
    private readonly FakeNetworkHealthService _networkHealthService = new();

    // Real (not faked) — pure/deterministic, no I/O, and every test here loads a floor with zero
    // existing TestPoints, so StartGuidedWalk's adaptive path never actually calls into this (falls
    // back to the plain grid, per MeasurementPointSuggester's own MinimumMeasurementsForAdaptiveOrdering
    // guard) — nothing here exercises its real behavior, so a fake would just be unused boilerplate.
    private readonly OrdinaryKrigingInterpolator _krigingInterpolator = new();

    private WorkspaceViewModel CreateSut() => new(
        _surveyFileService,
        _propagationModel,
        _placementOptimizer,
        _wlanAdapterService,
        _localizationService,
        _logger,
        _surveyDataExporter,
        _reportExporter,
        _exportFilePickerService,
        _krigingInterpolator,
        _appSettingsService,
        _linkDiagnosticsService,
        _networkHealthService);

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
    public async Task ShouldShowOnboardingAsync_NotSeenBefore_ReturnsTrue()
    {
        _appSettingsService.SettingsToReturn = new Core.Settings.AppSettings { HasSeenWorkspaceOnboarding = false };
        var sut = CreateSut();

        Assert.True(await sut.ShouldShowOnboardingAsync());
    }

    [Fact]
    public async Task ShouldShowOnboardingAsync_AlreadySeen_ReturnsFalse()
    {
        _appSettingsService.SettingsToReturn = new Core.Settings.AppSettings { HasSeenWorkspaceOnboarding = true };
        var sut = CreateSut();

        Assert.False(await sut.ShouldShowOnboardingAsync());
    }

    [Fact]
    public async Task MarkOnboardingSeenAsync_PersistsFlagAndFutureCheckReturnsFalse()
    {
        var sut = CreateSut();

        await sut.MarkOnboardingSeenAsync();

        Assert.True(_appSettingsService.LastSaved?.HasSeenWorkspaceOnboarding);
        Assert.False(await sut.ShouldShowOnboardingAsync());
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

        Assert.Single(sut.SelectedFloor!.TestPoints);
        Assert.Single(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task AddWallAsync_AddsWallAndSavesSurvey()
    {
        var sut = await LoadedViewModelAsync(new Floor { PlanSource = new RoomListSource() });

        await sut.AddWallAsync(new Point2D(0, 0), new Point2D(1, 0));

        Assert.Single(sut.SelectedFloor!.Walls);
        Assert.Single(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task UpdateOutdoorBoundsAsync_OutdoorFloor_UpdatesBoundsAndSaves()
    {
        var floor = new Floor { PlanSource = new RoomListSource(), IsOutdoor = true, OutdoorBoundsMin = new Point2D(0, 0), OutdoorBoundsMax = new Point2D(40, 40) };
        var sut = await LoadedViewModelAsync(floor);

        await sut.UpdateOutdoorBoundsAsync(new Point2D(5, 5), new Point2D(25, 30));

        Assert.Equal(new Point2D(5, 5), sut.SelectedFloor!.OutdoorBoundsMin);
        Assert.Equal(new Point2D(25, 30), sut.SelectedFloor.OutdoorBoundsMax);
        Assert.Single(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task UpdateOutdoorBoundsAsync_IndoorFloor_DoesNothing()
    {
        var floor = SquareRoomFloor(10);
        var sut = await LoadedViewModelAsync(floor);

        await sut.UpdateOutdoorBoundsAsync(new Point2D(5, 5), new Point2D(25, 30));

        Assert.Null(sut.SelectedFloor!.OutdoorBoundsMin);
        Assert.Empty(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task DeleteNearestElementAsync_WithinTolerance_RemovesNearestTestPoint()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var sut = await LoadedViewModelAsync(floor);

        await sut.DeleteNearestElementAsync(new Point2D(5.1, 5.1));

        Assert.Empty(sut.SelectedFloor!.TestPoints);
    }

    [Fact]
    public async Task DeleteNearestElementAsync_OutsideTolerance_DoesNothing()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var sut = await LoadedViewModelAsync(floor);

        await sut.DeleteNearestElementAsync(new Point2D(9, 9));

        Assert.Single(sut.SelectedFloor!.TestPoints);
    }

    [Fact]
    public async Task DeleteNearestElementAsync_RemovesElement_CanUndoDeleteBecomesTrue()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var sut = await LoadedViewModelAsync(floor);

        await sut.DeleteNearestElementAsync(new Point2D(5.1, 5.1));

        Assert.True(sut.CanUndoDelete);
        Assert.True(sut.UndoDeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task UndoDeleteCommand_AfterDeletingTestPoint_RestoresIt()
    {
        var floor = SquareRoomFloor(10);
        var testPoint = new TestPoint { Position = new Point2D(5, 5) };
        floor.TestPoints.Add(testPoint);
        var sut = await LoadedViewModelAsync(floor);
        await sut.DeleteNearestElementAsync(new Point2D(5.1, 5.1));

        await sut.UndoDeleteCommand.ExecuteAsync(null);

        Assert.Same(testPoint, Assert.Single(sut.SelectedFloor!.TestPoints));
        Assert.False(sut.CanUndoDelete);
    }

    [Fact]
    public async Task UndoDeleteCommand_AfterDeletingWall_RestoresIt()
    {
        var floor = SquareRoomFloor(10);
        var wallToDelete = floor.Walls[0];
        var midpoint = new Point2D((wallToDelete.Start.X + wallToDelete.End.X) / 2, (wallToDelete.Start.Y + wallToDelete.End.Y) / 2);
        var sut = await LoadedViewModelAsync(floor);
        await sut.DeleteNearestElementAsync(midpoint);
        Assert.DoesNotContain(wallToDelete, sut.SelectedFloor!.Walls);

        await sut.UndoDeleteCommand.ExecuteAsync(null);

        Assert.Contains(wallToDelete, sut.SelectedFloor!.Walls);
    }

    [Fact]
    public async Task DeleteNearestElementAsync_SecondDelete_OverwritesUndoWithTheNewerOne()
    {
        var floor = SquareRoomFloor(10);
        var firstPoint = new TestPoint { Position = new Point2D(5, 5) };
        var secondPoint = new TestPoint { Position = new Point2D(6, 6) };
        floor.TestPoints.Add(firstPoint);
        floor.TestPoints.Add(secondPoint);
        var sut = await LoadedViewModelAsync(floor);
        await sut.DeleteNearestElementAsync(new Point2D(5.1, 5.1));

        await sut.DeleteNearestElementAsync(new Point2D(6.1, 6.1));
        await sut.UndoDeleteCommand.ExecuteAsync(null);

        var remaining = Assert.Single(sut.SelectedFloor!.TestPoints);
        Assert.Same(secondPoint, remaining);
    }

    [Fact]
    public async Task SwitchingFloor_ClearsUndoDeleteState()
    {
        var floorA = SquareRoomFloor(10);
        floorA.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var floorB = new Floor { PlanSource = new RoomListSource() };
        _surveyFileService.SurveyToReturn = new Survey
        {
            Name = "Test Survey",
            Type = SurveyType.NewDeployment,
            TargetBands = [Band.TwoPointFourGhz],
            Floors = [floorA, floorB],
        };
        var sut = CreateSut();
        await sut.LoadAsync(FilePath);
        await sut.DeleteNearestElementAsync(new Point2D(5.1, 5.1));
        Assert.True(sut.CanUndoDelete);

        sut.SelectedFloor = floorB;

        Assert.False(sut.CanUndoDelete);
    }

    [Fact]
    public async Task ToggleWallSelection_TogglesMembershipAndCount()
    {
        var floor = SquareRoomFloor(10);
        var sut = await LoadedViewModelAsync(floor);
        var wall = floor.Walls[0];

        sut.ToggleWallSelection(wall);

        Assert.Equal(1, sut.SelectedWallCount);
        Assert.Contains(wall, sut.SelectedWalls);
        Assert.True(sut.HasWallSelection);

        sut.ToggleWallSelection(wall);

        Assert.Equal(0, sut.SelectedWallCount);
        Assert.DoesNotContain(wall, sut.SelectedWalls);
        Assert.False(sut.HasWallSelection);
    }

    [Fact]
    public async Task ApplyMaterialToSelectedWallsAsync_SetsMaterialOnEverySelectedWallAndClearsSelection()
    {
        var floor = SquareRoomFloor(10);
        var sut = await LoadedViewModelAsync(floor);
        sut.ToggleWallSelection(floor.Walls[0]);
        sut.ToggleWallSelection(floor.Walls[1]);

        await sut.ApplyMaterialToSelectedWallsAsync(WallMaterial.Concrete, 0.2);

        Assert.Equal(WallMaterial.Concrete, floor.Walls[0].Material);
        Assert.Equal(0.2, floor.Walls[0].ThicknessMeters);
        Assert.Equal(WallMaterial.Concrete, floor.Walls[1].Material);
        Assert.Null(floor.Walls[2].Material);
        Assert.Equal(0, sut.SelectedWallCount);
        Assert.NotEmpty(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task FindNearestSelectable_ReturnsWallOrTestPointWithinToleranceElseNull()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(5, 5) });
        var sut = await LoadedViewModelAsync(floor);

        Assert.IsType<TestPoint>(sut.FindNearestSelectable(new Point2D(5.1, 5.1)));
        Assert.IsType<Wall>(sut.FindNearestSelectable(new Point2D(0, 0.05)));
        Assert.Null(sut.FindNearestSelectable(new Point2D(5, 8)));
    }

    [Fact]
    public async Task RecaptureTestPointAsync_ReplacesExistingTestPointWithNewScan()
    {
        var floor = SquareRoomFloor(10);
        var existing = new TestPoint { Position = new Point2D(5, 5) };
        floor.TestPoints.Add(existing);
        _wlanAdapterService.DefaultScanResult = new WlanScanResult(WlanScanStatus.Success, [
            new WlanNetworkReading("Neighbor", "AA:AA:AA:AA:AA:AA", Band.TwoPointFourGhz, 6, -70),
        ]);
        var sut = await LoadedViewModelWithAdapterAsync(floor);

        await sut.RecaptureTestPointAsync(existing);

        var rebuilt = Assert.Single(sut.SelectedFloor!.TestPoints);
        Assert.NotSame(existing, rebuilt);
        Assert.Equal(new Point2D(5, 5), rebuilt.Position);
        Assert.Single(rebuilt.InterferenceReadings);
        Assert.NotEmpty(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task RecaptureTestPointAsync_ScanFails_SetsErrorMessageAndKeepsOriginal()
    {
        var floor = SquareRoomFloor(10);
        var existing = new TestPoint { Position = new Point2D(5, 5) };
        floor.TestPoints.Add(existing);
        _wlanAdapterService.DefaultScanResult = new WlanScanResult(WlanScanStatus.NoAdapter, []);
        var sut = await LoadedViewModelWithAdapterAsync(floor);

        await sut.RecaptureTestPointAsync(existing);

        Assert.Same(existing, Assert.Single(sut.SelectedFloor!.TestPoints));
        Assert.True(sut.HasError);
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

        Assert.Contains(overrideAp, sut.SelectedFloor!.AccessPoints);
        Assert.DoesNotContain(suggestedAp, sut.SelectedFloor.AccessPoints);
        Assert.Contains(newSuggestion, sut.SelectedFloor.AccessPoints);
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

        Assert.Single(sut.SelectedFloor!.TestPoints);
        Assert.Equal(expectedPoints[0], sut.SelectedFloor.TestPoints[0].Position);
        Assert.Single(sut.SelectedFloor.TestPoints[0].InterferenceReadings);
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

        Assert.Empty(sut.SelectedFloor!.TestPoints);
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
        Assert.Single(sut.SelectedFloor!.TestPoints);
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
        Assert.Single(sut.SelectedFloor!.TestPoints);
        Assert.True(sut.HasGuidedWalkStatusMessage);
    }

    [Fact]
    public async Task StartGuidedWalkCommand_PersistsPendingPointsAndBandToFloor()
    {
        var floor = SquareRoomFloor(10);
        var expectedPoints = MeasurementPointSuggester.SuggestPoints(floor);
        var sut = await LoadedViewModelWithAdapterAsync(floor);

        await sut.StartGuidedWalkCommand.ExecuteAsync(null);

        Assert.Equal(expectedPoints, sut.SelectedFloor!.PendingGuidedWalkPoints);
        Assert.Equal(Band.TwoPointFourGhz, sut.SelectedFloor.PendingGuidedWalkBand);
        Assert.NotEmpty(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task SkipWalkPointCommand_DequeuesWithoutCapturingAndAdvances()
    {
        var floor = SquareRoomFloor(10);
        var expectedPoints = MeasurementPointSuggester.SuggestPoints(floor);
        var sut = await LoadedViewModelWithAdapterAsync(floor);
        await sut.StartGuidedWalkCommand.ExecuteAsync(null);

        await sut.SkipWalkPointCommand.ExecuteAsync(null);

        Assert.Empty(sut.SelectedFloor!.TestPoints);
        Assert.True(sut.IsGuidedWalkActive);
        Assert.Equal(expectedPoints[1], sut.CurrentWalkPoint);
        Assert.DoesNotContain(expectedPoints[0], sut.SelectedFloor.PendingGuidedWalkPoints);
    }

    [Fact]
    public async Task CancelGuidedWalkCommand_ClearsPendingPointsOnFloor()
    {
        var floor = SquareRoomFloor(10);
        var sut = await LoadedViewModelWithAdapterAsync(floor);
        await sut.StartGuidedWalkCommand.ExecuteAsync(null);

        await sut.CancelGuidedWalkCommand.ExecuteAsync(null);

        Assert.Empty(sut.SelectedFloor!.PendingGuidedWalkPoints);
    }

    [Fact]
    public async Task SwitchingToFloorWithPendingWalk_ResumesWalkAndSwitchingAwayPauses()
    {
        var floorA = SquareRoomFloor(10);
        var floorB = new Floor { PlanSource = new RoomListSource() };
        _surveyFileService.SurveyToReturn = new Survey
        {
            Name = "Test Survey",
            Type = SurveyType.NewDeployment,
            TargetBands = [Band.TwoPointFourGhz],
            Floors = [floorA, floorB],
        };
        _wlanAdapterService.AdaptersToReturn = [new NetworkAdapterInfo(Guid.NewGuid(), "Test Adapter")];
        var sut = CreateSut();
        await sut.LoadAsync(FilePath);
        var expectedPoints = MeasurementPointSuggester.SuggestPoints(floorA);
        await sut.StartGuidedWalkCommand.ExecuteAsync(null);

        sut.SelectedFloor = floorB;

        Assert.False(sut.IsGuidedWalkActive);

        sut.SelectedFloor = floorA;

        Assert.True(sut.IsGuidedWalkActive);
        Assert.Equal(expectedPoints[0], sut.CurrentWalkPoint);
    }

    [Fact]
    public async Task ExportTestPointsCsvCommand_UserPicksFile_CallsExporterAndSuggestsFileName()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        using var stream = new MemoryStream();
        _exportFilePickerService.StreamToReturn = stream;

        await sut.ExportTestPointsCsvCommand.ExecuteAsync(null);

        Assert.Equal(1, _surveyDataExporter.ExportTestPointsCsvCallCount);
        Assert.Equal("Test Survey-testpoints", _exportFilePickerService.LastSuggestedFileName);
        Assert.Equal(".csv", _exportFilePickerService.LastExtension);
    }

    [Fact]
    public async Task ExportAccessPointsCsvCommand_UserPicksFile_CallsExporter()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        using var stream = new MemoryStream();
        _exportFilePickerService.StreamToReturn = stream;

        await sut.ExportAccessPointsCsvCommand.ExecuteAsync(null);

        Assert.Equal(1, _surveyDataExporter.ExportAccessPointsCsvCallCount);
    }

    [Fact]
    public async Task ExportSurveyJsonCommand_UserPicksFile_CallsExporter()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        using var stream = new MemoryStream();
        _exportFilePickerService.StreamToReturn = stream;

        await sut.ExportSurveyJsonCommand.ExecuteAsync(null);

        Assert.Equal(1, _surveyDataExporter.ExportJsonCallCount);
    }

    [Fact]
    public async Task ExportCoverageReportPdfCommand_UserPicksFile_CallsReportExporter()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        using var stream = new MemoryStream();
        _exportFilePickerService.StreamToReturn = stream;

        await sut.ExportCoverageReportPdfCommand.ExecuteAsync(null);

        Assert.Equal(1, _reportExporter.ExportPdfCallCount);
    }

    [Fact]
    public async Task ExportTestPointsCsvCommand_UserCancelsPicker_DoesNotCallExporter()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        _exportFilePickerService.StreamToReturn = null;

        await sut.ExportTestPointsCsvCommand.ExecuteAsync(null);

        Assert.Equal(0, _surveyDataExporter.ExportTestPointsCsvCallCount);
    }

    [Fact]
    public async Task ExportTestPointsCsvCommand_ExporterThrowsIOException_SetsErrorMessage()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        using var stream = new MemoryStream();
        _exportFilePickerService.StreamToReturn = stream;
        _surveyDataExporter.ExceptionToThrow = new IOException("disk full");

        await sut.ExportTestPointsCsvCommand.ExecuteAsync(null);

        Assert.NotNull(sut.ErrorMessage);
        Assert.True(sut.HasError);
    }

    [Fact]
    public async Task ExportTestPointsCsvCommand_NoSurveyLoaded_DoesNotCallPickerOrExporter()
    {
        var sut = CreateSut();

        await sut.ExportTestPointsCsvCommand.ExecuteAsync(null);

        Assert.Null(_exportFilePickerService.LastSuggestedFileName);
        Assert.Equal(0, _surveyDataExporter.ExportTestPointsCsvCallCount);
    }

    [Fact]
    public async Task AddFloorCommand_AppendsFloorAndSelectsIt()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));
        int floorCountBefore = sut.Floors.Count;

        await sut.AddFloorCommand.ExecuteAsync(("Second Floor", false));

        Assert.Equal(floorCountBefore + 1, sut.Floors.Count);
        Assert.Equal("Second Floor", sut.SelectedFloor!.Name);
        Assert.False(sut.SelectedFloor.IsOutdoor);
        Assert.Same(sut.SelectedFloor, sut.Survey!.Floors[^1]);
    }

    [Fact]
    public async Task AddFloorCommand_Outdoor_SetsOutdoorBounds()
    {
        var sut = await LoadedViewModelAsync(SquareRoomFloor(10));

        await sut.AddFloorCommand.ExecuteAsync(("Parking Lot", true));

        Assert.True(sut.SelectedFloor!.IsOutdoor);
        Assert.NotNull(sut.SelectedFloor.OutdoorBoundsMin);
        Assert.NotNull(sut.SelectedFloor.OutdoorBoundsMax);
    }

    [Fact]
    public async Task SaveSnapshotCommand_AppendsAFrozenCopyOfCurrentFloors()
    {
        var floor = SquareRoomFloor(10);
        floor.TestPoints.Add(new TestPoint { Position = new Point2D(1, 1) });
        var sut = await LoadedViewModelAsync(floor);

        await sut.SaveSnapshotCommand.ExecuteAsync("Before upgrade");

        var snapshot = Assert.Single(sut.Survey!.Snapshots);
        Assert.Equal("Before upgrade", snapshot.Label);
        Assert.Single(snapshot.Floors[0].TestPoints);

        // The snapshot must be a real copy, not a shared reference — a later edit to the live
        // floor should never retroactively change what the snapshot recorded.
        sut.SelectedFloor!.TestPoints.Add(new TestPoint { Position = new Point2D(2, 2) });
        Assert.Single(snapshot.Floors[0].TestPoints);
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

    [Fact]
    public async Task DiagnoseAtPointAsync_NoAdapterSelected_DoesNothing()
    {
        _surveyFileService.SurveyToReturn = BuildSurvey(SquareRoomFloor(10));
        var sut = CreateSut();
        await sut.LoadAsync(FilePath);
        sut.SelectedAdapter = null;

        await sut.DiagnoseAtPointAsync(new Point2D(1, 1));

        Assert.Empty(sut.DiagnoseFindings);
    }

    [Fact]
    public async Task DiagnoseAtPointAsync_Connected_PopulatesPredictionAndFindings()
    {
        var floor = SquareRoomFloor(10);
        floor.AccessPoints.Add(new AccessPoint
        {
            Position = new Point2D(0, 0),
            Label = "AP",
            Radios = { [Band.TwoPointFourGhz] = new BandRadioSettings { TransmitPowerDbm = 17, Channel = 1 } },
        });
        _surveyFileService.SurveyToReturn = BuildSurvey(floor);
        var adapter = new NetworkAdapterInfo(Guid.NewGuid(), "Test Adapter");
        _wlanAdapterService.AdaptersToReturn = [adapter];
        _linkDiagnosticsService.SnapshotToReturn = new LinkDiagnosticsSnapshot(
            IsConnected: true, Ssid: "Net", Bssid: "AA:BB:CC:DD:EE:FF", Band: Band.TwoPointFourGhz, Channel: 1,
            SignalPercent: 90, PhyType: "HT (802.11n)", RxLinkSpeedMbps: 150, TxLinkSpeedMbps: 150);
        _networkHealthService.SnapshotToReturn = new NetworkHealthSnapshot("192.168.1.1", 5, 10, true);
        var sut = CreateSut();
        await sut.LoadAsync(FilePath);

        await sut.DiagnoseAtPointAsync(new Point2D(1, 0));

        Assert.NotNull(sut.DiagnoseSummaryDisplay);
        Assert.NotEmpty(sut.DiagnoseFindings);
    }

    private static Survey BuildSurvey(Floor floor) => new()
    {
        Name = "Test Survey",
        Type = SurveyType.NewDeployment,
        TargetBands = [Band.TwoPointFourGhz],
        Floors = [floor],
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
