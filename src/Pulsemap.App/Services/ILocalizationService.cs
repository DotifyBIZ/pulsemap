namespace Pulsemap.App.Services;

/// <summary>
/// Looks up localized strings for code that can't use XAML's automatic x:Uid resolution (dynamic
/// ViewModel-built text). Pulsemap follows Windows' own display language setting rather than
/// offering an in-app override — see the Settings page's "Open Language Settings" link. The two
/// documented WinRT mechanisms for an in-app override (ApplicationLanguages.PrimaryLanguageOverride,
/// which throws for unpackaged apps; ResourceContext.SetGlobalQualifierValue, which was verified to
/// crash this unpackaged app natively, not just throw) were both ruled out empirically before
/// settling on this design — same "honest about capability" reasoning as the WLAN Location-
/// permission flow, which deep-links to OS settings rather than pretending it can grant the
/// permission itself.
/// </summary>
public interface ILocalizationService
{
    /// <summary>BCP-47 language tag currently in effect, e.g. "en-US" or "pl-PL" — whatever
    /// Windows' own display language setting resolved to.</summary>
    string CurrentLanguage { get; }

    string GetString(string key);
}
