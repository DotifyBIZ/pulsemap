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
- **Colors and type come from the design tokens**, not hand-picked hex values — see `docs/design-tokens.md`. They live inlined in `App.xaml`'s `ResourceDictionary`, not a separate merged file — see the XAML compiler note below for why.
- **XAML compiler quirks (WindowsAppSDK.WinUI 2.3.6, observed 2026-09-01):** A standalone `.xaml` file whose root element is `ResourceDictionary` may fail with a cryptic `WMC9999 "cannot find Polish/neutral culture resources"` error — this is the XAML compiler's own error-reporting path breaking on a non-English OS locale, not a real problem with your markup, and it can mask a genuinely different underlying error. If you hit `WMC9999`, don't trust it as the real problem: temporarily move suspect files fully outside the project directory (renaming within the tree isn't enough — the SDK's XAML globbing is recursive and folder-name-independent) to bisect down to the actual cause. Concretely already found: (1) an extra `ResourceDictionary`-rooted file caused it once a `Page`-rooted file was *also* present and broken — inlining resources into `App.xaml` (which is `Application`-rooted and always compiled first) sidesteps it; (2) `{x:Bind Some.Method('literalArg')}` — the function-call-with-string-literal-argument form of x:Bind — is not supported by this compiler version and triggers the same masked error; use a computed property on the bound type instead; (3) `{x:Bind PageRoot.ViewModel.SomeCommand}` inside a nested `DataTemplate` — binding to an outer named element's property path from within a `DataTemplate`'s own `x:DataType` scope — also isn't supported here; fall back to a plain `Tag="{x:Bind}"` (binds the template's own DataContext, which *is* supported) plus a code-behind `Click` handler that calls the command directly.
- **Related but distinct — a runtime (not compile-time) XAML crash:** declaring `IsChecked="True"` on a `ToggleButton` in markup throws `Microsoft.UI.Xaml.Markup.XamlParseException: Failed to assign to property 'Microsoft.UI.Xaml.Controls.Primitives.ToggleButton.IsChecked'` the moment that page is constructed (`LoadComponent`), silently killing the app since it happens inside `async void`/fire-and-forget navigation code with nothing to catch it. Same locale-masked-error family (the exception's primary message is unreadable Polish mojibake; only a secondary fallback line names the real property), but this one fires at `InitializeComponent()` time, not build time — so it won't show up until the page is actually navigated to. Fix: don't set `IsChecked` declaratively; set it in code-behind after `InitializeComponent()` instead (e.g. `SomeToggleButton.IsChecked = true;`). If a page like this crashes the app with no visible error, temporarily wire `Application.UnhandledException` to write `e.Exception.ToString()` to a file — the default unhandled-exception behavior here is silent process termination with nothing in the UI.

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
- **Run tests:** `dotnet test Pulsemap.sln`. `Pulsemap.App.Core.Tests` has real coverage (88%+ branch as of 2026-09-01); `Pulsemap.App.Tests` is still empty (no ViewModel logic complex enough to need one yet).
- **Verify before committing:** `dotnet format Pulsemap.sln --verify-no-changes`, `dotnet build Pulsemap.sln --configuration Release`, `dotnet test Pulsemap.sln --configuration Release`.
- **Deployed by:** GitHub Releases, InnoSetup-built installer wrapping the self-contained `WindowsAppSDKSelfContained` publish output (see `docs/adr/0002-installer-innosetup-over-msix.md`). No CI pipeline exists yet.
- **Built so far:** the full Core engine (models, zip+JSON persistence, propagation model, Kriging interpolation, AP placement, CSV/JSON/PDF export) and the app shell (DI, NavigationView, Mica, HomePage). Still open: the New Survey wizard, the Workspace canvas, and WLAN scanning (native `wlanapi.dll` P/Invoke — see the plan's research notes on why the WinRT `Windows.Devices.WiFi` API won't work unpackaged).
- **Guided measurement walk (PM requirement, added 2026-09-01, not yet built):** the app should suggest measurement points, the surveyor walks to each and confirms arrival, the app captures the reading and advances. Every point captures background noise/interference (full BSS list, not just the target network); existing-network audits also capture the target network's own signal (new-deployment surveys skip this — no live network yet). Measured interference should then feed back into AP placement's channel/power suggestions. Affects `Survey`/`BandMeasurement` (needs a survey-type flag and an interference-readings collection), the Workspace canvas (Stage 8), `WlanAdapterService` (Stage 9), and `GreedyCoverageApPlacementOptimizer`/`ChannelPlan` (Stage 10) — see the plan doc for the full breakdown.
