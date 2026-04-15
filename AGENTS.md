# AGENTS.md — DeckTop

Agent context file for Claude, Copilot, Cursor, and other AI assistants.
Read this before modifying any code in this repo.

---

## What This Project Is

**DeckTop** is a fork of [GameConsoleMode](https://github.com/toonymak1993/GameConsoleMode).

Original project: WinUI 3 shell-replacement app for Windows gaming handhelds (Steam Deck-like experience on PC). Replaces `explorer.exe`, disables UAC, uses gamepad/D-pad as the only navigation input.

**DeckTop goal:** Strip the shell-replacement and controller-centric design. Rebuild as a desktop-first WinUI 3 launcher/productivity shell that works with mouse, keyboard, and optionally a controller.

---

## Critical Context — Read Before Touching Anything

### The codebase is currently in a dangerous transitional state.

The upstream code does the following things that DeckTop intends to remove:

1. **Writes to `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell`** — replaces explorer.exe with itself. If removed incorrectly, Windows will not boot to a shell.
2. **Disables UAC unconditionally at startup** (`gcmloader/gcmloaderwindow.xaml.cs:6522`) — hardcoded registry write to `ConsentPromptBehaviorAdmin=0`.
3. **Uses undocumented `XInputGetStateSecret` P/Invoke** for gamepad input that is invisible to other apps.
4. **Has no keyboard or mouse navigation** — D-pad is the only navigation path in the main UI.

Do not add features or refactor UI until items 1–4 are removed. See [docs/PRIORITY.md](docs/PRIORITY.md).

---

## Project Structure

```
DeckTop/
├── GAMINGCONSOLEMODE/          # WinUI 3 — may become the primary project
│   ├── App.xaml.cs
│   ├── AppSettings.cs          # 308 lines — TOML config
│   └── *.xaml.cs               # UI pages
│
├── gcmloader/                  # WinUI 3 — current primary entry point
│   ├── App.xaml.cs             # Entry: admin check, single-instance mutex
│   ├── gcmloaderwindow.xaml.cs # 12,654 lines — MONOLITH (shell, gamepad, services, UI)
│   ├── AppSettings.cs          # 130 lines — diverged from GAMINGCONSOLEMODE version
│   └── LosslessScalingController.cs
│
├── TaskHelper/                 # .NET Framework console — admin helper
│   └── Program.cs              # UAC toggle, scheduled tasks
│
├── wingamepad/                 # WPF — pure gamepad monitor, fires hotkeys
├── OverlayWindow/              # WPF — gamepad-driven overlay
├── gcminstaller/               # Installer/setup
│
└── docs/
    ├── AUDIT.md                # Full architectural audit (2026-04-14)
    └── PRIORITY.md             # Ordered pre-UI work plan
```

---

## Architecture Notes

### Entry Point
`gcmloader/App.xaml.cs:23` → `gcmloaderwindow.xaml.cs` constructor → `SetupGamepad()` + `Start()` → `SetupSystemAndDesktopAsync()` → `ConsoleModeToShell()`

### The Monolith
`gcmloader/gcmloaderwindow.xaml.cs` is 12,654 lines and owns: shell registration, gamepad polling (3 loops), D-pad UI navigation, service enable/disable, UAC control, launcher integration, keyboard redirect, and exit choreography. It must be decomposed before UI work.

### Config System Bug
Settings keys referenced throughout code but **not initialized in `initialconfig()`**. If `settings.toml` is missing, silent failures cascade. Two separate `AppSettings.cs` files exist — they are not in sync. Do not add new settings keys without adding defaults first.

### Two WinUI 3 Projects
`GAMINGCONSOLEMODE` and `gcmloader` overlap. Their relationship is unclear. Do not assume either is authoritative until the project structure decision is made (see PRIORITY.md step 1).

---

## What To Do / Not Do

### DO
- Read `docs/AUDIT.md` and `docs/PRIORITY.md` before starting any task
- Work in the priority order defined in `docs/PRIORITY.md`
- Add defaults for every settings key you reference
- Use WinUI 3 `KeyDown`, `PointerPressed`, `FocusManager` for navigation
- Extract service logic out of `gcmloaderwindow.xaml.cs` into dedicated classes with interfaces

### DO NOT
- Write to `HKLM\Winlogon\Shell` — ever
- Disable UAC
- Add or extend gamepad/XInput code — it's scheduled for removal
- Modify `BackToWindows()` exit sequence without reading the full 19-step flow first
- Add new UI before navigation works with mouse and keyboard
- Assume `AppSettings.Load<T>(key)` returns a valid value — check for defaults

---

## Key File Locations

| What | File | Lines |
|------|------|-------|
| Shell write | `gcmloader/gcmloaderwindow.xaml.cs` | 5349–5425 |
| Shell restore | `gcmloader/gcmloaderwindow.xaml.cs` | 4751–4757 |
| UAC disable (hardcoded) | `gcmloader/gcmloaderwindow.xaml.cs` | 6522 |
| UAC method | `gcmloader/gcmloaderwindow.xaml.cs` | 5313–5348 |
| Gamepad loops start | `gcmloader/gcmloaderwindow.xaml.cs` | 9302–9304 |
| XInputGetStateSecret P/Invoke | `gcmloader/gcmloaderwindow.xaml.cs` | 9325–9345 |
| D-pad navigation (UI) | `gcmloader/gcmloaderwindow.xaml.cs` | 10344–10396 |
| Exit sequence | `gcmloader/gcmloaderwindow.xaml.cs` | 4730–4814 |
| Config defaults (incomplete) | `gcmloader/AppSettings.cs` | — |
| Admin entry point | `gcmloader/App.xaml.cs` | 23–82 |

---

## Tech Stack

- **WinUI 3** (.NET 8, `net8.0-windows10.0.19041.0`)
- **XAML** for UI
- **TOML** (`Tomlyn`) for config at `%APPDATA%\gcmsettings\settings.toml`
- **SharpDX.XInput** — gamepad (to be removed)
- **System.Management** — WMI service control
- **CommunityToolkit.WinUI** — controls and animations
- **NAudio** + **AudioSwitcher** — audio device management
- **Flurl.Http** — HTTP client
- **Microsoft.Web.WebView2** — embedded browser

---

## Branch / PR Guidance

- Main branch: `main`
- No force pushes to `main`
- PRs should reference a step from `docs/PRIORITY.md` in the description
- Each PR should be scoped to one priority step — do not bundle shell removal with config fixes
