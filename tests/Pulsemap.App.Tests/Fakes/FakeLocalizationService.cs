using Pulsemap.App.Services;

namespace Pulsemap.App.Tests.Fakes;

/// <summary>Returns the resource key itself rather than real (locale-dependent) translated text —
/// keeps tests deterministic regardless of which OS locale runs them, and test failures show
/// exactly which key was expected vs. produced. Tests assert the ViewModel picked the *right key*
/// for a given state, not the translated prose, which is a content-correctness concern the actual
/// .resw files and manual runtime verification cover instead.</summary>
internal sealed class FakeLocalizationService : ILocalizationService
{
    public string CurrentLanguage => "en-US";

    public string GetString(string key) => key switch
    {
        // The one format string with a real regression test riding on its argument substitution
        // (the guided walk's "Point N of M" counter had an off-by-one bug once) - every other key
        // returns itself unchanged, so most tests assert on which key was selected rather than on
        // translated prose.
        "WorkspaceGuidedWalkProgressFormat" => "Point {0} of {1} — walk to ({2:0.0}m, {3:0.0}m) and confirm.",
        _ => key,
    };
}
