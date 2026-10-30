# Design tokens

Pulsemap uses the [Dotify One Design System](https://repo.dotify.biz/one/design-system/) for colour, type, radius and the primary button. UI structure follows native WinUI 3/Fluent patterns (`NavigationView`, native controls), not Dotify's web app shell. See [docs/adr/](adr/) for the platform and packaging decisions this sits alongside.

## Where the values live

`App.xaml` holds the design system's `xaml/Dotify.inline.xaml`, pasted inside `MergedDictionaries` after `XamlControlsResources`. It is not a separate merged file, because this environment's XAML compiler fails on a standalone `ResourceDictionary`-rooted file (see `CLAUDE.md`). To update, paste the current file over the inner `ResourceDictionary`. Never edit the values in place, and never write a hex value in a view.

Montserrat (Regular and SemiBold, SIL OFL) ships in `Assets/Fonts/`.

## Which resource to use

| Use | Resource |
|---|---|
| Page background | `{ThemeResource DotifyColorSurfaceBrush}` |
| Card and panel background | `{ThemeResource DotifyColorSurfaceRaisedBrush}`, or `{StaticResource DotifyCardStyle}` on a `Border` |
| Borders and dividers | `{ThemeResource DotifyColorBorderBrush}` |
| Headings and body text | `{ThemeResource DotifyColorOnSurfaceBrush}` |
| Secondary text | `{ThemeResource DotifyColorOnSurfaceMutedBrush}` |
| Hints and timestamps | `{ThemeResource DotifyColorOnSurfaceFaintBrush}` |
| Status text (diagnostics) | `DotifyColorErrorTextBrush`, `DotifyColorWarningTextBrush`, `DotifyColorSuccessTextBrush` |
| Font sizes | `{StaticResource DotifyTextXs}` … `DotifyText3xl` (12, 14, 16, 18, 20, 24, 30) |
| Corner radius | `{StaticResource DotifyRadius}` (15) |
| The one primary action on a page | `Style="{StaticResource DotifyPrimaryButtonStyle}"` |

- **Themes:** content follows the Windows light or dark setting. Every colour above is a theme brush, so use `{ThemeResource}` for brushes and `{StaticResource}` for sizes and styles.
- **Primary button:** `DotifyPrimaryButtonStyle` has its own hover and pressed states. Don't add `Button.Resources` overrides or set the content's `Foreground`.
- **Status:** pair every status colour with text or an icon, never colour alone. Brand red is for the primary action, never an error.
- **Floor plan:** heatmap, access point and wall colours in `FloorPlanCanvas` are data colours, not design tokens.

## Mica backdrop

Standard Mica (`MicaKind.Base`), single main window — see [ADR-0001](adr/0001-winui3-windows-only-platform.md). Backdrop-only: it shows through the title bar and the `NavigationView` pane (no solid fill behind either), never through content surfaces. The canvas, wizard panels, and side panels stay solid.

## Icons

Outline SVG, stroke-based, 20–24px grid, `stroke-width:1.5`, `currentColor` (or the token color it inherits). No emoji, no filled/dingbat glyphs.
