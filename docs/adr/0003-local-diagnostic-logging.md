# ADR-0003: Local diagnostic logging, not telemetry

- **Date:** 2026-09-01
- **Status:** Accepted
- **Deciders:** Product Manager
- **Affected systems:** `Pulsemap.App.Core` (`Logging/`), all catch blocks that previously swallowed or silently handled a failure, Settings page

## Context

Pulsemap's project plan states "no database server, nothing phones home," and Phase 1 has stayed local-only on that basis — no telemetry, no crash reporting, no analytics, no outbound network calls of any kind beyond the already-documented WLAN scanning and `ms-settings:` deep links. That stance is unchanged by this decision and this ADR does not revisit it (see the separate, still-open question tracked outside this repo about telemetry in a *later* phase).

Separately, the app had no way for a user to hand over useful diagnostic information when something goes wrong. The only thing writing anything to disk on failure was a single `UnhandledException` handler in `App.xaml.cs` dumping the raw exception to a fixed temp file — every other failure path (a corrupt survey file, a failed save, a skipped-because-invalid file in the survey list) either set an on-screen error message with no record of *why*, or silently did nothing. A user reporting "my survey disappeared" or "the app crashed" had nothing they could send that would explain what happened.

## Decision

Add a local, rolling, plain-text diagnostic log (`IAppLogger`/`FileAppLogger` in `Pulsemap.App.Core/Logging/`) that records ERROR/WARN/INFO lines to `%LocalAppData%\Pulsemap\Logs\pulsemap-{yyyy-MM-dd}.log`. This is diagnostic logging, not telemetry: nothing is transmitted anywhere by the app itself. If a user hits a problem, Settings has an "Open Logs Folder" button — they find the file and decide for themselves whether to attach it to a bug report. No automatic collection, no opt-out toggle needed because there's nothing to opt out of.

## Options Considered

### Local-only log file, manually shared (chosen)

- Pros: Zero network surface added — doesn't touch the "nothing phones home" line at all, so it doesn't need the PM sign-off or Security Baseline "integrating external services" review that actual telemetry would trigger. Gives real diagnostic value today. User stays in full control of what leaves their machine.
- Cons: Relies on the user actually finding and sending the file — no automatic crash reporting to catch problems the user doesn't notice or doesn't bother reporting.

### Automatic crash/telemetry reporting to a backend

- Pros: Would catch failures proactively without depending on the user to notice and report them.
- Cons: Requires a backend to receive reports (out of scope, and the separate telemetry question this ADR deliberately doesn't touch is still open), directly contradicts the plan's current "nothing phones home" text, and would trip Dotify's Security Baseline review for integrating an external service. Not a Phase 1 decision to make unilaterally.

### No logging at all (status quo)

- Pros: Simplest — nothing to build.
- Cons: Leaves the app with no diagnosable failure path beyond a raw crash-only text dump. A corrupt-file or failed-save report from a user would be unactionable.

## Consequences

Every catch block that used to swallow or silently handle a failure (`ZipSurveyFileService`'s save/load paths, `SurveyLibraryService`'s skip-corrupt-file case, `NewSurveyWizardViewModel` and `WorkspaceViewModel`'s error paths) now also logs. `App.xaml.cs`'s `UnhandledException` handler keeps its original raw, dependency-free crash-file write exactly as-is — that's the one handler that must never itself fail — and separately makes a best-effort call through `IAppLogger` alongside it, wrapped in its own try/catch.

If telemetry is ever added in a later phase, it's a distinct, separate decision — this ADR's "local-only, user-shared" log is not a stepping stone toward automatic transmission, and adding that later would need its own ADR and sign-off, not a quiet extension of this one.

## Implementation Notes

- Lives in `Pulsemap.App.Core`, not `Pulsemap.App`: plain file I/O has no WinUI dependency, and `ZipSurveyFileService` (Core) needs to log too, so this is a legitimate Core citizen rather than a platform capability behind an `Abstractions/` interface.
- `%LocalAppData%`, not `SurveysDirectory`'s `MyDocuments` — this is app diagnostic data, not user-owned survey data, matching the conventional Windows split between the two.
- Kept to exactly three levels (Error/Warning/Info) — no Debug/Trace/Critical added speculatively.
- Must never itself throw: a logging failure (disk full, permissions) should never take down the caller's own error handling.

## References

- `pulsemap-project-plan.md`'s "no database server, nothing phones home" line (unchanged by this decision)
- The still-open telemetry-for-later-phases question (tracked outside this repo, not resolved by this ADR)
