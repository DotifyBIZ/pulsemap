# ADR-0001: WinUI 3 as the Windows shell, accepting Windows-only for now

- **Date:** 2026-08-31
- **Status:** Accepted
- **Deciders:** Product Manager
- **Affected systems:** Application shell (`Pulsemap.App`), packaging/distribution

## Context

Pulsemap's project plan requires native, Fluent-Design-quality look and feel on Windows as a hard requirement — "not a nice-to-have." Windows is also the primary environment for both Dotify's own use and client-site work in the initial phases. A Linux shell is explicitly planned for later (Phase 3) but must not block Windows delivery.

Dotify's engineering system's `desktop-winui` structure preset specifies WinUI 3 / Windows App SDK on .NET 9 as the standard for Windows desktop applications, with the explicit architectural principle that business logic must never reference WinUI types, so the core can survive a future port.

## Decision

Build the Windows shell (`Pulsemap.App`) on WinUI 3 / Windows App SDK, targeting `net9.0-windows10.0.19041.0` (Windows 10 version 2004 minimum). All propagation math, interpolation, placement logic, and data models live in `Pulsemap.App.Core`, a separate `net9.0` class library with zero dependency on the Windows App SDK.

## Options Considered

### WinUI 3 (chosen)

- Pros: True native Fluent Design and Mica support; matches Dotify's engineering standard and desktop-winui preset directly; first-class native WLAN API access for live measurement.
- Cons: Windows-only. No supported path to Linux — a port would require adopting Uno Platform (XAML-compatible) or rewriting the shell on Avalonia.

### Cross-platform UI framework from day one (e.g. Avalonia)

- Pros: Single codebase could target Windows and Linux immediately.
- Cons: Does not meet the plan's hard requirement for genuine native Windows look and feel; deviates from Dotify's desktop-winui standard without a documented reason to do so.

## Consequences

Windows-only for Phase 1 and Phase 2. Reaching Linux later (Phase 3) means porting or rewriting the shell — but because `Pulsemap.App.Core` has no WinUI dependency, that port touches only the UI layer, not the propagation/interpolation/placement engine or data models.

## Implementation Notes

- Enforce the boundary in code review: no `Microsoft.WindowsAppSDK` or `Microsoft.UI.*` reference anywhere in `Pulsemap.App.Core`.
- Platform-specific capabilities the Core needs (e.g. WLAN adapter enumeration) go behind interfaces defined in Core's `Abstractions/`, implemented only in `Pulsemap.App`.

## References

- Dotify engineering system: `structures/presets/desktop-winui.md`, `docs/standards/csharp.md`
- `pulsemap-project-plan.md`, section 3 (Platform & architecture)
