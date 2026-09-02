using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Propagation;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class SnapshotComparisonViewModelTests
{
    private const string FilePath = "C:\\FakeSurveys\\Test.pulsemap";

    // Real (not faked) — pure/deterministic, and these tests want to confirm an actual heatmap
    // comes out the other end, not just that some delegate was called.
    private readonly LogDistancePropagationModel _propagationModel = new();
    private readonly FakeSurveyFileService _surveyFileService = new();

    private SnapshotComparisonViewModel CreateSut() => new(_propagationModel, _surveyFileService);

    [Fact]
    public void Initialize_PopulatesOptionsWithCurrentFirstThenSnapshots()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();

        sut.Initialize(survey, FilePath);

        Assert.Equal(2, sut.Options.Count);
        Assert.Null(sut.Options[0].SnapshotId);
        Assert.Equal("Before upgrade", sut.Options[1].Label);
    }

    [Fact]
    public void Initialize_DefaultsBothSidesToCurrentAndSelectsFirstFloorAndBand()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();

        sut.Initialize(survey, FilePath);

        Assert.Same(sut.Options[0], sut.LeftOption);
        Assert.Same(sut.Options[0], sut.RightOption);
        Assert.Equal(survey.Floors[0].Id, sut.SelectedFloor!.Id);
        Assert.Equal(Band.TwoPointFourGhz, sut.SelectedBand);
    }

    [Fact]
    public void SwitchingRightOptionToASnapshot_ResolvesTheSnapshotsOwnFloorAndRecomputesItsHeatmap()
    {
        // The snapshot was frozen with no access points, unlike the live floor - so its heatmap
        // must come out empty even though the live ("Current") side has real coverage.
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();
        sut.Initialize(survey, FilePath);
        Assert.NotEmpty(sut.RightHeatmap);

        sut.RightOption = sut.Options[1];

        Assert.Equal(survey.Snapshots[0].Floors[0].Id, sut.RightFloor!.Id);
        Assert.Empty(sut.RightHeatmap);
        Assert.NotEmpty(sut.LeftHeatmap);
    }

    [Fact]
    public void Recompute_FiresChangedEvent()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();
        sut.Initialize(survey, FilePath);
        int changedCount = 0;
        sut.Changed += (_, _) => changedCount++;

        sut.RightOption = sut.Options[1];

        Assert.Equal(1, changedCount);
    }

    [Fact]
    public void CanDeleteLeftSnapshot_CurrentSelected_IsFalse()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();
        sut.Initialize(survey, FilePath);

        Assert.False(sut.CanDeleteLeftSnapshot);
    }

    [Fact]
    public void CanDeleteLeftSnapshot_SnapshotSelected_IsTrue()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();
        sut.Initialize(survey, FilePath);

        sut.LeftOption = sut.Options[1];

        Assert.True(sut.CanDeleteLeftSnapshot);
    }

    [Fact]
    public async Task DeleteLeftSnapshotCommand_RemovesSnapshotAndSaves()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        var sut = CreateSut();
        sut.Initialize(survey, FilePath);
        sut.LeftOption = sut.Options[1];

        await sut.DeleteLeftSnapshotCommand.ExecuteAsync(null);

        Assert.Empty(survey.Snapshots);
        Assert.Single(sut.Options);
        Assert.Same(sut.Options[0], sut.LeftOption);
        Assert.Single(_surveyFileService.SaveCalls);
    }

    [Fact]
    public async Task DeleteLeftSnapshotCommand_RightSideKeepsItsOwnSelection()
    {
        var survey = BuildSurvey(withAccessPoint: true, snapshotLabel: "Before upgrade");
        survey.Snapshots.Add(new SurveySnapshot { Label = "Another", Floors = survey.Snapshots[0].Floors });
        var sut = CreateSut();
        sut.Initialize(survey, FilePath);
        sut.LeftOption = sut.Options[1];
        sut.RightOption = sut.Options[2];

        await sut.DeleteLeftSnapshotCommand.ExecuteAsync(null);

        Assert.Equal("Another", sut.RightOption?.Label);
    }

    private static Survey BuildSurvey(bool withAccessPoint, string snapshotLabel)
    {
        var liveFloor = SquareRoomFloor(10);
        if (withAccessPoint)
        {
            var accessPoint = new AccessPoint { Position = new Point2D(5, 5), Label = "AP 1" };
            accessPoint.Radios[Band.TwoPointFourGhz] = new BandRadioSettings { TransmitPowerDbm = 17, Channel = 1 };
            liveFloor.AccessPoints.Add(accessPoint);
        }

        var snapshotFloor = SquareRoomFloor(10, id: liveFloor.Id);

        return new Survey
        {
            Name = "Test Survey",
            Type = SurveyType.NewDeployment,
            TargetBands = [Band.TwoPointFourGhz],
            Floors = [liveFloor],
            Snapshots = [new SurveySnapshot { Label = snapshotLabel, Floors = [snapshotFloor] }],
        };
    }

    private static Floor SquareRoomFloor(double sizeMeters, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
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
