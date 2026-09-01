namespace Pulsemap.App.Services;

/// <summary>
/// Backs ILocalizationService.GetString with a hand-rolled string table instead of the WinRT
/// resource APIs that back XAML's x:Uid (which is untouched and works correctly). Every WinRT
/// resource-lookup API tried for C#-code access failed or crashed outright in this unpackaged
/// app, confirmed empirically, not assumed:
///   - Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue crashes
///     natively (Microsoft.UI.Xaml.dll fault, confirmed via Windows Event Log), both mid-session
///     and at pure startup before any window exists.
///   - Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse() crashes the
///     same way.
///   - Microsoft.Windows.ApplicationModel.Resources.ResourceManager.MainResourceMap.GetValue(key)
///     throws COMException 0x80073B17 "NamedResource not found" for keys already proven to
///     resolve fine via x:Uid for that exact same resource - tried with and without a "Resources/"
///     prefix, via the named-.pri constructor, the parameterless constructor, and an absolute
///     path, all identical failures.
///   - A directly-constructed Microsoft.Windows.ApplicationModel.Resources.ResourceContext()
///     appears to fail-fast silently (process exits with no new crash-log entry).
/// Given four independent failures across this whole API family, duplicating this handful of
/// dynamic strings here (instead of chasing a fifth WinRT API) is the safe, working choice - the
/// .resw files stay the source of truth for every XAML-declared string, which is the vast
/// majority of the app's text either way.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private const string DefaultLanguage = "en-US";

    public string CurrentLanguage { get; } = System.Globalization.CultureInfo.CurrentUICulture.Name;

    public string GetString(string key)
    {
        string language = Strings.TryGetValue(CurrentLanguage, out var table) ? CurrentLanguage : DefaultLanguage;
        if (Strings[language].TryGetValue(key, out var value))
        {
            return value;
        }

        // A key present in one language table but missing from another (e.g. a forgotten
        // translation) falls back to the default-language string instead of leaking a raw key.
        return Strings[DefaultLanguage].TryGetValue(key, out var fallback) ? fallback : key;
    }

    private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Strings = new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en-US"] = new Dictionary<string, string>
        {
            ["SettingsCurrentLanguageFormat"] = "Current language: {0}",

            ["WizardSurveyTypeSummaryNewDeployment"] = "New deployment — no access points yet",
            ["WizardSurveyTypeSummaryExistingAuditNoSsid"] = "Existing network audit",
            ["WizardSurveyTypeSummaryExistingAuditWithSsidFormat"] = "Existing network audit — \"{0}\"",
            ["WizardFloorPlanSummaryImageFormat"] = "Image/PDF floor plan ({0})",
            ["WizardFloorPlanSummaryNoFileSelected"] = "no file selected",
            ["WizardFloorPlanSummaryRoomListFormat"] = "Room list — {0} {1}",
            ["WizardRoomWordSingular"] = "room",
            ["WizardRoomWordFew"] = "rooms",
            ["WizardRoomWordMany"] = "rooms",
            ["WizardBandsSummaryNoneSelected"] = "None selected",
            ["WizardSaveSurveyErrorFormat"] = "Couldn't save the survey: {0}",
            ["WizardWallMaterialDrywall.Content"] = "Drywall",
            ["WizardWallMaterialGlassStandard.Content"] = "Glass (standard)",
            ["WizardWallMaterialGlassLowE.Content"] = "Glass (low-E)",
            ["WizardWallMaterialWood.Content"] = "Wood",
            ["WizardWallMaterialBrick.Content"] = "Brick",
            ["WizardWallMaterialConcrete.Content"] = "Concrete",
            ["WizardWallMaterialReinforcedConcrete.Content"] = "Reinforced concrete",
            ["WizardBand24Checkbox.Content"] = "2.4 GHz",
            ["WizardBand5Checkbox.Content"] = "5 GHz",
            ["WizardBand6Checkbox.Content"] = "6 GHz",

            ["WorkspaceCoveragePercentFormat"] = "{0:0}% of the floor at -67dBm or better",
            ["WorkspaceNoAccessPointsPlaced"] = "No access points placed yet.",
            ["WorkspaceChannelAbbreviation"] = "ch",
            ["WorkspaceScanStatusLocationDenied"] = "Windows needs Location access to show WiFi scan results for this app.",
            ["WorkspaceScanStatusNoAdapter"] = "Couldn't reach the WLAN service — is WiFi hardware available and enabled?",
            ["WorkspaceScanStatusFailed"] = "The scan didn't complete. Try again.",
            ["WorkspaceNoNetworksFound"] = "No networks found nearby.",
            ["WorkspaceHiddenNetwork"] = "(hidden network)",
            ["WorkspaceUnknownBand"] = "unknown band",
            ["WorkspaceNetworkSubtitleFormat"] = "{0} · {1}{2} · {3} · {4:0} dBm",
            ["WorkspaceLoadErrorFormat"] = "Couldn't open this survey: {0}",
            ["WorkspaceExportErrorFormat"] = "Couldn't export: {0}",
            ["WorkspaceExportCsvFileType"] = "CSV file",
            ["WorkspaceExportJsonFileType"] = "JSON file",
            ["WorkspaceExportPdfFileType"] = "PDF file",
            ["WorkspaceNoUnmeasuredPoints"] = "No unmeasured points to suggest — draw walls first, or every candidate point already has a nearby test point.",
            ["WorkspaceGuidedWalkCanceled"] = "Guided walk canceled — points captured so far were kept.",
            ["WorkspaceGuidedWalkComplete"] = "Guided walk complete.",
            ["WorkspaceGuidedWalkProgressFormat"] = "Point {0} of {1} — walk to ({2:0.0}m, {3:0.0}m) and confirm.",
            ["WorkspaceGuidedWalkNotWalking"] = "Not walking.",
            ["WorkspaceAccessPointSummaryFormat"] = "{0} — {1}",
        },
        ["pl-PL"] = new Dictionary<string, string>
        {
            ["SettingsCurrentLanguageFormat"] = "Bieżący język: {0}",

            ["WizardSurveyTypeSummaryNewDeployment"] = "Nowe wdrożenie — brak punktów dostępowych",
            ["WizardSurveyTypeSummaryExistingAuditNoSsid"] = "Audyt istniejącej sieci",
            ["WizardSurveyTypeSummaryExistingAuditWithSsidFormat"] = "Audyt istniejącej sieci — „{0}”",
            ["WizardFloorPlanSummaryImageFormat"] = "Plan w postaci obrazu/PDF ({0})",
            ["WizardFloorPlanSummaryNoFileSelected"] = "nie wybrano pliku",
            ["WizardFloorPlanSummaryRoomListFormat"] = "Lista pomieszczeń — {0} {1}",
            ["WizardRoomWordSingular"] = "pomieszczenie",
            ["WizardRoomWordFew"] = "pomieszczenia",
            ["WizardRoomWordMany"] = "pomieszczeń",
            ["WizardBandsSummaryNoneSelected"] = "Brak wybranych",
            ["WizardSaveSurveyErrorFormat"] = "Nie udało się zapisać badania: {0}",
            ["WizardWallMaterialDrywall.Content"] = "Płyta gipsowo-kartonowa",
            ["WizardWallMaterialGlassStandard.Content"] = "Szkło (standardowe)",
            ["WizardWallMaterialGlassLowE.Content"] = "Szkło (niskoemisyjne)",
            ["WizardWallMaterialWood.Content"] = "Drewno",
            ["WizardWallMaterialBrick.Content"] = "Cegła",
            ["WizardWallMaterialConcrete.Content"] = "Beton",
            ["WizardWallMaterialReinforcedConcrete.Content"] = "Żelbet",
            ["WizardBand24Checkbox.Content"] = "2,4 GHz",
            ["WizardBand5Checkbox.Content"] = "5 GHz",
            ["WizardBand6Checkbox.Content"] = "6 GHz",

            ["WorkspaceCoveragePercentFormat"] = "{0:0}% powierzchni przy poziomie -67dBm lub lepszym",
            ["WorkspaceNoAccessPointsPlaced"] = "Nie rozmieszczono jeszcze żadnych punktów dostępowych.",
            ["WorkspaceChannelAbbreviation"] = "kan.",
            ["WorkspaceScanStatusLocationDenied"] = "Windows wymaga dostępu do lokalizacji, aby wyświetlić wyniki skanowania WiFi dla tej aplikacji.",
            ["WorkspaceScanStatusNoAdapter"] = "Nie można połączyć się z usługą WLAN — czy sprzęt WiFi jest dostępny i włączony?",
            ["WorkspaceScanStatusFailed"] = "Skanowanie się nie powiodło. Spróbuj ponownie.",
            ["WorkspaceNoNetworksFound"] = "Nie znaleziono pobliskich sieci.",
            ["WorkspaceHiddenNetwork"] = "(sieć ukryta)",
            ["WorkspaceUnknownBand"] = "nieznane pasmo",
            ["WorkspaceNetworkSubtitleFormat"] = "{0} · {1}{2} · {3} · {4:0} dBm",
            ["WorkspaceLoadErrorFormat"] = "Nie udało się otworzyć badania: {0}",
            ["WorkspaceExportErrorFormat"] = "Nie udało się wyeksportować: {0}",
            ["WorkspaceExportCsvFileType"] = "Plik CSV",
            ["WorkspaceExportJsonFileType"] = "Plik JSON",
            ["WorkspaceExportPdfFileType"] = "Plik PDF",
            ["WorkspaceNoUnmeasuredPoints"] = "Brak punktów do zasugerowania — najpierw narysuj ściany, albo każdy kandydujący punkt ma już pobliski punkt pomiarowy.",
            ["WorkspaceGuidedWalkCanceled"] = "Pomiar terenowy anulowany — zarejestrowane dotychczas punkty zostały zachowane.",
            ["WorkspaceGuidedWalkComplete"] = "Pomiar terenowy zakończony.",
            ["WorkspaceGuidedWalkProgressFormat"] = "Punkt {0} z {1} — podejdź do ({2:0.0}m, {3:0.0}m) i potwierdź.",
            ["WorkspaceGuidedWalkNotWalking"] = "Brak aktywnego pomiaru.",
            ["WorkspaceAccessPointSummaryFormat"] = "{0} — {1}",
        },
    };
}
