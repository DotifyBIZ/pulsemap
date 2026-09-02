<div align="center">
  <img src=".github/images/logo.png" alt="Pulsemap" width="120" height="120">

  # Pulsemap

  **Find the dead zones before your clients do.**

  Plan it, survey it, place it — WiFi coverage done right, entirely on your own machine.

  [![CI](https://github.com/DotifyBIZ/pulsemap/actions/workflows/ci.yml/badge.svg)](https://github.com/DotifyBIZ/pulsemap/actions/workflows/ci.yml)
  [![License: PolyForm Shield 1.0.0](https://img.shields.io/badge/license-PolyForm%20Shield%201.0.0-blue)](LICENSE)
  ![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)
  ![.NET 9](https://img.shields.io/badge/.NET-9-512BD4)

</div>

## Contents

- [Pulsemap](#pulsemap)
  - [Contents](#contents)
  - [What it does](#what-it-does)
  - [What it deliberately doesn't do](#what-it-deliberately-doesnt-do)
  - [Status](#status)
  - [Building from source](#building-from-source)
  - [Architecture](#architecture)
  - [Roadmap](#roadmap)
  - [Contributing](#contributing)
  - [License](#license)

## What it does

Pulsemap is a local-first WiFi site survey and planning tool covering the full lifecycle of a wireless network: designing new deployments before any hardware exists, planning upgrades to existing networks, and diagnosing coverage problems on-site — indoors and out, single floor or many.

- **Predictive coverage modeling** per band (2.4/5/6 GHz), from a log-distance path-loss model with per-material wall attenuation (drywall through reinforced concrete) where wall data is provided, plus an inter-floor penalty for multi-floor buildings
- **Live on-site measurement** through whichever adapter you pick, honest about what that adapter can and can't see — a 6 GHz-blind adapter shows 6 GHz as unmeasured, never as no signal
- **Adaptive test-point suggestion** — Kriging interpolation tracks its own estimation uncertainty and sends you to whichever spot the model is least sure about, instead of a fixed walking grid
- **AP count and placement recommendations** — position, transmit power, and channel per band, ranked by measured interference once a walk has captured some, always overridable
- **Floor plans and outdoor areas** — upload an image/PDF and draw over it, or describe rooms as a structured list; outdoor zones (parking lots, courtyards) share the same coordinate grid and use free-space propagation instead of wall-attenuated
- **Historical comparison** — snapshot a survey before and after a change, then view both side by side
- A guided wizard for new surveys, with the same data editable on a freeform canvas underneath
- Interactive heatmap, PDF report, and CSV/JSON export

## What it deliberately doesn't do

- Read live data from AP controllers (UniFi, Omada, ...) — this is survey/measurement tooling, not a controller integration
- Ship a built-in vendor hardware database — recommendations stay generic (power, channel, position)
- Send telemetry, require an account, or use a server for your data. Projects are local files; diagnostics are a local log file you choose whether to share (see [ADR-0003](docs/adr/0003-local-diagnostic-logging.md)). The one exception: an optional, on-by-default check against GitHub's public release list looks for newer versions on launch — nothing about you or your surveys is sent, and it's a toggle in Settings (see [ADR-0004](docs/adr/0004-update-check-network-call.md))

## Status

Pre-release, developed in the open. The Core engine and WinUI shell both build and run today — see [Building from source](#building-from-source) — but no packaged installer has shipped yet. [CHANGELOG.md](CHANGELOG.md) is the source of truth for what's actually been released, generated automatically from commit history.

Backed by a real automated test suite, with branch coverage on the platform-independent engine enforced at an 80% floor in CI.

## Building from source

Requires the .NET 9 SDK and Windows 10 build 19041 or later.

```powershell
git clone https://github.com/DotifyBIZ/pulsemap.git
cd pulsemap
dotnet build Pulsemap.sln
dotnet run --project src/Pulsemap.App
```

`dotnet test Pulsemap.sln` runs the full suite. Pulsemap needs an interactive Windows session — it won't launch headless.

## Architecture

- **`Pulsemap.App.Core`** — the propagation math, Kriging interpolation, AP placement optimizer, and data models. Zero UI dependency, so it can outlive any one shell.
- **`Pulsemap.App`** — the WinUI 3 shell: native Fluent design, Mica backdrop, unpackaged (no MSIX — see [ADR-0002](docs/adr/0002-installer-innosetup-over-msix.md) for why) so it runs on client machines Dotify doesn't administer.
- WLAN access goes through native `wlanapi.dll` P/Invoke rather than the WinRT `Windows.Devices.WiFi` API, which requires package identity this app deliberately doesn't have.

Architectural decisions, and the alternatives rejected, are recorded as they're made in [`docs/adr/`](docs/adr/).

## Roadmap

- **Phase 1 (MVP) — done:** single floor, walls with optional material + thickness, live scan via chosen adapter, manual test-point placement, heatmap + PDF/CSV/JSON export, AP count + placement suggestion, generic per-band recommendations
- **Phase 2 (Depth) — done:** multi-floor with inter-floor propagation, adaptive Kriging-based test-point suggestion, outdoor areas, historical before/after comparison
- **Phase 3 (Reach):** Linux shell sharing the same Core, vendor-specific config templates, monitor-mode adapter support

## Contributing

Contributions are welcome from day one — see [CONTRIBUTING.md](CONTRIBUTING.md). Agents and contributors working in this repo should read [CLAUDE.md](CLAUDE.md) first; it documents the engineering standards and a few hard-won platform footguns this project has already hit, so they don't get hit twice.

## License

[PolyForm Shield 1.0.0](LICENSE) — free to use, modify, and redistribute for any purpose except building a competing product or service on top of it.

This project follows a [Code of Conduct](CODE_OF_CONDUCT.md).

---

<div align="center">
  <sub>Crafted with ❤️ by <a href="https://dotify.biz">Dotify</a></sub>
</div>
