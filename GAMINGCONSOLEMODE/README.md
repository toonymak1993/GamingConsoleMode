# GAMINGCONSOLEMODE (companion hub)

Optional **Big Picture–style** companion UI for DeckTop: multi-page hub, lighter startup than **gcmloader** (no single-instance mutex / admin gate in the current design). It does **not** replace **gcmloader** as the primary shell-style launcher.

## When to use which

| | **gcmloader** | **This hub (GAMINGCONSOLEMODE)** |
|---|----------------|----------------------------------|
| Role | Primary DeckTop experience (admin gate, deep integration) | Optional hub for browsing settings pages in a TV-friendly layout |
| Elevation | Runs elevated when packaged that way | Typically **standard user**; do not assume admin |
| Config | `%AppData%\gcmsettings\settings.toml` | **Same file** via shared `DeckTop.Settings` |

## Shared settings

Both apps read and write the same TOML: `DeckTop.Settings` (`AppSettings` in code) backs `Save` / `Load` / defaults. Avoid forked copies of settings logic.

## Elevation

If a feature requires admin (rare in the hub), gate it explicitly. Most hub pages must behave when **not** elevated.

## Baseline keyboard (MKB)

- Root `Grid` uses **XYFocusKeyboardNavigation** so arrow keys move between controls where supported.
- Primary top-bar actions use **TabIndex** order and **UseSystemFocusVisuals** for visible focus.
- Per-page **Esc** / **Enter** handling: extend in each `Page` code-behind as needed; `NavigationView` provides back when **IsBackEnabled** is true.

### IME / search (optional)

For text search fields, use normal `TextBox`/`TextBlock` input paths so **IME** works for non-Latin layouts. If keyboard and gamepad events double-fire, add debouncing at the control that owns search — not in shared shell code until a concrete repro exists.
