# AGENTS.md — DeckTop

Agent context file for Claude, Copilot, Cursor, and other AI assistants. Read this before modifying any code in this repo.

---

## What This Project Is

**DeckTop** is a fork of [GameConsoleMode](https://github.com/toonymak1993/GameConsoleMode).

**DeckTop goal:** A desktop-first WinUI 3 launcher experience (mouse, keyboard, optional controller). Shell replacement (`explorer.exe` swap), unconditional UAC tampering, and Winlogon `Shell` registry writes are **out of scope** and have been removed per `docs/PRIORITY.md`.

---

## Project Structure (current)

```
DeckTop/
├── DeckTop.Settings/           # Shared TOML: %AppData%\gcmsettings\settings.toml
│   └── AppSettings.cs
├── GAMINGCONSOLEMODE/          # WinUI 3 companion hub (optional “Big Picture–style” UI)
│   ├── README.md               # Role vs gcmloader, elevation, shared config
│   └── AppSettings.cs          # Thin WinUI facade → DeckTop.Settings + restart helpers
├── gcmloader/                  # Primary WinUI 3 entry (mutex, admin gate, main shell)
│   ├── App.xaml.cs
│   ├── GlobalUsings.cs         # global using AppSettings = DeckTop.Settings.AppSettings
│   ├── gcmloaderwindow.xaml.cs # Main window (still large; services extracted under Services/)
│   └── Services/               # ShellManagement, LauncherService, Registry, deferred init, etc.
├── TaskHelper/                 # Admin helper for scheduled tasks (not launcher logic)
├── wingamepad/                 # Legacy WPF — see README (deprecation path)
├── OverlayWindow/              # Legacy WPF overlay — see README
└── docs/
    ├── AUDIT.md
    ├── PRIORITY.md
    └── HANDOFF_QWEN.md
```

---

## Architecture Notes

### Entry points

- **`gcmloader`**: Single-instance mutex + admin check → `MainWindow` — primary DeckTop shell.
- **`GAMINGCONSOLEMODE`**: Companion hub; same `settings.toml` via **DeckTop.Settings**; typically non-admin; does not replace `gcmloader`.

### Shared configuration

Single implementation: **`DeckTop.Settings.AppSettings`** (`Save` / `Load` / `Delete` / `initialconfig` / `RegenerateConfiguration`).  
`GAMINGCONSOLEMODE.AppSettings` forwards TOML calls and adds **WinUI-only** `FirstStart(Window)`, `RestartApplication()`, `MessageBoxHelper`.

### Services extracted from the monolith

`gcmloader/Services/` includes launcher orchestration (**`ILauncherService` / `LauncherService`**), shell/explorer helpers, registry helpers, optional GitHub update check (`github_update_check` key), deferred post–first-paint hooks, etc.

### Controller navigation

Setting `use_controller_navigation` in `settings.toml` defaults to **false** (desktop-first). When **true**, legacy gamepad UI paths and related loops may activate — restart to apply.

---

## What To Do / Not Do

### DO

- Prefer **`DeckTop.Settings`** for any new settings keys; add defaults in **`AddDefaultSettingsKeys`** in one place.
- Keep **WinUI** out of pure service classes (pass `LauncherShell` delegates / hooks from the window).
- Build **`gcmloader`** and **`GAMINGCONSOLEMODE`** with `-p:Platform=x64` if the default solution mix hits RID issues.

### DO NOT

- Reintroduce **Winlogon Shell** replacement or **`explorer.exe`** swap as a product feature.
- Reintroduce **UAC policy** toggles from the app.
- Duplicate **`AppSettings`** / TOML logic in multiple projects.
- Edit the user’s **plan files** in `.cursor/plans/` when executing an attached plan (read-only).

---

## Key locations

| Area | Location |
|------|----------|
| Shared TOML | `DeckTop.Settings/AppSettings.cs` |
| Launcher orchestration | `gcmloader/Services/LauncherService.cs` |
| Exit cleanup | `BackToWindows()` in `gcmloaderwindow.xaml.cs` |
| Hub product doc | `GAMINGCONSOLEMODE/README.md` |

---

## Tech stack

- WinUI 3 (.NET 8, `net8.0-windows10.0.19041.0`)
- Tomlyn (`Tomlyn`) for config
- See individual `.csproj` files for package references

---

## Learned user preferences

- When executing an attached implementation plan, **do not edit the plan file**; implement in the repo only.
- Name feature branches with the `cursor/` prefix when using tool-driven workflows.
- Split large work into passes with bounded scope.
