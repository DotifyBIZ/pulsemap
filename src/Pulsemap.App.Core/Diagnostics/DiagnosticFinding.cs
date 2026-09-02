namespace Pulsemap.App.Core.Diagnostics;

/// <summary>One troubleshooting observation. <paramref name="MessageKey"/> is a localization key,
/// not display text — <see cref="FormatArgs"/> are passed to the App layer's
/// <c>string.Format</c>/<c>ILocalizationService</c> call so this stays free of any UI/culture
/// concerns, matching how the rest of Core hands display strings back as keys.</summary>
public sealed record DiagnosticFinding(DiagnosticSeverity Severity, string MessageKey, IReadOnlyList<object>? FormatArgs = null);
