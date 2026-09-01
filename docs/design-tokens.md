# Design tokens

Pulsemap adopts Dotify's design-system **palette and type scale only** — UI structure follows native WinUI 3/Fluent patterns (`NavigationView`, native controls), not Dotify's web app shell. See [docs/adr/](adr/) for the platform and packaging decisions this sits alongside.

These values are proven out in the [design proposal](../README.md) mockups — implement them as XAML `Color`/`SolidColorBrush` resources in a shared `ResourceDictionary`, not hand-picked hex values inline in views.

## Color

**Primary (brand red)** — primary actions, active nav state, brand accents. Never used to signal an error.

| Token | Hex |
|---|---|
| primary-50 | `#fef2f2` |
| primary-100 | `#fee2e2` |
| primary-200 | `#fecaca` |
| primary-300 | `#f9a8a8` |
| primary-400 | `#e45a5c` |
| primary-500 | `#d42a2c` |
| primary-600 | `#b41618` — canonical brand red |
| primary-700 | `#971214` |
| primary-800 | `#7d1012` |
| primary-900 | `#6b1113` |
| primary-950 | `#3c0506` |

**Secondary (navy)** — secondary emphasis, informational surfaces, links.

| Token | Hex |
|---|---|
| secondary-50 | `#eeeef8` |
| secondary-100 | `#d9d9ef` |
| secondary-200 | `#b8b7e0` |
| secondary-300 | `#9695cf` |
| secondary-400 | `#6e6cb8` |
| secondary-500 | `#3d3a8e` |
| secondary-600 | `#211f60` — canonical navy |
| secondary-700 | `#1b1950` |
| secondary-800 | `#151341` |
| secondary-900 | `#100e33` |
| secondary-950 | `#090822` |

**Neutrals** — standard Tailwind gray scale. Page background `gray-50` (`#f9fafb`), card surfaces white, borders `gray-200` (`#e5e7eb`).

**Semantic** (state, never the accent) — success green, danger red `#ef4444` (distinct from brand primary), warning amber `#f59e0b`, info navy. Pair every semantic color with text or an icon — never color alone.

## Typography

- **Family:** `"Segoe UI", ui-sans-serif, system-ui, -apple-system, sans-serif` — Segoe UI first, explicitly, for genuine Windows-native rendering rather than relying on `system-ui` resolution.
- **Scale:** xs 12/16, sm 14/20, base 16/24, lg 18/28, xl 20/28, 2xl 24/32, 3xl 30/36 (px, size/line-height).
- **Weight:** 400 body, 500 labels/nav items, 600 headings and buttons, 700–800 reserved for the brand wordmark.
- **Default:** sm (14px) for dense application UI.

## Radius

| Element | Value |
|---|---|
| Inputs, buttons | 6px |
| Dropdowns, popovers | 8px |
| Panels | 12px |
| Cards | 16px |
| Avatars, badges, pills | full |

## Mica backdrop

Standard Mica (`MicaKind.Base`), single main window — see [ADR-0001](adr/0001-winui3-windows-only-platform.md). Backdrop-only: it shows through the title bar and the `NavigationView` pane (no solid fill behind either), never through content surfaces. The canvas, wizard panels, and side panels stay solid per the card/panel tokens above.

## Icons

Outline SVG, stroke-based, 20–24px grid, `stroke-width:1.5`, `currentColor` (or the token color it inherits). No emoji, no filled/dingbat glyphs.
