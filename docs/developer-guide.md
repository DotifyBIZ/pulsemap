# Pulsemap Developer Guide

This is the map: how the codebase is organized, how the RF/planning engine actually works, and how to get productive as a contributor. It complements, rather than repeats:

- **[CONTRIBUTING.md](../CONTRIBUTING.md)** — branching, commit style, and the pull-request checklist. Read that first if you're about to open a PR.
- **[CLAUDE.md](../CLAUDE.md)** — the project's non-negotiable engineering rules and a running log of hard-won platform footguns (XAML compiler quirks, WinRT API dead ends). Originally written as instructions for AI coding agents working in this repo, but every rule in it applies equally to a human contributor — read it before writing code, not after something breaks.
- **[docs/adr/](adr/)** — why significant architectural decisions were made, and what alternatives were rejected. If your change touches one of these decisions, either follow it or open a new ADR explaining why not.

If you're here to *use* Pulsemap rather than build or modify it, see the [User Guide](user-guide.md) instead.

## Contents

- [Prerequisites](#prerequisites)
- [Getting the code running](#getting-the-code-running)
- [Solution structure](#solution-structure)
- [Architecture](#architecture)
  - [Layering](#layering)
  - [Domain models](#domain-models)
  - [Persistence](#persistence)
  - [Propagation and coverage](#propagation-and-coverage)
  - [Kriging interpolation](#kriging-interpolation)
  - [AP placement](#ap-placement)
  - [Measurement capture and guided-walk suggestion](#measurement-capture-and-guided-walk-suggestion)
  - [WLAN interop](#wlan-interop)
  - [WiFi diagnostics](#wifi-diagnostics)
  - [App-layer services](#app-layer-services)
  - [Localization](#localization)
  - [Dependency injection](#dependency-injection)
- [Testing strategy](#testing-strategy)
- [Adding things — a cookbook](#adding-things--a-cookbook)
- [Platform footguns](#platform-footguns)
- [CI/CD](#cicd)
- [Where to ask questions](#where-to-ask-questions)

## Prerequisites

- **Windows 10, build 19041 or later, 64-bit.** Pulsemap is WinUI 3 and Windows-only for now — see [ADR-0001](adr/0001-winui3-windows-only-platform.md).
- **.NET 9 SDK.** The exact version is pinned in [global.json](../global.json); `dotnet --version` should report something the SDK's `rollForward: latestMinor` policy accepts.
- **Visual Studio 2022** (with the "Windows App SDK" / WinUI workload) or VS Code with the C# Dev Kit both work. WinUI's XAML designer/hot-reload is VS-only if you rely on it, but the CLI build/test/run flow below doesn't need it.
- **Inno Setup 6+**, only if you want to build the installer locally (`Pulsemap.iss`) — not needed for day-to-day development.

## Getting the code running

```powershell
git clone https://github.com/DotifyBIZ/pulsemap.git
cd pulsemap
dotnet build Pulsemap.sln
dotnet run --project src/Pulsemap.App
```

`dotnet run` needs an interactive Windows session with a display — it will not launch headless or over most remote sessions without one.

```powershell
dotnet test Pulsemap.sln                       # full suite
dotnet format Pulsemap.sln --verify-no-changes # style check (matches CI)
```

Restores are locked-mode in CI (`dotnet restore --locked-mode`) — if you add or bump a package reference, run a plain `dotnet restore` locally first so `packages.lock.json` regenerates, and commit that file alongside your `.csproj` change.

## Solution structure

```
Pulsemap.sln
├── src/
│   ├── Pulsemap.App.Core/     # Platform-free engine — zero WinUI/Windows App SDK dependency
│   │   ├── Abstractions/      # Interfaces + DTOs for platform capabilities (WLAN, network health, updates)
│   │   ├── Diagnostics/       # Pure link-health heuristics (LinkDiagnosticsAnalyzer)
│   │   ├── Export/            # CSV/JSON/PDF export
│   │   ├── Interpolation/     # Ordinary Kriging
│   │   ├── Logging/           # File-based diagnostic logger
│   │   ├── Measurement/       # Guided-walk point suggestion, TestPoint capture
│   │   ├── Models/            # Survey, Floor, Wall, TestPoint, AccessPoint, ...
│   │   ├── Persistence/       # ISurveyFileService / ZipSurveyFileService
│   │   ├── Placement/         # AP placement optimizer, channel plan
│   │   ├── Propagation/       # Path-loss model, wall attenuation, coverage grid
│   │   └── Settings/          # App-preferences persistence
│   └── Pulsemap.App/          # WinUI 3 shell
│       ├── Controls/          # FloorPlanCanvas (the custom canvas control)
│       ├── Converters/        # XAML value converters
│       ├── Interop/           # Raw wlanapi.dll P/Invoke declarations
│       ├── Services/          # App-layer implementations of Core abstractions + WinUI-specific services
│       ├── Strings/           # en-US / pl-PL .resw resource files
│       ├── ViewModels/        # One per page, plus shared display records
│       └── Views/             # XAML pages + code-behind
├── tests/
│   ├── Pulsemap.App.Core.Tests/  # 107 tests, 80%+ branch coverage enforced in CI
│   └── Pulsemap.App.Tests/       # 133 tests, view models + App-layer services, hand-rolled fakes
├── docs/                       # This guide, the user guide, design tokens, ADRs
└── scripts/                    # build-installer.ps1 (used by the release pipeline)
```

**The one rule that shapes this split:** `Pulsemap.App.Core` has zero WinUI/Windows App SDK dependency and must compile without it. Business logic, models, and services live there. Anything that needs a platform capability (WLAN adapter access, file pickers, WinRT APIs) is defined as an interface in Core's `Abstractions/` and implemented only in `Pulsemap.App`. This is what lets Core survive a future shell rewrite (Phase 3's Linux idea) with zero changes.

## Architecture

### Layering

Standard MVVM: `Views/*.xaml` + `.xaml.cs` code-behind wire up a page and forward user gestures; `ViewModels/*.cs` hold state and decisions as `ObservableObject`s (CommunityToolkit.Mvvm's source-generated `[ObservableProperty]`/`[RelayCommand]`); Core services do the actual work. Code-behind should never contain business logic — if you're tempted to put an `if` there beyond "which dialog do I show," it belongs in the view model instead.

`App.Services` (a static `IServiceProvider` set up in `App.xaml.cs`'s `ConfigureServices`) is how views resolve their view model and any directly-needed service — there's no constructor injection into `Page`/`UserControl` types since WinUI constructs those itself. View models, by contrast, take everything via constructor injection, which is what makes them unit-testable with fakes.

### Domain models

`Survey` is the root: a name, type (`NewDeployment` or `ExistingNetworkAudit`), target bands, a list of `Floor`s, and a list of `SurveySnapshot`s (frozen copies for before/after comparison). A `Floor` has `Walls`, `TestPoints`, `AccessPoints`, a `PlanSource` (polymorphic — `RoomListSource` or `ImagePlanSource`, discriminated via `JsonPolymorphic`/`JsonDerivedType`), and either wall geometry (indoor) or explicit `OutdoorBoundsMin`/`Max` (outdoor, `IsOutdoor = true`). `Level` is an integer used only to estimate inter-floor signal leakage — not a real elevation.

`ImagePlanSource.ImageData` is `[JsonIgnore]`d — the actual bytes travel as a separate zip asset entry (see [Persistence](#persistence)), not inlined as base64 in the JSON. `PdfPageIndex` (0-based) picks which page of a multi-page PDF the plan comes from.

### Persistence

`ZipSurveyFileService` (Core) reads/writes a `.pulsemap` file: a zip containing `survey.json` plus, for every floor with an image-style plan, an `assets/floor-<id><ext>` entry. `SaveAsync` writes to a `.tmp` file and atomically `File.Move`s it into place — a process death mid-write can't corrupt the real file, which matters because Workspace auto-saves on nearly every edit.

`LoadAsync` pre-parses `SchemaVersion` via `JsonDocument` before deserializing the rest, and branches to `MigrateFromV1` for anything older than `CurrentSchemaVersion` (currently 2 — v1's singular `Floor` became v2's `Floors` list). If you ever need to bump the schema again: add the migration branch the same way, and keep new `Floor`/`Survey` properties non-required with sensible defaults wherever possible, so an *old* file deserializes straight into the new shape with no migration code needed at all — this is how `Floor.IsOutdoor`, `Level`, and `ImagePlanSource.PdfPageIndex` were all added without touching `MigrateFromV1`.

`.pulsemap` files are untrusted input (hand-edited, corrupted, or from an unexpected build) — every load runs through a `Sanitize` pass afterward: out-of-range enum values (`System.Text.Json` will deserialize *any* integer into an enum field without validation) are normalized rather than left to throw later out of a propagation-math switch statement, coordinates are clamped to a sane range, and a non-positive `PixelsPerMeter` is replaced. Decompression is bounded too — `CopyWithLimitAsync` caps `survey.json` at 50MB and any single asset at 200MB regardless of what the zip's own (untrustworthy) declared size says, closing off a decompression-bomb entry.

### Propagation and coverage

`LogDistancePropagationModel` (the only `IPropagationModel` implementation) computes free-space (Friis) path loss between two points, plus `WallAttenuationTable`'s per-material dB penalty for every wall the direct line between them crosses (a flat generic penalty when a wall has no material specified). `FloorGrid.BuildPoints` generates the regular candidate grid every consumer below shares — it self-limits to `FloorGrid.MaxGridPoints` (250,000) by widening the spacing rather than allocating without bound, since an absurd extent (a wall dragged to a huge coordinate, a tiny pixels-per-meter) would otherwise mean an unbounded allocation and a frozen UI.

`CoverageGridCalculator.ComputeGrid` walks that grid and, per point, takes the strongest signal across every AP on the target floor plus (subject to skip rules) every other floor — same-level-different-floor pairs and outdoor floors don't participate in the cross-floor model, since "the same spot one level up" only means something for floors genuinely stacked at the same origin. `StrongestSignalDbm` is the single-point version of the same logic, extracted so Workspace's live-vs-predicted diagnostics comparison can never disagree with the heatmap about what "predicted signal here" means.

### Kriging interpolation

`OrdinaryKrigingInterpolator` solves the standard Lagrange-multiplier kriging system once via LU factorization (reused across every query point — `O(n³ + queries·n²)`, not `O(queries·n³)`). It exposes both an `Estimate` and its own estimation `Variance` from the same solve. Today, `Variance` is the one with a real production caller (`MeasurementPointSuggester`'s adaptive guided-walk ordering, described next); nothing currently corrects the coverage heatmap itself from real measurements via `Estimate` — the heatmap stays a pure propagation-model prediction. Wiring measured test points back into the displayed heatmap (rather than just walk ordering) is a natural next step if you're looking for something meaty to build.

### AP placement

`GreedyCoverageApPlacementOptimizer` is the standard greedy approximation to maximum-coverage facility location: repeatedly place an AP wherever it covers the most currently-uncovered grid area, until reaching 95% coverage or a cap of 8 APs. It optimizes physical position against whichever requested band has the shortest range (highest frequency — `bands.Max()`, since the enum ordinal increases with frequency), on the theory that the same spot serving the hardest band to reach also serves the easier ones. Channel assignment per band is a separate pass afterward: `RankChannelsByInterference` orders each band's channel list by measured interference (from guided-walk `TestPoint.InterferenceReadings`) plus a cross-floor score (channels already used on nearby floors, discounted by the same inter-floor attenuation the coverage grid uses), then assigns round-robin across placed APs — with zero measurements, every channel ties and the original order wins, so this is purely additive over the original fixed-order behavior.

A grid point's "reliable" threshold isn't a flat −67dBm everywhere — `EffectiveReliabilityThresholdDbm` raises it near a measured `TestPoint` when nearby interference on that band is strong enough that −67dBm signal still wouldn't beat it by a reasonable SINR margin (10dB). This only has an effect within 8m of an actual measurement; everywhere else the plain −67dBm floor applies, identical to before any guided walk existed.

### Measurement capture and guided-walk suggestion

`TestPointCapture.BuildTestPoint` turns one WLAN scan into a `TestPoint`: every observed network becomes an `InterferenceReadings` entry (used for channel planning regardless of survey type), and for an `ExistingNetworkAudit` survey with a target SSID set, the strongest matching reading per band also becomes a real `Measurements` entry. A new-deployment survey only ever gets interference data — there's no live network yet to measure signal from.

`MeasurementPointSuggester.SuggestPoints` builds candidates from the same `FloorGrid`, filtered to exclude anything within 1m of an already-captured point, at a wider 3m spacing (a human has to walk there, unlike a placement candidate). The `(Floor, Band, IKrigingInterpolator)` overload reorders those same candidates by descending Kriging variance once at least 2 same-band measurements exist — falling straight back to plain grid-scan order otherwise.

### WLAN interop

`WlanAdapterService` and `WlanLinkDiagnosticsService` (`Pulsemap.App/Services/`) go through raw `wlanapi.dll` P/Invoke (`Interop/NativeWlan.cs`) rather than the WinRT `Windows.Devices.WiFi` API, which requires package identity this unpackaged app deliberately doesn't have (see [ADR-0002](adr/0002-installer-innosetup-over-msix.md) for the packaging decision this follows from). Struct layouts in `NativeWlan.cs` are verified against Microsoft Learn's `wlanapi.h` reference, not assumed — a wrong field order there corrupts memory silently instead of failing loudly, so if you ever touch that file, re-verify against the actual header docs rather than trusting IntelliSense inference. Both services open and close a client handle per call rather than holding one for the app's lifetime, since scans are infrequent and user-triggered — simplicity over avoiding a small per-call overhead.

Every P/Invoke call in this layer is synchronous and blocking, so both services wrap their entry points in `Task.Run` rather than exposing a blocking `Task`-returning method that's secretly synchronous underneath — never call into `NativeWlan` directly from a UI-thread `async` method without that wrapper.

### WiFi diagnostics

`LinkDiagnosticsAnalyzer` (Core, pure, zero I/O) turns a `LinkDiagnosticsSnapshot` + `NetworkHealthSnapshot` into plain-language findings — weak/very-weak signal, a legacy PHY rate fallback on 5/6GHz, DNS failure/slowness, high/absent gateway ping, and (only when a predicted signal is supplied) a predicted-vs-actual mismatch. `DiagnosticsViewModel` (the standalone Diagnose page) calls it with no prediction; `WorkspaceViewModel.DiagnoseAtPointAsync` calls it with one, computed via `CoverageGridCalculator.StrongestSignalDbm` at the clicked point — this is the only difference between the two, and it's why the analyzer takes prediction as an optional parameter rather than being two separate classes.

### App-layer services

- **`FloorPlanFilePickerService`** / **`SurveyExportFilePickerService`** wrap WinRT's `FileOpenPicker`/`FileSavePicker` — unpackaged apps need the owning HWND wired in via `InitializeWithWindow.Initialize` before either will show.
- **`FloorPlanImageCache`** rasterizes an uploaded image/PDF to a local PNG once (cached by a SHA-256 hash of the bytes, plus the PDF page index for a PDF, so two floors picking different pages of the same uploaded PDF never collide) and hands `FloorPlanCanvas` a plain file path to load via `BitmapImage`'s file-URI path — WinRT's own `Windows.Graphics.Imaging`/`Windows.Data.Pdf` decode APIs require package identity this app doesn't have, same family of constraint as the WLAN interop choice above.
- **`FileAppLogger`** appends to a daily-rolling text file and never throws — a logging failure must never take down whatever caller was already handling its own error. It also prunes files older than 30 days once per process.
- **`FileAppSettingsService`** persists `AppSettings` as plain JSON; a missing or corrupt file quietly falls back to defaults.
- **`GitHubUpdateCheckService`** is Pulsemap's only unprompted network call — see [ADR-0004](adr/0004-update-check-network-call.md). A failed check (offline, GitHub down, malformed response) is always treated identically to "no update available," never surfaced as an error.

### Localization

Two separate mechanisms, deliberately not unified:

1. **XAML-declared text** uses `x:Uid` + `.resw` files (`Strings/en-US/`, `Strings/pl-PL/`) — the normal WinUI mechanism, and it works correctly.
2. **Strings built dynamically in C#** (interpolated into a ViewModel-computed display string, or looked up by a runtime-chosen key like a diagnostic finding's message key) go through `ILocalizationService.GetString`, backed by `LocalizationService` — a hand-rolled `Dictionary<string, IReadOnlyDictionary<string, string>>` table, *not* a WinRT resource API. This exists because every WinRT resource-lookup API reachable from C# (`ResourceContext.SetGlobalQualifierValue`, `ResourceLoader.GetForViewIndependentUse()`, `ResourceManager.MainResourceMap.GetValue`, a direct `ResourceContext()`) either crashed natively or threw for keys that resolved fine via `x:Uid` for the exact same resource, confirmed empirically across four independent attempts — see the class's own doc comment for specifics if you're tempted to try a fifth. `LocalizationService.CurrentLanguage` reads `CultureInfo.CurrentUICulture`, so it still follows Windows' own display-language setting; a key missing from the current language's table falls back to English rather than leaking a raw key.

If you add a new dynamic string, add it to **both** language dictionaries in `LocalizationService.cs`, in the same position in both, so a parity check (`Strings["en-US"].Keys` vs `Strings["pl-PL"].Keys`, both dictionaries) never drifts. If you add a new `x:Uid`, add the matching entry to **both** `.resw` files. Both `.resw` files are CRLF — a shell script doing a multi-line-anchored text substitution against them with a bare `\n` pattern will silently no-op rather than error; this bit a real commit once (see the note in `CLAUDE.md`'s "process note" from 2026-09-03) and is worth remembering before scripting an edit to either file.

### Dependency injection

`App.xaml.cs`'s `ConfigureServices` is the single place every service and view model is registered — Core services as singletons (they're stateless), App-layer platform wrappers also as singletons, view models as transient (a fresh instance per navigation). If you add a new service interface + implementation, register it here; if you add a new page, register its view model here too, `AddTransient`.

## Testing strategy

- **`Pulsemap.App.Core.Tests`** (107 tests) has real coverage, enforced in CI at an **80% branch floor** via Coverlet — this is the platform-independent engine, and it's expected to carry the weight of correctness proof for the RF math, persistence, and placement logic.
- **`Pulsemap.App.Tests`** (133 tests) covers view models and App-layer services using hand-rolled fakes under `Fakes/` for every App-layer interface, rather than a mocking library — the interfaces are small enough that a fake is less ceremony than a mock setup, and it avoids adding a new dependency for it.
- `FloorPlanImageCacheTests` is a deliberate exception to "unit test, mock the boundary": it genuinely exercises PDFium/SkiaSharp rasterization (via `PDFtoImage`) on a real machine rather than mocking it, because the actual question that test answers is "does the native dependency work at all when unpackaged," not "does this C# call compile." If you touch `FloorPlanImageCache`, keep at least one test that renders a real (even if minimal, generated on the fly with `PdfSharp`) PDF rather than mocking the rasterizer away.
- Regression tests get a comment naming the real bug they reproduce, not just what they assert — see `WorkspaceViewModelTests`' guided-walk button test or the sanitize tests in `ZipSurveyFileServiceTests` for the pattern. If you fix a bug, add the test that would have caught it, with a one-line note on the actual failure mode.

Run `dotnet test tests/Pulsemap.App.Core.Tests/Pulsemap.App.Core.Tests.csproj -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:Threshold=80 -p:ThresholdType=branch -p:ThresholdStat=total` locally to check the coverage gate before pushing — this is exactly what CI runs.

## Adding things — a cookbook

**A new localized string:** add it to both `.resw` files (XAML-declared) or both dictionaries in `LocalizationService.cs` (C#-computed) — see [Localization](#localization).

**A new page:** add the `.xaml`/`.xaml.cs` under `Views/`, a matching `ViewModel` under `ViewModels/` registered `AddTransient` in `ConfigureServices`, and (if it belongs in the nav rail) a `NavigationViewItem` in `MainPage.xaml` plus a case in `MainPage.xaml.cs`'s `NavigateToTag`.

**A new Core service:** define the interface in `Abstractions/` if it wraps a platform capability, implement it in `Pulsemap.App/Services/`, register both in `ConfigureServices`. If it's pure logic with no platform dependency, it can live and be implemented entirely in Core.

**A new wall material, band, or channel plan entry:** `WallAttenuationTable.References` (Core), `Band` enum (Core, plus every `switch` over it — the compiler's exhaustiveness warnings under `TreatWarningsAsErrors` will find them for you), `ChannelPlan` (Core).

**A schema change to `Survey`/`Floor`/etc.:** prefer a new property with a sensible default over a version bump — see [Persistence](#persistence). Only add a `MigrateFromV1`-style branch and bump `CurrentSchemaVersion` for a genuinely breaking shape change (a property that changed *type*, or moved structurally, the way `Floor` → `Floors` did).

## Platform footguns

CLAUDE.md keeps the authoritative, continuously-updated list — don't duplicate it here, but know it exists and read it before you hit one of these yourself:

- A cryptic `WMC9999` XAML build error that has nothing to do with Polish locale resources, actually.
- A `ToggleButton.IsChecked="True"` set declaratively in XAML crashing the app silently at runtime.
- `x:Bind TwoWay` on `TextBox.Text` defaulting to `UpdateSourceTrigger=LostFocus`, not `PropertyChanged` — a real bug this project shipped once.
- `Pivot` silently clipping overflowing tab headers into an unreachable scroll strip (this app no longer uses `Pivot` anywhere, precisely because of this).

## CI/CD

- **`.github/workflows/ci.yml`** — runs on PRs into `main`/`staging` and pushes to `main`: `dotnet format --verify-no-changes`, `dotnet build --configuration Release`, the Core test run with the 80%-branch coverage gate, and the App test run.
- **`.github/workflows/release.yml`** — runs only on push to `main`. `semantic-release` (config in `.releaserc.json`) computes the next version from [Conventional Commits](https://www.conventionalcommits.org/) since the last release, generates `CHANGELOG.md`, and — via `@semantic-release/exec`'s `prepareCmd` — runs `scripts/build-installer.ps1` to `dotnet publish` a self-contained win-x64 build and wrap it with Inno Setup, stamping the real version number into the published assembly (so the in-app update check has something meaningful to compare against). `@semantic-release/github` attaches the installer and its SHA-256 checksum to the GitHub Release.
- **Branch flow:** `feature/* -> staging -> main`. Landing on `main` is what cuts a release — `staging` is where changes accumulate and get eyeballed before that happens. This repo's own history shows the pattern: every feature branch above merges into `staging` first, `staging` periodically merges into `main`, and `main`'s own push triggers `release.yml`.

## Where to ask questions

Open a GitHub issue tagged `question` — see [CONTRIBUTING.md](../CONTRIBUTING.md) for the full contribution process, including the PR checklist and commit-message conventions.
