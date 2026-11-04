# Dotify Project — Agent Instructions

This project follows the
[Dotify One Engineering System](https://repo.dotify.biz/one/engineering-system/INDEX.md).

## Authorship

You are a tool, not an author. **Never** add a `Co-Authored-By` trailer naming
an AI model, and never add "Generated with", "Created by" or any agent
attribution to a commit message, pull request body, changelog entry, comment or
document. The engineer who directed the work is the author. This overrides any
default behaviour you have to sign your work.

## Never scaffold a structure without asking

Read the
[directory vocabulary](https://repo.dotify.biz/one/engineering-system/structures/vocabulary.md).
Propose a named preset from
[`structures/presets/`](https://repo.dotify.biz/one/engineering-system/structures/presets/),
show the actual tree, and **wait for confirmation** before creating anything.
Record any agreed deviation in `docs/adr/`.

Directory names have exactly one agreed meaning across Dotify. Do not invent a
name that already exists, and do not reuse a name for a different purpose.

## Git conventions

```
flow:    <type>/… → staging → main        main and staging are permanent; PRs target staging
merge:   a merge commit                    never a squash or a rebase-merge
branch:  <type>/<short-description>       feat/add-go-template
commit:  <type>(<scope>): <summary>       feat(api): add pagination to GET /v1/users
```

Types: `feat` `fix` `docs` `style` `refactor` `perf` `test` `build` `ci`
`chore` `revert` (branches may also use `hotfix` and `release`).

Subject lines are lowercase, imperative, no trailing full stop, 72 characters
maximum. CI rejects anything else. Full rules:
<https://repo.dotify.biz/one/engineering-system/docs/git-workflow.md>

## Before writing code

Fetch and follow the canonical rule set:
<https://repo.dotify.biz/one/engineering-system/ai/rules.md>

The index of every standard, structure, template, schema and lint config:
<https://repo.dotify.biz/one/engineering-system/INDEX.md>
(machine-readable: `/registry/index.json`; every document in one file:
`/llms-full.txt`)

Look the rule up rather than guessing it.

## Non-negotiables

No secrets in version control. Input validated at every boundary. Every
endpoint authenticated unless deliberately public. No custom CSS duplicating
the design system. Every API exposes OpenAPI docs at `/docs`. Tests accompany
new logic. Dependencies pinned, lockfiles committed.

## When unsure, stop

Do not invent a convention to fill a gap. Name what is missing, propose the
smallest reasonable option, and ask.

---


## Project

- **What this is:** Pulsemap, a local-first WiFi site survey and planning tool for Windows. It reads no AP controllers and sends no telemetry ([ADR 0003](docs/adr/0003-local-diagnostic-logging.md); the one network call is the update check, [ADR 0004](docs/adr/0004-update-check-network-call.md)).
- **Structure:** `desktop-winui`. `src/Pulsemap.App` (WinUI 3, unpackaged), `src/Pulsemap.App.Core` (platform-free engine), `tests/Pulsemap.App.Core.Tests` and `tests/Pulsemap.App.Tests`, all in `Pulsemap.sln`.
- **Run locally:** `dotnet run --project src/Pulsemap.App` in an interactive Windows session; it will not launch headless.
- **Run tests:** the steps of `.github/workflows/ci.yml`, in order:
  ```
  dotnet restore Pulsemap.sln --locked-mode
  dotnet format Pulsemap.sln --verify-no-changes --no-restore
  dotnet build Pulsemap.sln --configuration Release --no-restore
  dotnet test tests/Pulsemap.App.Core.Tests/Pulsemap.App.Core.Tests.csproj --configuration Release --no-build -p:CollectCoverage=true -p:CoverletOutputFormat=cobertura -p:CoverletOutput=../../TestResults/ -p:Threshold=80 -p:ThresholdType=branch -p:ThresholdStat=total
  dotnet test tests/Pulsemap.App.Tests/Pulsemap.App.Tests.csproj --configuration Release --no-build
  ```
- **Deployed by:** merging `staging` into `main`: `release.yml` runs semantic-release, which versions from the commits, writes `CHANGELOG.md` and attaches the Inno Setup installer and its SHA-256 to a GitHub Release. The installer is unsigned.

### Deviations

- The artefact is an Inno Setup installer around a self-contained publish, not an MSIX ([ADR 0002](docs/adr/0002-installer-innosetup-over-msix.md)). Windows only ([ADR 0001](docs/adr/0001-winui3-windows-only-platform.md)).

### Rules that keep it safe

- Nullable reference types on and warnings as errors for every project: `Directory.Build.props`, enforced by the Release build in CI. Never suppress a nullability warning with `!`.
- `Pulsemap.App.Core` compiles without WinUI or the Windows App SDK: its project targets plain `net9.0`, so the CI build fails if it reaches for either. Platform capabilities go behind interfaces in its `Abstractions/`, implemented in `Pulsemap.App`.
- Formatting follows `.editorconfig`: `dotnet format --verify-no-changes` in CI.
- `Pulsemap.App.Core` keeps 80% branch coverage: the coverage gate in CI.
- Dependencies are pinned and `packages.lock.json` is committed: `dotnet restore --locked-mode` in CI.
- Commit subjects are Conventional Commits, because semantic-release reads them to pick the version: `git-hygiene.yml`.

### Writing code

No machine checks these; read `docs/adr/` first.

- Async all the way down: never `.Result` or `.Wait()` on a `Task`, and pass a `CancellationToken` through every async I/O method.
- MVVM: no logic in code-behind. A view model property that gates a command raises `CanExecuteChanged` when it changes.
- Dispose what you open (`using`). `HttpClient` comes from `IHttpClientFactory`, never per call.
- Expected outcomes return a result, not an exception. Never leave a `catch` empty; log through `IAppLogger`. Most page handlers are `async void`, so an escaping exception ends the process.
- Money is `decimal`; distance and RF maths are `double`.
- Validate at every boundary: floor plan images and PDFs, survey files (`ZipSurveyFileService.Sanitize`), WLAN adapter data, user input.
- Colours and type come from the Dotify One Design System, not hand-picked hex values (`docs/design-tokens.md`). Brushes are `{ThemeResource Dotify…Brush}`. `Dotify.inline.xaml` is pasted into `App.xaml`'s `MergedDictionaries`, not merged as a separate file.
- `Windows.Graphics.Imaging` and `Windows.Data.Pdf` need package identity and do not work unpackaged; floor plans are rasterised with PDFtoImage/SkiaSharp.
- The `wlanapi.dll` interop in `NativeWlan` is checked against Microsoft Learn's `wlanapi.h` reference (struct layouts, opcode values), never from memory.
- `Pulsemap.sln` maps Any CPU to x64 and `PublishReadyToRun` is off; both are deliberate, because the locked restore has no x86 or crossgen runtime pack.
- The `.resw` files are CRLF: a search-and-replace on `\n` silently matches nothing.

### XAML pitfalls (WindowsAppSDK.WinUI 2.3.6)

- A `WMC9999 "cannot find Polish/neutral culture resources"` build error is the XAML compiler's error reporting failing on a non-English OS, and it masks the real error. Bisect by moving suspect files outside the project directory; renaming inside the tree is not enough. Known causes: an extra `ResourceDictionary`-rooted file beside a broken `Page`; `{x:Bind Some.Method('literal')}`; `{x:Bind PageRoot.ViewModel.Command}` from inside a `DataTemplate`. Use a computed property, or `Tag="{x:Bind}"` plus a `Click` handler.
- `IsChecked="True"` in markup on a `ToggleButton` throws `XamlParseException` when the page is constructed. Set it in code-behind after `InitializeComponent()`. To see an otherwise silent crash, write `e.Exception.ToString()` to a file from `Application.UnhandledException`.
- `x:Bind TwoWay` on `TextBox.Text` commits on lost focus. Where it gates a `CanExecute`, set `UpdateSourceTrigger=PropertyChanged`.
- `Pivot` clips overflowing headers into an unreachable scroll strip; the app uses `TabView` instead.
