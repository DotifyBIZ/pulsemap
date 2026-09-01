using Pulsemap.App.Core.Models;
using Pulsemap.App.Services;
using Pulsemap.App.Tests.Fakes;
using Pulsemap.App.ViewModels;

namespace Pulsemap.App.Tests.ViewModels;

public sealed class NewSurveyWizardViewModelTests : IDisposable
{
    private readonly FakeSurveyFileService _surveyFileService = new();
    private readonly FakeSurveyLibraryService _surveyLibraryService = new();
    private readonly FakeFloorPlanFilePickerService _filePickerService = new();
    private readonly FakeLocalizationService _localizationService = new();
    private readonly FakeAppLogger _logger = new();
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "PulsemapTests", Guid.NewGuid().ToString());

    public NewSurveyWizardViewModelTests() => _surveyLibraryService.SurveysDirectory = _tempDirectory;

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private NewSurveyWizardViewModel CreateSut() => new(_surveyFileService, _surveyLibraryService, _filePickerService, _localizationService, _logger);

    [Fact]
    public void NextCommand_CanExecute_FalseWhenSurveyNameIsEmpty()
    {
        var sut = CreateSut();

        Assert.False(sut.NextCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_CanExecute_TrueOnceSurveyNameIsSet()
    {
        var sut = CreateSut();

        sut.SurveyName = "Riverside Site";

        Assert.True(sut.NextCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_Execute_AdvancesStepAndUpdatesVisibility()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";

        sut.NextCommand.Execute(null);

        Assert.Equal(1, sut.CurrentStepIndex);
        Assert.False(sut.IsBasicsStepVisible);
        Assert.True(sut.IsFloorPlanStepVisible);
    }

    [Fact]
    public void NextCommand_CanExecute_RoomListStepRequiresAtLeastOneRoom()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.NextCommand.Execute(null);

        Assert.False(sut.NextCommand.CanExecute(null));

        sut.AddRoomCommand.Execute(null);

        Assert.True(sut.NextCommand.CanExecute(null));
    }

    [Fact]
    public void NextCommand_CanExecute_ImageStepRequiresSelectedImage()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.NextCommand.Execute(null);
        sut.SelectedFloorPlanStyle = FloorPlanStyleChoice.Image;

        Assert.False(sut.NextCommand.CanExecute(null));
    }

    [Fact]
    public void BackCommand_CanExecute_FalseOnFirstStep()
    {
        var sut = CreateSut();

        Assert.False(sut.BackCommand.CanExecute(null));
    }

    [Fact]
    public void BackCommand_Execute_ReturnsToPreviousStep()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.NextCommand.Execute(null);

        sut.BackCommand.Execute(null);

        Assert.Equal(0, sut.CurrentStepIndex);
    }

    [Fact]
    public void AddRoomCommand_AddsRoomAndUpdatesSummary()
    {
        var sut = CreateSut();

        sut.AddRoomCommand.Execute(null);

        Assert.Single(sut.Rooms);
        Assert.Equal("WizardFloorPlanSummaryRoomListFormat", sut.FloorPlanSummaryDisplay);
    }

    [Fact]
    public void RemoveRoomCommand_RemovesRoom()
    {
        var sut = CreateSut();
        sut.AddRoomCommand.Execute(null);
        var room = sut.Rooms[0];

        sut.RemoveRoomCommand.Execute(room);

        Assert.Empty(sut.Rooms);
    }

    [Fact]
    public void SurveyTypeSummaryDisplay_NewDeployment_UsesNewDeploymentKey()
    {
        var sut = CreateSut();

        Assert.Equal("WizardSurveyTypeSummaryNewDeployment", sut.SurveyTypeSummaryDisplay);
    }

    [Fact]
    public void SurveyTypeSummaryDisplay_ExistingAuditWithSsid_UsesWithSsidFormatKey()
    {
        var sut = CreateSut();
        sut.SelectedSurveyType = SurveyType.ExistingNetworkAudit;
        sut.TargetNetworkSsid = "OfficeNet";

        Assert.Equal("WizardSurveyTypeSummaryExistingAuditWithSsidFormat", sut.SurveyTypeSummaryDisplay);
    }

    [Fact]
    public void SurveyTypeSummaryDisplay_ExistingAuditWithoutSsid_UsesNoSsidKey()
    {
        var sut = CreateSut();
        sut.SelectedSurveyType = SurveyType.ExistingNetworkAudit;

        Assert.Equal("WizardSurveyTypeSummaryExistingAuditNoSsid", sut.SurveyTypeSummaryDisplay);
    }

    [Fact]
    public async Task CreateSurveyAsync_RoomListStyle_BuildsPerimeterWallsAndSaves()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.AddRoomCommand.Execute(null);

        await sut.CreateSurveyCommand.ExecuteAsync(null);

        Assert.Single(_surveyFileService.SaveCalls);
        var savedSurvey = _surveyFileService.SaveCalls[0].Survey;
        Assert.Equal("Riverside Site", savedSurvey.Name);
        Assert.Equal(4, savedSurvey.Floor.Walls.Count);
    }

    [Fact]
    public async Task CreateSurveyAsync_ExistingAuditWithSsid_SetsTargetNetworkSsidOnSurvey()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.SelectedSurveyType = SurveyType.ExistingNetworkAudit;
        sut.TargetNetworkSsid = "  OfficeNet  ";
        sut.AddRoomCommand.Execute(null);

        await sut.CreateSurveyCommand.ExecuteAsync(null);

        Assert.Equal("OfficeNet", _surveyFileService.SaveCalls[0].Survey.TargetNetworkSsid);
    }

    [Fact]
    public async Task CreateSurveyAsync_NewDeployment_TargetNetworkSsidIsNull()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.AddRoomCommand.Execute(null);

        await sut.CreateSurveyCommand.ExecuteAsync(null);

        Assert.Null(_surveyFileService.SaveCalls[0].Survey.TargetNetworkSsid);
    }

    [Fact]
    public async Task CreateSurveyAsync_RaisesSurveyCreatedWithFilePath()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.AddRoomCommand.Execute(null);
        string? raisedFilePath = null;
        sut.SurveyCreated += (_, filePath) => raisedFilePath = filePath;

        await sut.CreateSurveyCommand.ExecuteAsync(null);

        Assert.NotNull(raisedFilePath);
        Assert.EndsWith(".pulsemap", raisedFilePath);
    }

    [Fact]
    public async Task CreateSurveyAsync_SaveThrows_SetsErrorMessageAndDoesNotRaiseSurveyCreated()
    {
        var sut = CreateSut();
        sut.SurveyName = "Riverside Site";
        sut.AddRoomCommand.Execute(null);
        _surveyFileService.LoadExceptionToThrow = null;
        bool raised = false;
        sut.SurveyCreated += (_, _) => raised = true;

        // Point the library at a location Directory.CreateDirectory cannot create, forcing
        // CreateSurveyAsync's real filesystem call to throw the exception it's meant to catch.
        _surveyLibraryService.SurveysDirectory = "Z:\\definitely\\not\\a\\real\\drive";

        await sut.CreateSurveyCommand.ExecuteAsync(null);

        Assert.True(sut.HasError);
        Assert.False(raised);
    }

    [Fact]
    public async Task PickImageCommand_UserSelectsFile_SetsSelectedImageFileName()
    {
        var sut = CreateSut();
        _filePickerService.ResultToReturn = new FloorPlanFilePickResult("floorplan.png", ".png", [1, 2, 3]);

        await sut.PickImageCommand.ExecuteAsync(null);

        Assert.Equal("floorplan.png", sut.SelectedImageFileName);
    }

    [Fact]
    public async Task PickImageCommand_UserCancels_LeavesSelectedImageFileNameNull()
    {
        var sut = CreateSut();
        _filePickerService.ResultToReturn = null;

        await sut.PickImageCommand.ExecuteAsync(null);

        Assert.Null(sut.SelectedImageFileName);
    }
}
