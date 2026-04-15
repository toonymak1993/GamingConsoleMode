# OverlayWindow

Legacy **WPF** transparent overlay used in older GCM flows. It is **not** the primary DeckTop UI (`gcmloader` / WinUI 3).

- Keep changes minimal; consider removal if nothing in the main shell depends on it.
- The local `AppSettings.cs` here is a forked copy — prefer aligning with `%AppData%\gcmsettings\settings.toml` via shared `DeckTop.Settings` if this project is maintained long term.
