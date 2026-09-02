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

            ["SurveysRenameDialogTitle"] = "Rename survey",
            ["SurveysRenameDialogPrimaryButton"] = "Rename",
            ["SurveysRenameDialogCloseButton"] = "Cancel",
            ["SurveysDeleteDialogTitle"] = "Delete survey",
            ["SurveysDeleteDialogContentFormat"] = "Delete \"{0}\"? This can't be undone.",
            ["SurveysDeleteDialogPrimaryButton"] = "Delete",
            ["SurveysDeleteDialogCloseButton"] = "Cancel",
            ["SurveysDeleteErrorFormat"] = "Couldn't delete: {0}",
            ["SurveysRenameErrorFormat"] = "Couldn't rename: {0}",

            ["WorkspaceAddFloorDialogTitle"] = "Add floor or area",
            ["WorkspaceAddFloorDialogNameLabel"] = "Name",
            ["WorkspaceAddFloorDialogOutdoorLabel"] = "Outdoor area (no walls)",
            ["WorkspaceAddFloorDialogPrimaryButton"] = "Add",
            ["WorkspaceAddFloorDialogCloseButton"] = "Cancel",
            ["WorkspaceSaveSnapshotDialogTitle"] = "Save snapshot",
            ["WorkspaceSaveSnapshotDialogLabelHeader"] = "Label",
            ["WorkspaceSaveSnapshotDialogPrimaryButton"] = "Save",
            ["WorkspaceSaveSnapshotDialogCloseButton"] = "Cancel",
            ["WorkspaceWallSelectionCountFormat"] = "Selected walls: {0}",
            ["WorkspaceRecaptureDialogTitle"] = "Recapture this point?",
            ["WorkspaceRecaptureDialogContent"] = "This replaces its existing reading with a new scan from here.",
            ["WorkspaceRecaptureDialogPrimaryButton"] = "Recapture",
            ["WorkspaceRecaptureDialogCloseButton"] = "Cancel",
            ["WorkspaceUndoDeleteWallMessage"] = "Wall deleted.",
            ["WorkspaceUndoDeleteTestPointMessage"] = "Test point deleted.",
            ["WorkspaceUndoDeleteAccessPointMessage"] = "Access point deleted.",
            ["WorkspaceDiagnosePredictedFormat"] = "Survey predicted {0:0.0} dBm here.",
            ["WorkspaceDiagnoseNoPredictionDisplay"] = "No access points on this band reach this point in the survey model.",

            ["HomeUpdateAvailableFormat"] = "Pulsemap {0} is available.",
            ["HomeGreetingMorning"] = "Good morning",
            ["HomeGreetingAfternoon"] = "Good afternoon",
            ["HomeGreetingEvening"] = "Good evening",
            ["HomeGreetingNight"] = "Hello",
            ["HomeWellbeingMessage1"] = "Hope you're doing well today.",
            ["HomeWellbeingMessage2"] = "Take a moment for yourself if you need one.",
            ["HomeWellbeingMessage3"] = "However today's going, we hope it gets a little easier.",
            ["HomeWellbeingMessage4"] = "Glad you're here. Take care of yourself out there.",
            ["HomeWellbeingMessage5"] = "Remember to drink some water and stretch your legs.",
            ["HomeWellbeingMessage6"] = "No rush today. One thing at a time.",
            ["HomeWellbeingMessage7"] = "Hope something good happens for you today.",
            ["HomeWellbeingMessage8"] = "You're doing better than you think.",

            ["DiagnosticNotConnected"] = "This adapter isn't connected to a network.",
            ["DiagnosticNoIssuesFound"] = "No issues found — link and network health both look healthy.",
            ["DiagnosticWeakSignal"] = "Signal is weak ({0}%) — expect reduced range and occasional slowdowns.",
            ["DiagnosticVeryWeakSignal"] = "Signal is very weak ({0}%) — this is likely limiting your speed significantly.",
            ["DiagnosticLegacyPhyRate"] = "Connected at only {0:0} Mbps on 5/6GHz — this looks like a fallback to a legacy rate rather than a real 802.11n/ac/ax/be link.",
            ["DiagnosticDnsFailed"] = "DNS lookups are failing — this can look like \"no internet\" even with a strong WiFi link.",
            ["DiagnosticSlowDns"] = "DNS lookups are slow ({0:0} ms) — pages may feel slow to start loading even with fast WiFi.",
            ["DiagnosticHighGatewayPing"] = "Ping to your router is high ({0:0} ms) — this points to a problem on the local network, not necessarily your internet connection.",
            ["DiagnosticGatewayUnreachable"] = "Your router didn't respond to a ping — it may be busy, blocking ping, or there's a local network problem.",
            ["DiagnosticPredictedVsActualMismatch"] = "The actual signal here ({1:0.0} dBm) is notably weaker than the survey predicted ({0:0.0} dBm) — the environment may have changed since the survey.",

            ["DiagnosticsStartMonitoringButton"] = "Start monitoring",
            ["DiagnosticsStopMonitoringButton"] = "Stop monitoring",
            ["DiagnosticsNotConnectedDisplay"] = "Not connected",
            ["DiagnosticsBandAndChannelFormat"] = "{0}, channel {1}",
            ["DiagnosticsUnknownBandDisplay"] = "unknown band",
            ["DiagnosticsLinkSummaryFormat"] = "{0}% signal · {1} · Rx {2:0} Mbps · Tx {3:0} Mbps",
            ["DiagnosticsNetworkHealthSummaryFormat"] = "Gateway {0} · {1} · DNS {2}",
            ["DiagnosticsPingMsFormat"] = "{0:0} ms ping",
            ["DiagnosticsNoPingReplyDisplay"] = "no ping reply",
            ["DiagnosticsDnsMsFormat"] = "{0:0} ms",
            ["DiagnosticsDnsFailedDisplay"] = "failed",
            ["DiagnosticsMbpsFormat"] = "{0:0} Mbps",
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

            ["SurveysRenameDialogTitle"] = "Zmień nazwę badania",
            ["SurveysRenameDialogPrimaryButton"] = "Zmień nazwę",
            ["SurveysRenameDialogCloseButton"] = "Anuluj",
            ["SurveysDeleteDialogTitle"] = "Usuń badanie",
            ["SurveysDeleteDialogContentFormat"] = "Usunąć „{0}”? Tej operacji nie można cofnąć.",
            ["SurveysDeleteDialogPrimaryButton"] = "Usuń",
            ["SurveysDeleteDialogCloseButton"] = "Anuluj",
            ["SurveysDeleteErrorFormat"] = "Nie udało się usunąć: {0}",
            ["SurveysRenameErrorFormat"] = "Nie udało się zmienić nazwy: {0}",

            ["WorkspaceAddFloorDialogTitle"] = "Dodaj piętro lub obszar",
            ["WorkspaceAddFloorDialogNameLabel"] = "Nazwa",
            ["WorkspaceAddFloorDialogOutdoorLabel"] = "Obszar zewnętrzny (bez ścian)",
            ["WorkspaceAddFloorDialogPrimaryButton"] = "Dodaj",
            ["WorkspaceAddFloorDialogCloseButton"] = "Anuluj",
            ["WorkspaceSaveSnapshotDialogTitle"] = "Zapisz migawkę",
            ["WorkspaceSaveSnapshotDialogLabelHeader"] = "Etykieta",
            ["WorkspaceSaveSnapshotDialogPrimaryButton"] = "Zapisz",
            ["WorkspaceSaveSnapshotDialogCloseButton"] = "Anuluj",
            ["WorkspaceWallSelectionCountFormat"] = "Zaznaczone ściany: {0}",
            ["WorkspaceRecaptureDialogTitle"] = "Zarejestrować ten punkt ponownie?",
            ["WorkspaceRecaptureDialogContent"] = "Zastąpi to jego dotychczasowy odczyt nowym skanem z tego miejsca.",
            ["WorkspaceRecaptureDialogPrimaryButton"] = "Zarejestruj ponownie",
            ["WorkspaceRecaptureDialogCloseButton"] = "Anuluj",
            ["WorkspaceUndoDeleteWallMessage"] = "Usunięto ścianę.",
            ["WorkspaceUndoDeleteTestPointMessage"] = "Usunięto punkt pomiarowy.",
            ["WorkspaceUndoDeleteAccessPointMessage"] = "Usunięto punkt dostępowy.",
            ["WorkspaceDiagnosePredictedFormat"] = "Pomiar przewidywał tutaj {0:0.0} dBm.",
            ["WorkspaceDiagnoseNoPredictionDisplay"] = "Żaden punkt dostępowy na tym paśmie nie dociera do tego miejsca w modelu pomiaru.",

            ["HomeUpdateAvailableFormat"] = "Dostępny jest Pulsemap {0}.",
            ["HomeGreetingMorning"] = "Dzień dobry",
            ["HomeGreetingAfternoon"] = "Dzień dobry",
            ["HomeGreetingEvening"] = "Dobry wieczór",
            ["HomeGreetingNight"] = "Witaj",
            ["HomeWellbeingMessage1"] = "Mamy nadzieję, że wszystko u Ciebie dobrze.",
            ["HomeWellbeingMessage2"] = "Jeśli potrzebujesz chwili dla siebie, zrób sobie przerwę.",
            ["HomeWellbeingMessage3"] = "Niezależnie od tego, jak mija dzisiejszy dzień, mamy nadzieję, że będzie choć trochę lżej.",
            ["HomeWellbeingMessage4"] = "Miło, że tu jesteś. Zadbaj o siebie.",
            ["HomeWellbeingMessage5"] = "Pamiętaj, żeby napić się wody i trochę się rozciągnąć.",
            ["HomeWellbeingMessage6"] = "Bez pośpiechu. Jedna rzecz na raz.",
            ["HomeWellbeingMessage7"] = "Mamy nadzieję, że dzisiaj wydarzy się coś dobrego.",
            ["HomeWellbeingMessage8"] = "Radzisz sobie lepiej, niż Ci się wydaje.",

            ["DiagnosticNotConnected"] = "Ten adapter nie jest połączony z żadną siecią.",
            ["DiagnosticNoIssuesFound"] = "Nie znaleziono problemów — łącze i stan sieci wyglądają dobrze.",
            ["DiagnosticWeakSignal"] = "Słaby sygnał ({0}%) — spodziewaj się mniejszego zasięgu i okazjonalnych spowolnień.",
            ["DiagnosticVeryWeakSignal"] = "Bardzo słaby sygnał ({0}%) — to prawdopodobnie znacząco ogranicza prędkość.",
            ["DiagnosticLegacyPhyRate"] = "Połączenie z prędkością tylko {0:0} Mb/s na 5/6GHz — wygląda na przejście na starszy tryb transmisji zamiast prawdziwego łącza 802.11n/ac/ax/be.",
            ["DiagnosticDnsFailed"] = "Zapytania DNS kończą się niepowodzeniem — może to wyglądać jak „brak internetu”, mimo silnego sygnału WiFi.",
            ["DiagnosticSlowDns"] = "Zapytania DNS są wolne ({0:0} ms) — strony mogą wydawać się wolno wczytywać nawet przy szybkim WiFi.",
            ["DiagnosticHighGatewayPing"] = "Wysoki ping do routera ({0:0} ms) — wskazuje to na problem w sieci lokalnej, niekoniecznie z połączeniem internetowym.",
            ["DiagnosticGatewayUnreachable"] = "Router nie odpowiedział na ping — może być zajęty, blokować ping, albo wystąpił problem w sieci lokalnej.",
            ["DiagnosticPredictedVsActualMismatch"] = "Rzeczywisty sygnał w tym miejscu ({1:0.0} dBm) jest zauważalnie słabszy niż przewidywał pomiar ({0:0.0} dBm) — otoczenie mogło się zmienić od czasu pomiaru.",

            ["DiagnosticsStartMonitoringButton"] = "Rozpocznij monitorowanie",
            ["DiagnosticsStopMonitoringButton"] = "Zatrzymaj monitorowanie",
            ["DiagnosticsNotConnectedDisplay"] = "Niepołączono",
            ["DiagnosticsBandAndChannelFormat"] = "{0}, kanał {1}",
            ["DiagnosticsUnknownBandDisplay"] = "nieznane pasmo",
            ["DiagnosticsLinkSummaryFormat"] = "Sygnał {0}% · {1} · Rx {2:0} Mb/s · Tx {3:0} Mb/s",
            ["DiagnosticsNetworkHealthSummaryFormat"] = "Router {0} · {1} · DNS {2}",
            ["DiagnosticsPingMsFormat"] = "ping {0:0} ms",
            ["DiagnosticsNoPingReplyDisplay"] = "brak odpowiedzi",
            ["DiagnosticsDnsMsFormat"] = "{0:0} ms",
            ["DiagnosticsDnsFailedDisplay"] = "niepowodzenie",
            ["DiagnosticsMbpsFormat"] = "{0:0} Mb/s",
        },
    };
}
