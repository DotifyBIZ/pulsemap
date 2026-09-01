using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsemap.App.Core.Models;
using Pulsemap.App.Core.Persistence;
using Pulsemap.App.Services;

namespace Pulsemap.App.ViewModels;

/// <summary>
/// Drives the 5-step New Survey wizard (Basics -> Floor Plan -> Building Details -> Adapter &amp;
/// Bands -> Review). One view model for all five steps rather than one per step — the steps share
/// a single linear flow and a single "create the survey" outcome, so splitting them would only add
/// cross-view-model plumbing without buying any real isolation.
/// </summary>
public partial class NewSurveyWizardViewModel : ObservableObject
{
    private const double DefaultPixelsPerMeter = 100;
    private const double NewRoomSizeMeters = 5;
    private const double RoomLayoutGapMeters = 1;

    private readonly ISurveyFileService _surveyFileService;
    private readonly ISurveyLibraryService _surveyLibraryService;
    private readonly IFloorPlanFilePickerService _filePickerService;
    private readonly ILocalizationService _localizationService;

    private byte[]? _pickedImageData;
    private string? _pickedImageExtension;

    public NewSurveyWizardViewModel(
        ISurveyFileService surveyFileService,
        ISurveyLibraryService surveyLibraryService,
        IFloorPlanFilePickerService filePickerService,
        ILocalizationService localizationService)
    {
        _surveyFileService = surveyFileService;
        _surveyLibraryService = surveyLibraryService;
        _filePickerService = filePickerService;
        _localizationService = localizationService;

        Rooms.CollectionChanged += (_, _) =>
        {
            NextCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(FloorPlanSummaryDisplay));
        };
    }

    public event EventHandler<string>? SurveyCreated;

    public ObservableCollection<RoomListEntry> Rooms { get; } = [];

    [ObservableProperty]
    public partial int CurrentStepIndex { get; set; }

    // Step 1 — Basics
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    public partial string SurveyName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SiteDescription { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SurveyTypeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(IsExistingNetworkAuditSelected))]
    public partial SurveyType SelectedSurveyType { get; set; } = SurveyType.NewDeployment;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SurveyTypeSummaryDisplay))]
    public partial string TargetNetworkSsid { get; set; } = string.Empty;

    public bool IsExistingNetworkAuditSelected => SelectedSurveyType == SurveyType.ExistingNetworkAudit;

    // Step 2 — Floor plan
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(FloorPlanSummaryDisplay))]
    public partial FloorPlanStyleChoice SelectedFloorPlanStyle { get; set; } = FloorPlanStyleChoice.RoomList;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(FloorPlanSummaryDisplay))]
    public partial string? SelectedImageFileName { get; set; }

    [ObservableProperty]
    public partial double PixelsPerMeter { get; set; } = DefaultPixelsPerMeter;

    public bool IsImageStyleSelected => SelectedFloorPlanStyle == FloorPlanStyleChoice.Image;

    public bool IsRoomListStyleSelected => SelectedFloorPlanStyle == FloorPlanStyleChoice.RoomList;

    // Step 3 — Building details
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DefaultWallMaterialDisplay))]
    public partial WallMaterial DefaultWallMaterial { get; set; } = WallMaterial.Drywall;

    // Step 4 — Adapter & bands
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(BandsSummaryDisplay))]
    public partial bool Includes24Ghz { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(BandsSummaryDisplay))]
    public partial bool Includes5Ghz { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyPropertyChangedFor(nameof(BandsSummaryDisplay))]
    public partial bool Includes6Ghz { get; set; }

    // Step 5 — Review
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateSurveyCommand))]
    public partial bool IsCreating { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => ErrorMessage is not null;

    public bool IsBasicsStepVisible => CurrentStepIndex == 0;

    public bool IsFloorPlanStepVisible => CurrentStepIndex == 1;

    public bool IsBuildingDetailsStepVisible => CurrentStepIndex == 2;

    public bool IsAdapterBandsStepVisible => CurrentStepIndex == 3;

    public bool IsReviewStepVisible => CurrentStepIndex == 4;

    public bool IsNotReviewStep => !IsReviewStepVisible;

    public string SurveyTypeSummaryDisplay => SelectedSurveyType == SurveyType.NewDeployment
        ? _localizationService.GetString("WizardSurveyTypeSummaryNewDeployment")
        : string.IsNullOrWhiteSpace(TargetNetworkSsid)
            ? _localizationService.GetString("WizardSurveyTypeSummaryExistingAuditNoSsid")
            : string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WizardSurveyTypeSummaryExistingAuditWithSsidFormat"), TargetNetworkSsid.Trim());

    public string FloorPlanSummaryDisplay => IsImageStyleSelected
        ? string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WizardFloorPlanSummaryImageFormat"), SelectedImageFileName ?? _localizationService.GetString("WizardFloorPlanSummaryNoFileSelected"))
        : string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WizardFloorPlanSummaryRoomListFormat"), Rooms.Count, _localizationService.GetString(RoomCountWordKey(Rooms.Count)));

    // Polish needs three plural forms (1 / 2-4 / 5+, with 11-14 folding into the "many" form even
    // when the last digit would otherwise say "few") — English only has two, so its "few" and
    // "many" resource entries are identical and this collapses to the usual singular/plural check.
    private static string RoomCountWordKey(int count) => count switch
    {
        1 => "WizardRoomWordSingular",
        _ when count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 => "WizardRoomWordFew",
        _ => "WizardRoomWordMany",
    };

    public string DefaultWallMaterialDisplay => DefaultWallMaterial switch
    {
        WallMaterial.Drywall => _localizationService.GetString("WizardWallMaterialDrywall.Content"),
        WallMaterial.GlassStandard => _localizationService.GetString("WizardWallMaterialGlassStandard.Content"),
        WallMaterial.GlassLowE => _localizationService.GetString("WizardWallMaterialGlassLowE.Content"),
        WallMaterial.Wood => _localizationService.GetString("WizardWallMaterialWood.Content"),
        WallMaterial.Brick => _localizationService.GetString("WizardWallMaterialBrick.Content"),
        WallMaterial.Concrete => _localizationService.GetString("WizardWallMaterialConcrete.Content"),
        WallMaterial.ReinforcedConcrete => _localizationService.GetString("WizardWallMaterialReinforcedConcrete.Content"),
        _ => DefaultWallMaterial.ToString(),
    };

    public string BandsSummaryDisplay
    {
        get
        {
            var parts = new List<string>();
            if (Includes24Ghz)
            {
                parts.Add(_localizationService.GetString("WizardBand24Checkbox.Content"));
            }

            if (Includes5Ghz)
            {
                parts.Add(_localizationService.GetString("WizardBand5Checkbox.Content"));
            }

            if (Includes6Ghz)
            {
                parts.Add(_localizationService.GetString("WizardBand6Checkbox.Content"));
            }

            return parts.Count == 0 ? _localizationService.GetString("WizardBandsSummaryNoneSelected") : string.Join(", ", parts);
        }
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsBasicsStepVisible));
        OnPropertyChanged(nameof(IsFloorPlanStepVisible));
        OnPropertyChanged(nameof(IsBuildingDetailsStepVisible));
        OnPropertyChanged(nameof(IsAdapterBandsStepVisible));
        OnPropertyChanged(nameof(IsReviewStepVisible));
        OnPropertyChanged(nameof(IsNotReviewStep));
        NextCommand.NotifyCanExecuteChanged();
        BackCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFloorPlanStyleChanged(FloorPlanStyleChoice value)
    {
        OnPropertyChanged(nameof(IsImageStyleSelected));
        OnPropertyChanged(nameof(IsRoomListStyleSelected));
    }

    [RelayCommand]
    private async Task PickImageAsync()
    {
        var result = await _filePickerService.PickFloorPlanFileAsync();
        if (result is null)
        {
            return;
        }

        _pickedImageData = result.ImageData;
        _pickedImageExtension = result.FileExtension;
        SelectedImageFileName = result.FileName;
    }

    [RelayCommand]
    private void AddRoom() =>
        Rooms.Add(new RoomListEntry { Name = $"Room {Rooms.Count + 1}", WidthMeters = NewRoomSizeMeters, LengthMeters = NewRoomSizeMeters });

    [RelayCommand]
    private void RemoveRoom(RoomListEntry room)
    {
        ArgumentNullException.ThrowIfNull(room);
        Rooms.Remove(room);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (CurrentStepIndex < 4)
        {
            CurrentStepIndex++;
        }
    }

    private bool CanGoNext() => CurrentStepIndex switch
    {
        0 => !string.IsNullOrWhiteSpace(SurveyName),
        1 => IsImageStyleSelected ? SelectedImageFileName is not null : Rooms.Count > 0,
        3 => Includes24Ghz || Includes5Ghz || Includes6Ghz,
        _ => true,
    };

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    private bool CanGoBack() => CurrentStepIndex > 0;

    private bool CanCreateSurvey() => !IsCreating;

    [RelayCommand(CanExecute = nameof(CanCreateSurvey))]
    private async Task CreateSurveyAsync()
    {
        ErrorMessage = null;
        IsCreating = true;
        try
        {
            var survey = BuildSurvey();
            Directory.CreateDirectory(_surveyLibraryService.SurveysDirectory);
            string filePath = UniqueFilePath(_surveyLibraryService.SurveysDirectory, survey.Name);
            await _surveyFileService.SaveAsync(survey, filePath);
            SurveyCreated?.Invoke(this, filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorMessage = string.Format(CultureInfo.CurrentCulture, _localizationService.GetString("WizardSaveSurveyErrorFormat"), ex.Message);
        }
        finally
        {
            IsCreating = false;
        }
    }

    private Survey BuildSurvey()
    {
        var bands = new List<Band>();
        if (Includes24Ghz)
        {
            bands.Add(Band.TwoPointFourGhz);
        }

        if (Includes5Ghz)
        {
            bands.Add(Band.FiveGhz);
        }

        if (Includes6Ghz)
        {
            bands.Add(Band.SixGhz);
        }

        FloorPlanSource planSource = IsImageStyleSelected
            ? new ImagePlanSource
            {
                ImageData = _pickedImageData ?? [],
                FileExtension = _pickedImageExtension ?? string.Empty,
                PixelsPerMeter = PixelsPerMeter,
            }
            : new RoomListSource { Rooms = [.. Rooms] };

        var floor = new Floor { PlanSource = planSource };
        if (IsRoomListStyleSelected)
        {
            floor.Walls.AddRange(BuildPerimeterWalls(Rooms, DefaultWallMaterial));
        }

        return new Survey
        {
            Name = SurveyName.Trim(),
            SiteDescription = string.IsNullOrWhiteSpace(SiteDescription) ? null : SiteDescription.Trim(),
            Type = SelectedSurveyType,
            TargetNetworkSsid = IsExistingNetworkAuditSelected && !string.IsNullOrWhiteSpace(TargetNetworkSsid) ? TargetNetworkSsid.Trim() : null,
            TargetBands = bands,
            Floor = floor,
        };
    }

    private static List<Wall> BuildPerimeterWalls(IEnumerable<RoomListEntry> rooms, WallMaterial material)
    {
        var walls = new List<Wall>();
        double offsetX = 0;

        foreach (var room in rooms)
        {
            double x0 = offsetX;
            double x1 = offsetX + room.WidthMeters;
            const double y0 = 0;
            double y1 = room.LengthMeters;

            walls.Add(new Wall { Start = new Point2D(x0, y0), End = new Point2D(x1, y0), Material = material });
            walls.Add(new Wall { Start = new Point2D(x1, y0), End = new Point2D(x1, y1), Material = material });
            walls.Add(new Wall { Start = new Point2D(x1, y1), End = new Point2D(x0, y1), Material = material });
            walls.Add(new Wall { Start = new Point2D(x0, y1), End = new Point2D(x0, y0), Material = material });

            offsetX = x1 + RoomLayoutGapMeters;
        }

        return walls;
    }

    private static string UniqueFilePath(string directory, string surveyName)
    {
        string baseName = string.Concat(surveyName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string candidate = Path.Combine(directory, $"{baseName}.pulsemap");
        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{baseName} ({suffix}).pulsemap");
            suffix++;
        }

        return candidate;
    }
}
