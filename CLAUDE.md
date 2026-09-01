# Pulsemap — Instructions for AI Agents

Pulsemap is developed by Dotify. It follows Dotify's internal engineering standards, restated below in project-specific terms — this file is self-contained; it doesn't link back to Dotify's internal engineering-system host, since this repo is public and that host isn't.

## Two rules that override your defaults

**1. You are a tool, not an author.** Never add `Co-Authored-By` trailers naming an AI model, "Generated with..." tags, or any agent attribution to commits, PR descriptions, changelog entries, or code comments. The person who directed the work is the author and is accountable for it — this applies to every contributor, not just Dotify staff.

**2. Never scaffold a new top-level directory or restructure existing ones without asking first.** Propose the actual tree and wait for confirmation. If a human changes what you proposed, record why in `docs/adr/`.

## Before writing code

- **Read `docs/adr/` first.** Significant decisions — and why alternatives were rejected — are recorded there, not just in commit history.
- **Nullable reference types are enforced.** Never suppress a nullability warning with `!`. Express null-safety through types; handle `null` explicitly.
- **Async all the way down.** Never block on a `Task` with `.Result` or `.Wait()` — this deadlocks the UI thread. Accept and propagate a `CancellationToken` on every async I/O method.
- **`Pulsemap.App.Core` has zero WinUI/Windows App SDK dependency.** Business logic, models, and services live there and must compile without it. Platform-specific capabilities (e.g. WLAN adapter access) go behind interfaces in Core's `Abstractions/`, implemented only in `Pulsemap.App`.
- **MVVM, no logic in code-behind.** Code-behind wires views; decisions belong in view models and services.
- **Dispose what you open.** Use `using` declarations; types holding disposable fields implement `IDisposable`. `HttpClient` is the one exception — inject via `IHttpClientFactory`, never instantiate per call.
- **Exceptions are for the exceptional.** Expected outcomes (a missing record, an unmeasurable band) return a result, not a thrown exception. Never leave a catch block empty.
- **Money is `decimal`, distance/RF math is `double`** — don't mix them up.
- **Colors and type come from the design tokens**, not hand-picked hex values — see `docs/design-tokens.md` once it exists; until then, ask rather than guess a color.

## Non-negotiable, every change

- No secrets in code or version control — ever, including test fixtures.
- Validate input at every boundary: file parsing (floor plan images/PDFs, project bundles), WLAN adapter data, CLI/user input.
- Business logic (`Pulsemap.App.Core`) carries tests — minimum 80% branch coverage. UI projects are exempt from that bar but still need meaningful tests for view model logic.
- Dependencies are pinned, with a committed lockfile.
- New markdown docs, ADRs, and templates follow the existing structure in this repo — don't invent a new documentation convention.

## When unsure, stop and ask

Don't invent a convention to fill a gap. Name what's missing, propose the smallest reasonable option, and ask. A wrong convention adopted silently is far more expensive to undo than a question would have been.

---

## Project

- **What this is:** Pulsemap — a local-first WiFi site survey and planning tool. See `README.md`.
- **Structure preset:** Adapted from Dotify's `desktop-winui` preset — `src/Pulsemap.App` (WinUI 3 shell, unpackaged) + `src/Pulsemap.App.Core` (platform-free engine) + `tests/Pulsemap.App.Core.Tests` + `tests/Pulsemap.App.Tests`, wired into `Pulsemap.sln`.
- **Run locally:** `dotnet build Pulsemap.sln` then run `src/Pulsemap.App/bin/<config>/<platform>/<tfm>/<rid>/Pulsemap.App.exe`, or `dotnet run --project src/Pulsemap.App` on a machine with a display (this app needs an interactive Windows session — it won't launch headless).
- **Run tests:** `dotnet test Pulsemap.sln`. No real tests exist yet (Core has no logic yet) — this runs clean with zero tests collected.
- **Verify before committing:** `dotnet format Pulsemap.sln --verify-no-changes`, `dotnet build Pulsemap.sln --configuration Release`, `dotnet test Pulsemap.sln --configuration Release` — all three pass clean as of the initial scaffold (2026-09-01).
- **Deployed by:** GitHub Releases, InnoSetup-built installer wrapping the self-contained `WindowsAppSDKSelfContained` publish output (see `docs/adr/0002-installer-innosetup-over-msix.md`). No CI pipeline exists yet.
