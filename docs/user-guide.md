# Pulsemap User Guide

Pulsemap is a WiFi site survey and planning tool. It runs entirely on your own machine: no account, no server, no cloud sync. This guide covers installing it, running a survey end to end, and what every screen and button actually does.

If you're looking for build instructions or how the code works instead, see the [Developer Guide](developer-guide.md).

## Contents

- [Installing Pulsemap](#installing-pulsemap)
- [Updating and uninstalling](#updating-and-uninstalling)
- [The shell: Home, Surveys, Diagnose, Settings](#the-shell-home-surveys-diagnose-settings)
- [Creating a survey](#creating-a-survey)
- [The Workspace](#the-workspace)
  - [Tools](#tools)
  - [Floors and outdoor areas](#floors-and-outdoor-areas)
  - [Zooming and panning](#zooming-and-panning)
  - [Coverage tab](#coverage-tab)
  - [Suggestions tab](#suggestions-tab)
  - [Adapter tab](#adapter-tab)
  - [Undo](#undo)
  - [Snapshots and historical comparison](#snapshots-and-historical-comparison)
  - [Exporting](#exporting)
- [Standalone WiFi diagnostics](#standalone-wifi-diagnostics)
- [Settings](#settings)
- [Where your data lives](#where-your-data-lives)
- [Privacy — what leaves your machine](#privacy--what-leaves-your-machine)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Getting help](#getting-help)

## Installing Pulsemap

1. Go to the [Releases page](https://github.com/DotifyBIZ/pulsemap/releases) and download `PulsemapSetup-<version>.exe` from the latest release.
2. Run it. **Windows SmartScreen will likely warn you** — the installer isn't code-signed yet (see the [FAQ](#faq) for why). Click **More info** → **Run anyway** if you're comfortable doing so; you downloaded it from the official GitHub Releases page, so this is expected, not a sign anything is wrong.
3. The installer needs no administrator rights — it installs for your Windows user only, under `%LocalAppData%\Programs\Pulsemap`. You'll get the option to add a desktop shortcut (unchecked by default) and a Start Menu entry is created either way.
4. Launch Pulsemap from the Start Menu or desktop shortcut.

**Requirements:** Windows 10, build 19041 (version 2004, "May 2020 Update") or later, 64-bit. Pulsemap needs an interactive desktop session — it won't run under a remote/headless setup with no display.

## Updating and uninstalling

Pulsemap checks GitHub's public release list once, on launch, and shows a dismissible banner on Home if a newer version exists — click **View release** to go straight to it. This check is on by default and can be turned off in [Settings](#settings); either way, updating means downloading and running the newer installer the same way you installed the first time. It installs over the previous version in place — your surveys are separate files under your Documents folder and are untouched by an update.

To uninstall, use Windows' **Settings → Apps** (or the classic Control Panel **Programs and Features**) and remove Pulsemap the same way as any other app, or run the uninstaller directly from the Start Menu group. Uninstalling does not delete your saved surveys.

## The shell: Home, Surveys, Diagnose, Settings

Four sections, in the left-hand navigation rail:

- **Home** — a dashboard: a greeting, a **New Survey** button, and your three most-recently-modified surveys. If you haven't created one yet, it tells you so and points at the New Survey button.
- **Surveys** — every survey you've created, with no limit, each with **Rename** and **Delete** (delete asks you to confirm — it removes the file and can't be undone). Also has its own **New Survey** button.
- **Diagnose** — a standalone "why is my WiFi slow" tool that needs no survey at all. See [Standalone WiFi diagnostics](#standalone-wifi-diagnostics).
- **Settings** — language, the diagnostic log folder, and the update-check toggle. See [Settings](#settings).

Clicking a survey card on Home or Surveys opens it in the [Workspace](#the-workspace).

## Creating a survey

Click **New Survey** from Home or Surveys. The wizard is five steps; a step counter under the title shows where you are, and **Back**/**Next** move between them.

**1. Basics** — a name (required), an optional site description, and what kind of survey this is:
- **New deployment** — there's no WiFi here yet; you're planning where access points should go.
- **Existing network** — auditing a network that's already running. You can optionally name the SSID being audited, which lets the guided measurement walk (later, in the Workspace) capture that network's own signal strength at each point, not just background noise.

**2. Floor plan** — how you want to lay out the space:
- **Room list** — describe rooms by name, width, and length (meters). Pulsemap arranges them side by side and draws perimeter walls automatically. Good for a quick, approximate layout with no existing drawing to work from.
- **Image or PDF** — upload an existing floor plan (PNG, JPG, or PDF, up to 100MB) and draw over it in the Workspace. If the PDF has more than one page, you're asked which page the floor plan is actually on. You also set **pixels per meter** — how many pixels in the image correspond to one real-world meter — so distances and RF math come out correct; check your plan's printed scale or measure a known dimension (like a door width) against the image to figure this out.

**3. Building details** — a default wall material (drywall, standard or low-E glass, wood, brick, concrete, or reinforced concrete) applied to any walls the room-list layout generates automatically. You can change individual walls later in the Workspace regardless of which style you picked in step 2.

**4. Adapter & bands** — which frequency bands this survey covers: 2.4 GHz, 5 GHz, 6 GHz, pick any combination (at least one). This drives which bands get heatmaps, AP suggestions, and measurement prompts. Picking your actual WiFi adapter happens later, inside the Workspace's Adapter tab — this step is only about which bands the survey cares about.

**5. Review** — a summary of everything above, with **Create Survey** to save it and open the Workspace.

## The Workspace

This is where you draw, measure, and get recommendations. The main area is a floor-plan canvas; a panel on the right holds three tabs (Coverage, Suggestions, Adapter). The toolbar above the canvas holds the tools, the floor switcher, and the band selector.

If it's your first time in the Workspace, a short guided tour points out each tool — skip it any time, or revisit the same explanations later via the **Tool Help** button.

### Tools

Five tools, one active at a time — click one to switch, the active one stays highlighted:

- **Select** — click a wall to select it (it highlights blue; hovering over a selectable wall highlights it lighter blue first, so you can see what's clickable before you click). Select multiple walls this way, then use the material/thickness controls that appear to batch-apply a material to all of them at once. Clicking a test point instead asks to **recapture** it — rescanning from that exact spot and replacing its old reading, useful if conditions changed or the original reading looked off. On an outdoor area, dragging its dashed-line edge resizes it; dragging inside it moves the whole area.
- **Add Test Point** — click anywhere to drop a manual test point with no measurement yet (gray, until something captures a reading there — the guided walk or a Select-tool recapture both fill it in).
- **Draw Wall** — click one end, then the other, to draw a wall between them. The first click shows a small blue dot marking where the wall will start from; press **Escape** to cancel a wall in progress before the second click.
- **Delete** — click near a wall, test point, or access point to remove it. An **Undo** bar appears right after — see [Undo](#undo).
- **Diagnose** — click any point on the plan to compare this machine's actual live WiFi signal there against what the survey's model predicted for that spot. Useful for sanity-checking the model against reality, or investigating a specific dead spot. Needs an adapter picked on the Adapter tab first.

### Floors and outdoor areas

The floor/area switcher (a dropdown in the toolbar) lists every floor and outdoor area in the survey. Every survey starts with one; add more with **+ Add Floor** (name it, and check the box if it's an outdoor area rather than an indoor floor — parking lots, courtyards, anything with no walls). **Rename Floor** and **Delete Floor** work on whichever one is currently selected; deleting one removes its walls, test points, and access points along with it and asks you to confirm first. The last remaining floor can't be deleted — every survey needs at least one.

Indoor floors are stacked at the same coordinate origin for the purpose of estimating how much signal leaks between them (a flat penalty per floor crossed) — this is a simplification, not true 3D modeling, but it means an AP on the floor below shows up as weaker-but-present interference upstairs rather than being ignored entirely. Outdoor areas don't participate in that — their coverage is calculated independently using free-space propagation (no walls to attenuate through).

### Zooming and panning

Scroll bars pan around a large plan; a **+ / − / reset** control in the bottom-right corner of the canvas zooms from 25% to 400%, and pinch-to-zoom or Ctrl+scroll-wheel work too if your input device supports them.

### Coverage tab

Shows the color legend for the heatmap painted on the canvas (green = excellent, through yellow-green, gold, orange, to red = poor, at the thresholds shown in the legend itself). Hover anywhere over a colored cell on the canvas to see its exact predicted signal in dBm. The heatmap is **predictive** — computed from placed access points and the propagation model, not from real measurements — until enough test points exist for the model to actually validate itself.

### Suggestions tab

Two independent things live here:

- **Access points** — click **Suggest Placements** to have Pulsemap propose how many APs you need, where to put them, and what channel/power to run each band at, aiming for reliable coverage (−67dBm or better) across the floor. It accounts for interference your guided walk has already measured, and for channels already used by APs on other floors. Running it again after you've already got suggestions asks for confirmation first, since it replaces them — anything you've manually placed or dragged yourself is always kept, never touched.
- **Test points (guided measurement walk)** — click **Start Guided Walk** and Pulsemap picks a sequence of real-world points worth walking to and measuring. Walk to the coordinates shown, click **Confirm Arrival & Capture** to scan from there (or **Skip** to move on without measuring that one), and repeat until done, or **Cancel Walk** to stop early — points already captured are kept either way. Closing Pulsemap mid-walk and reopening the same survey resumes exactly where you left off. Once at least two measurements exist for the current band, later points in the walk are re-ordered toward wherever the model is least certain, rather than a fixed grid — so the walk gets smarter as you go. This needs a WiFi adapter picked on the Adapter tab.

### Adapter tab

Pick which of this machine's WiFi adapters to scan with, then **Scan** to see nearby networks (SSID, signal, channel, band). Windows requires **Location access** to return WiFi scan results to any app — if you see a message about that, open Windows Settings → Privacy → Location and allow it (a shortcut button appears right there when this happens). If no adapter shows up at all, your machine may not have WiFi hardware, or the Windows WLAN AutoConfig service isn't running.

### Undo

A single level of undo, specifically for the Delete tool — the one action in the Workspace with no other confirmation. Delete something and an info bar appears with an **Undo** button; it stays until your *next* delete (which replaces it) or until you switch floors (since undoing something on a floor you've left would be confusing). Everything else — adding a wall, a test point, running Suggest Placements — either asks for confirmation up front or is trivially reversible by hand.

### Snapshots and historical comparison

**Save Snapshot** freezes the current state of every floor's walls, test points, and access points under a label you choose (e.g. "Before AP upgrade") — the floor plan image itself isn't duplicated, so a snapshot always displays over whatever plan image is currently set. **Compare** (enabled once you have at least one snapshot) opens a side-by-side view: pick "Current" or any saved snapshot independently for the left and right side, along with a floor and band, and see both heatmaps at once. Each side can be deleted from there too, with confirmation.

### Exporting

The **Export** menu offers four formats:
- **Test points (CSV)** and **Access points (CSV)** — raw measurement/placement data, one row per band per point, for spreadsheet analysis.
- **Survey data (JSON)** — the entire survey as structured data.
- **Coverage report (PDF)** — a printable summary: survey details, per-floor statistics, and the AP placement/channel recommendations.

## Standalone WiFi diagnostics

The **Diagnose** nav item is a general troubleshooting tool independent of any survey — good for "why is my WiFi slow right now" on this machine. Pick an adapter, click **Run diagnostics** for a one-time snapshot, or **Start monitoring** for a live, continuously-updating view (signal, link speed, ping) sampled every 5 seconds. Findings are plain-language: weak signal, a suspiciously slow negotiated link speed on 5/6GHz, DNS failures or slowness, high or absent gateway ping. If your machine has no WiFi hardware or the WLAN service isn't running, the page tells you that instead of just sitting empty.

Workspace's own **Diagnose tool** (in the toolbar, inside a survey) is a related but different thing — it additionally compares the live reading against what that specific survey's model predicted at the point you clicked, which the standalone page can't do since it has no survey to predict from.

## Settings

- **Language** — Pulsemap follows Windows' own display language setting rather than offering its own switch; change it in Windows Settings and Pulsemap follows. English and Polish are supported today.
- **Diagnostics** — **Open Logs Folder** opens the local error log Pulsemap keeps for troubleshooting. Nothing is ever sent automatically; if you hit a problem and want help, this is what to attach or paste from.
- **Updates** — toggle the launch-time GitHub release check on or off.

## Where your data lives

- **Surveys** — `Documents\Pulsemap\Surveys\*.pulsemap` (each is a zip file containing your survey data plus any floor-plan image/PDF you uploaded). Back these up like any other document; copying one to another machine's same folder makes it show up there too.
- **Logs** — `%LocalAppData%\Pulsemap\Logs\` (one file per day).
- **Settings** — `%LocalAppData%\Pulsemap\settings.json`.

None of this is synced anywhere by Pulsemap itself.

## Privacy — what leaves your machine

Pulsemap is local-first: no account, no server, your survey data never leaves your machine unless you export or copy it yourself. Two things do reach the network, and only these:

1. **Update check** — one request to GitHub's public release list on launch, carrying nothing about you or your surveys. On by default, toggle it off in Settings.
2. **WiFi diagnostics** — pinging your own router and timing one DNS lookup, only while you're actively running the Diagnose page or Workspace's Diagnose tool. This is inherent to what "check my network health" means and isn't separately toggleable — running the check is the consent.

Nothing else. See [SECURITY.md](../SECURITY.md) to report a vulnerability.

## Troubleshooting

**"Windows needs Location access to show WiFi scan results"** — this is a Windows OS requirement for any app doing a WiFi scan, not a Pulsemap choice. Use the **Open Location Settings** button that appears, or go to Windows Settings → Privacy & security → Location and allow desktop apps to access it.

**"No WiFi adapter found"** — your machine either has no WiFi hardware, the adapter is disabled, or the Windows WLAN AutoConfig service isn't running. Check Device Manager and that WiFi is turned on.

**A band (e.g. 6 GHz) never shows up in scans** — some adapters and drivers simply can't see certain bands. Pulsemap shows that band as *unmeasured*, never as a false zero signal, so you'll know it's a hardware limitation rather than "no coverage there."

**SmartScreen warning on install** — expected; see [FAQ](#faq).

**Something crashed or misbehaved** — check the log file (Settings → Open Logs Folder) for details, and consider [reporting it](#getting-help) with that attached.

## FAQ

**Why does Windows warn me when I install this?**
The installer isn't code-signed — that costs money Pulsemap's project doesn't currently spend on, and SmartScreen warns about any unsigned installer regardless of what it actually does. It's an installer built from this project's own public, open-source code (see the [Developer Guide](developer-guide.md) if you'd rather build it yourself from source and skip the download entirely).

**Does Pulsemap need an internet connection?**
No — every core feature (surveying, measuring, exporting) works fully offline. The only network activity is the optional update check and the WiFi diagnostics ping/DNS lookup described [above](#privacy--what-leaves-your-machine).

**Can I run this on Linux or macOS?**
Not currently — Pulsemap is Windows-only for now (see [ADR-0001](adr/0001-winui3-windows-only-platform.md) for why, if you're curious). A shared cross-platform core is on the long-term roadmap in the [README](../README.md#roadmap).

**Can Pulsemap read data from my UniFi/Omada/other AP controller?**
No, and this is deliberate — Pulsemap is survey and planning tooling, not a controller integration. Recommendations (power, channel, placement) stay generic rather than vendor-specific.

**What's the coverage heatmap actually based on?**
A log-distance path-loss model per band, with per-material wall attenuation where you've specified wall materials (a generic flat penalty otherwise), plus a flat penalty for signal crossing between floors. It's predictive from where APs are placed — walking real measurements narrows down where the model is uncertain (via the guided walk) and feeds real interference data into channel/placement recommendations, but the heatmap color itself stays a prediction, not an interpolation of your measurements. Use the Diagnose tool to spot-check specific points against reality.

## Getting help

- **Bugs and feature requests:** [open a GitHub issue](https://github.com/DotifyBIZ/pulsemap/issues).
- **Security vulnerabilities:** do **not** open a public issue — see [SECURITY.md](../SECURITY.md) for private reporting.
- **Questions:** open an issue tagged `question`.
