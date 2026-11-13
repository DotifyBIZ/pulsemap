# Ownership

For the team that owns Pulsemap. The catalog entry is [`catalog-info.yaml`](https://github.com/DotifyBIZ/pulsemap/blob/main/catalog-info.yaml): component `pulsemap`, type `desktop-app`, lifecycle `production`, owned by the TR-ITOperations group.

## What the team is accountable for

- The source and the releases in the `DotifyBIZ/pulsemap` repository. The repository is public.
- Keeping the promise that the app sends no telemetry and needs no account ([ADR 0003](adr/0003-local-diagnostic-logging.md), [ADR 0004](adr/0004-update-check-network-call.md)). Any new network call needs an ADR.
- Security reports. They arrive through GitHub private vulnerability reporting or cert@dotify.biz ([SECURITY.md](https://github.com/DotifyBIZ/pulsemap/blob/main/SECURITY.md)).
- Issues and pull requests from outside contributors.

## Dependencies

| Depends on | Why |
|---|---|
| `component:default/one-design-system` | Colours, type and the primary button, pasted into `App.xaml`. |
| `api:default/github-rest-api` | The update check reads the latest release. |

Nothing in the catalog depends on Pulsemap. It provides no API and uses no database or host. Its build and release run on GitHub Actions.

## Data

Pulsemap holds no data centrally. Surveys, logs and settings stay on each user's machine (see [Admin guide](admin-guide.md#files-and-data)). A survey can contain a floor plan and the names of nearby WiFi networks. The team never receives it.

## Recurring duties

- **Releases:** merging `staging` into `main` cuts a release. semantic-release picks the version from the commit messages and attaches the installer and its checksum.
- **Dependencies:** Dependabot opens pull requests against `staging`. Lockfiles (`packages.lock.json`) are committed and restored in locked mode.
- **Design system:** when the Dotify One Design System changes, paste the new `Dotify.inline.xaml` ([Design tokens](design-tokens.md)).
- **Code signing:** the installer is unsigned, because a signing certificate costs money. That is decided.

## Lifecycle

Not decided in writing yet. The [roadmap](https://github.com/DotifyBIZ/pulsemap/blob/main/README.md#roadmap) lists the phases: 1 and 2 are done; phase 3 (a Linux shell, vendor templates, monitor-mode adapters) is open. Significant decisions are recorded as ADRs.
