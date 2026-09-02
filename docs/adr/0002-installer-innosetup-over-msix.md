# ADR-0002: InnoSetup installer instead of MSIX

- **Date:** 2026-08-31
- **Status:** Accepted
- **Deciders:** Product Manager
- **Affected systems:** Packaging, distribution, release CI

## Context

Dotify's C# standard lists "MSIX package deployment (not file-copy distribution)" as a non-negotiable for WinUI 3 desktop applications. Pulsemap, however, is run by technicians on client-site machines whose Group Policy and security posture Dotify does not control.

MSIX has a hard requirement Dotify's other desktop tooling doesn't need to reckon with in the same way: Windows will not install an unsigned MSIX package at all. An unsigned or self-signed MSIX additionally requires Developer Mode or the `AllowAllTrustedApps` Group Policy to be enabled on the installing machine before install is even possible — a real risk on locked-down client machines Pulsemap has no administrative relationship with. A properly CA-signed MSIX avoids that, but:

- Dotify does not currently hold a code-signing certificate.
- The free signing route (SignPath Foundation) requires an OSI-approved license with no commercial dual-licensing. Pulsemap's PolyForm Shield 1.0.0 license is deliberately not OSI-approved (see the project plan, section 5) — it fails that eligibility bar by design, not by oversight.
- Even a paid certificate no longer grants immediate SmartScreen reputation — a 2024 policy change removed that benefit from EV certificates too, so every new release still shows an "unrecognized app" warning until it accumulates real download volume, regardless of cert tier.

A traditional installer (InnoSetup) has no equivalent hard requirement: unsigned, it shows the same SmartScreen reputation warning, but never blocks installation and never depends on the target machine's sideloading policy.

## Decision

Package Pulsemap with **InnoSetup** rather than MSIX, published directly as a release asset on GitHub Releases. `winget` submission is planned as a later addition — winget manifests support the `INNO` installer type natively, so this doesn't foreclose that path.

This is a deliberate deviation from Dotify's documented C# standard.

## Options Considered

### InnoSetup (chosen)

- Pros: No hard signing requirement to install at all; no Group Policy/sideloading dependency on the target machine; first-class winget installer type; mature, well-understood tooling.
- Cons: Deviates from Dotify's documented "MSIX...non-negotiable" standard; no built-in sandboxing or App Installer-driven auto-update (MSIX's main advantages over a traditional installer).

### MSIX, self-signed

- Pros: Matches the Dotify standard as written; free.
- Cons: Requires Developer Mode or `AllowAllTrustedApps` enabled on every installing machine — a plausible hard blocker on client sites Dotify doesn't administer.

### MSIX, paid certificate (e.g. Azure Trusted Signing, ~$10/month)

- Pros: Matches the Dotify standard as written; correctly attributed to Dotify; no sideloading policy dependency.
- Cons: Recurring cost; still doesn't grant instant SmartScreen reputation (that benefit was removed industry-wide in 2024); geographic/organizational eligibility for the cheapest tier needs verification.

## Consequences

Pulsemap loses MSIX's sandboxing and native auto-update mechanism for now. In exchange, it avoids a real deployment failure mode on client-site machines and avoids a recurring signing cost or the license change that free signing would otherwise require. If Dotify later adopts organization-wide MSIX signing infrastructure, this decision can be revisited without touching `Pulsemap.App.Core`.

Code signing for the InnoSetup output itself is still an open decision (unsigned for now vs. a paid cert) — lower-stakes than under MSIX, since it only affects the SmartScreen warning, not whether install is possible at all.

## Implementation Notes

- Release CI builds the InnoSetup script and attaches the resulting `.exe` to the GitHub Release, alongside built-in checksums.
- Revisit this ADR before submitting a winget manifest or if Dotify centralizes code-signing infrastructure.
- **Concrete project config** (confirmed working, `dotnet build` verified 2026-09-01): `Pulsemap.App.csproj` sets `WindowsPackageType=None` (unpackaged — no MSIX identity at all, per [Microsoft's unpackaged WinUI3 guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app)) and `WindowsAppSDKSelfContained=true` (bundles the Windows App SDK runtime into the app's own output, so users don't need a separate runtime installer). We deliberately do **not** set `PublishSingleFile` — Microsoft's own docs recommend exactly our path instead ("wrap the output folder in a single EXE installer") when single-file extraction-on-launch isn't wanted. The `win-x64`/`win-x86`/`win-arm64` publish profiles the template generated are already `FileSystem`/`SelfContained=true` publishes with no MSIX involved — InnoSetup should wrap that published output folder directly.
- `PublishTrimmed` is explicitly `false` — Microsoft's own WinRT interop assemblies (`Microsoft.Windows.SDK.NET`, `WinRT.Runtime`) aren't fully trim-clean and produce trim warnings that `TreatWarningsAsErrors` turns into build failures. Not a loss for us: trimming was only ever useful for the single-file path we're not taking.
- `Pulsemap.App.csproj` pins `LangVersion=preview` (scoped to that one project, not repo-wide) so CommunityToolkit.Mvvm's partial-property `[ObservableProperty]` pattern can use the C# `field` keyword — required for AOT/WinRT-safe `x:Bind` marshalling (`MVVMTK0045`) on the .NET 9 SDK, where `field` is still preview (stabilizes in C# 14/.NET 10).

## References

- Dotify engineering system: `docs/standards/csharp.md` (desktop-winui non-negotiables), `docs/security.md`
- `pulsemap-project-plan.md`, section 5 (Licensing)
