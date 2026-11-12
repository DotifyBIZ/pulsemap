# Admin guide

For whoever installs, packages or supports Pulsemap on Windows machines. Pulsemap has no server, so there is nothing to host.

## Requirements

- Windows 10 build 19041 (version 2004) or later, 64-bit.
- An interactive desktop session. The app does not start headless.
- A WiFi adapter and the Windows WLAN AutoConfig service, for scanning.
- No administrator rights.

## Install

The installer is `PulsemapSetup-<version>.exe`, attached to each [GitHub Release](https://github.com/DotifyBIZ/pulsemap/releases) with a `.sha256` checksum file. It is built with Inno Setup around a self-contained publish, so no .NET runtime is needed on the machine ([ADR 0002](adr/0002-installer-innosetup-over-msix.md)).

- It installs for the current Windows user only, into `%LocalAppData%\Programs\Pulsemap`.
- It adds a Start Menu entry. A desktop shortcut is an optional task, off by default.
- The installer is **not code-signed**, so Windows SmartScreen warns. Compare the file's SHA-256 with the published `.sha256` file before you run it.
- Inno Setup's standard command-line switches (for example `/SILENT`) apply.

## Update and uninstall

- Run a newer installer over the old one. The application ID is fixed, so it upgrades in place. Surveys are not touched.
- Uninstall through Windows Settings, Apps. Surveys, logs and settings are not deleted.
- The app checks GitHub once on launch for a newer release and shows a banner on Home. This is the only update signal ([ADR 0004](adr/0004-update-check-network-call.md)).

## Settings

Pulsemap has no environment variables or configuration files for admins to manage. There are two user settings, stored in `%LocalAppData%\Pulsemap\settings.json`:

| Key | Default | Meaning |
|---|---|---|
| `CheckForUpdatesEnabled` | `true` | Check GitHub for a newer release on launch. Also a toggle in the app's Settings. |
| `HasSeenWorkspaceOnboarding` | `false` | Whether the first-time Workspace tour was shown. |

A missing or unreadable file falls back to these defaults. The display language follows Windows (English and Polish).

## Network

The app makes two kinds of outbound connection:

- **Update check:** `GET https://api.github.com/repos/DotifyBIZ/pulsemap/releases/latest`, once on launch. Nothing about the user or surveys is sent. Block it at the firewall, or turn it off in Settings, and the app works the same.
- **WiFi diagnostics:** a ping to the user's own gateway and one DNS lookup, only while the user runs diagnostics.

## Files and data

| What | Where |
|---|---|
| Surveys | `Documents\Pulsemap\Surveys\*.pulsemap` (zip files) |
| Logs | `%LocalAppData%\Pulsemap\Logs\`, one file per day, deleted after 30 days |
| Settings | `%LocalAppData%\Pulsemap\settings.json` |

Back up the survey folder like any other documents. Copying a `.pulsemap` file to the same folder on another machine makes it appear there. Logs are local and never sent; users attach them to a bug report if they choose.

## Common failures

| Symptom | Cause and fix |
|---|---|
| "Windows needs Location access" | Windows requires Location access for any app that scans WiFi. Allow desktop apps in Settings, Privacy & security, Location. |
| "No WiFi adapter found" | No WiFi hardware, adapter disabled, or the WLAN AutoConfig service is stopped. |
| 6 GHz never appears | The adapter or driver cannot see that band. The app shows it as unmeasured. |
| Crash or odd behaviour | Read the newest file in the Logs folder. Report issues on [GitHub](https://github.com/DotifyBIZ/pulsemap/issues). |

## Building the installer

Releases are built by the `release.yml` workflow when `staging` is merged into `main`. To build locally, see [Developer guide](developer-guide.md#cicd).
