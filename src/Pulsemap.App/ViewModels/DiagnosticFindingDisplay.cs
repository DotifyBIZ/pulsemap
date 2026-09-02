using Pulsemap.App.Core.Diagnostics;

namespace Pulsemap.App.ViewModels;

/// <summary>A <see cref="DiagnosticFinding"/> with its message key already resolved to display text
/// — <see cref="DiagnosticsViewModel"/> is the only place that has both the localization service and
/// the finding's format args at hand. Deliberately has no WinUI dependency (unlike a Brush-typed
/// property would) so it stays constructible in a plain unit test — <see cref="Views.DiagnosticsPage"/>
/// resolves <see cref="Severity"/> to a design-token brush itself, via a converter.</summary>
public sealed record DiagnosticFindingDisplay(DiagnosticSeverity Severity, string Message);
