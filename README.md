# Pulsemap

**Find the dead zones before your clients do.**
Plan it, survey it, place it — WiFi done right.

*Crafted with ❤️ by [Dotify](https://dotify.biz)*

## Status

Early development — no release yet, no build output yet. This README describes what Pulsemap is building toward, not what's shipped. See [CHANGELOG.md](CHANGELOG.md) for what's actually landed.

## What it's for

Pulsemap is a WiFi site survey and planning tool covering the full lifecycle of a wireless network: designing new deployments before any hardware exists, planning upgrades to existing networks, and troubleshooting coverage problems — on Dotify's own sites and on client sites.

Out of scope, deliberately: reading live data from AP controllers (UniFi, Omada, etc.) — this is survey/measurement tooling, not a controller integration. No built-in vendor hardware database — recommendations stay generic (power, channel, position). No cloud or hosted backend — fully local-first.

## Planned capabilities

- **Predictive coverage modeling** per band (2.4GHz, 5GHz, 6GHz), using wall/ceiling material and thickness where provided, falling back to distance + wall-count otherwise
- **Live on-site measurement** through whichever network adapter you choose, honest about what that adapter can and can't see (a 6GHz-blind adapter shows 6GHz as unmeasured, not as no signal)
- **Adaptive test-point suggestion** — points at the coverage gaps that matter most (Kriging-based interpolation), instead of a fixed grid
- **AP count and placement recommendations** — position, transmit power, and channel per band, always overridable
- **Floor plans and outdoor areas** — upload an image/PDF and draw on it, or build a structured room/zone list; outdoor areas share the same local grid, positioned relative to the building
- **Historical comparison** — snapshot a site before and after a change
- A guided wizard for new surveys, with the same data editable on a freeform canvas underneath
- Interactive heatmap, PDF report, and CSV/JSON export

## Platform

- **Windows shell:** WinUI 3 (Windows App SDK), native Fluent design, Mica backdrop
- **Core engine:** a platform-independent .NET library — propagation math, Kriging interpolation, placement optimization, and data models — with zero UI dependencies, so it can outlive any one shell
- Windows-first; a native Linux shell is planned later, sharing the same Core

Projects are local files — no account, no server, nothing phones home.

## Build phases

- **Phase 1 (MVP):** single floor, walls with optional material + thickness, live scan via chosen adapter, manual test-point placement, heatmap + PDF/CSV/JSON export, AP count + placement suggestion, generic per-band recommendations
- **Phase 2 (Depth):** multi-floor with ceiling/floor propagation between levels, adaptive Kriging-based test-point suggestion, outdoor areas (same local grid as indoor floors — not GPS/map-based; deliberately dropped that framing once actually designed), historical before/after comparison
- **Phase 3 (Reach):** Linux shell, vendor-specific config templates, monitor-mode adapter support

## Getting started

Not available yet — Pulsemap has no build or release. This section fills in once Phase 1 ships.

## Contributing

Contributions are welcome from day one — see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[PolyForm Shield 1.0.0](LICENSE) — free to use, modify, and redistribute for any purpose except building a competing product or service on top of it.

## Code of Conduct

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md).
