# ADR-0004: In-app update check — Pulsemap's first outbound network call

- **Date:** 2026-09-02
- **Status:** Accepted
- **Deciders:** Product Manager
- **Affected systems:** `Pulsemap.App` (`Services/GitHubUpdateCheckService`, `HomeViewModel`, `SettingsViewModel`), `Pulsemap.App.Core` (`Abstractions/IUpdateCheckService`, `Updates/`, `Settings/`), README, `scripts/build-installer.ps1`

## Context

Pulsemap's project plan states "no database server, nothing phones home," and every prior ADR that touched this line (see ADR-0003) deliberately left it unresolved rather than deciding it unilaterally — ADR-0003's own References section calls out update-checking by name as one of the still-open questions for a later phase.

Users installing an unsigned, unpackaged Windows app (ADR-0002) have no Store or MSIX auto-update mechanism to tell them a newer version exists. Without some check, a user could stay on an old build indefinitely, missing fixes, with no signal anything changed.

## Decision

Add a single, on-by-default, user-disableable check against GitHub's public Releases API on Home page load: `GET https://api.github.com/repos/DotifyBIZ/pulsemap/releases/latest`, comparing its `tag_name` against the running build's own assembly version. No account identifier, machine identifier, survey data, or usage information is attached to the request — it carries nothing but a User-Agent header, the same as opening the releases page in a browser would. If a newer version is published, Home shows a dismissible banner linking to the release; otherwise nothing is shown and nothing is logged beyond a warning on failure.

This is explicitly **not** telemetry: no data about this installation or its usage leaves the machine, and nothing is ever received back except a version string. It is still an outbound network call, so it gets its own visible Settings toggle (`Check for updates on launch`, default on) rather than being silently bundled into "diagnostics" or assumed to be as uncontroversial as the local-only logging ADR-0003 added.

## Options Considered

### GitHub Releases version check, on by default with an opt-out (chosen)

- Pros: No backend to build or operate — GitHub's existing public API is the only new dependency. Cheap to implement and to reason about (a version-string comparison, nothing else). Matches what most desktop apps without a store do. Stays honest about the one-way, non-identifying nature of the request.
- Cons: Is, undeniably, a network call this app didn't make before — requires updating README's "won't phone home" claim and getting explicit sign-off (this ADR) rather than treating it as self-evidently fine the way ADR-0003's log file was.

### Silent/automatic background updater (e.g. Velopack, Squirrel)

- Pros: Removes the manual "go download the new installer" step entirely.
- Cons: Materially bigger scope — delta packaging, background download, install-time relaunch — for a pre-release app that doesn't yet have a signed installer (ADR-0002) or a release cadence that would make automatic delivery clearly worth the added failure surface. Deferred, not rejected.

### Opt-in only (default off)

- Pros: The most conservative reading of "nothing phones home" — a user must actively choose to let anything leave the machine.
- Cons: Most users never visit Settings before their first launch, so the feature would go unused by nearly everyone it exists to help. The check carries no identifying data, so the privacy cost of on-by-default is close to zero; opt-out achieves the same user control with actual reach.

### Status quo — no update check at all

- Pros: Zero network surface added, no README change needed.
- Cons: Users have no way to learn a fix or feature they're missing exists, beyond manually checking GitHub themselves.

## Consequences

README's "won't phone home" bullet is amended to distinguish telemetry/accounts/servers (still absent) from this one, non-identifying, disableable version check — the spirit of the original claim (no data about you or your surveys ever leaves this machine) holds; the literal "zero network calls, ever" reading does not.

`scripts/build-installer.ps1` now passes `-p:Version=$Version` to `dotnet publish`, stamping semantic-release's actual version number into the published assembly — previously nothing did this, so `Assembly.GetName().Version` would have read a static SDK default forever, making any version comparison meaningless regardless of which approach this ADR chose.

A new `AppSettings`/`IAppSettingsService` (Core, JSON file at `%LocalAppData%\Pulsemap\settings.json`) is Pulsemap's first app-preferences store, distinct from `FileAppLogger`'s log files and from per-survey data under `MyDocuments`. It exists to hold exactly one setting today; it is not a general-purpose settings framework built ahead of need.

## Implementation Notes

- `IUpdateCheckService` lives in `Pulsemap.App.Core/Abstractions/`, matching the existing pattern for platform/infrastructure capabilities (`IWlanAdapterService`) — the interface is Core, the concrete `GitHubUpdateCheckService` (needs `IHttpClientFactory`, an App-layer DI concern) lives in `Pulsemap.App/Services/`.
- Version comparison itself (`SemanticVersionComparer`) is pure and lives in Core so it's unit-testable without a network dependency — it does the "vX.Y.Z" parsing/comparison; the HTTP call and JSON shape are the only things the App-layer service adds.
- A failed check (offline, GitHub down, malformed response) is treated identically to "no update available" — logged as a warning, never surfaced as an error, and never blocks anything else on the Home page.
- `HttpClient` is obtained via `IHttpClientFactory.CreateClient()` per call, never instantiated directly, per this repo's standing rule.

## References

- ADR-0002 (installer distribution, no Store/MSIX auto-update available)
- ADR-0003 (local diagnostic logging — the previous "does this touch 'nothing phones home'" decision, which explicitly left this one open)
- `pulsemap-project-plan.md`'s "no database server, nothing phones home" line (amended by this decision; the account/server/telemetry-free part is unchanged)
