# DeckTop Architectural Audit

> Forked from GameConsoleMode (WinUI 3 / C# shell replacement).
> Audit performed 2026-04-14. Read-only — no files modified.

---

## 1. SHELL REGISTRATION

| What | File | Lines | Detail |
|------|------|-------|--------|
| Shell swap → gcmloader | `gcmloader/gcmloaderwindow.xaml.cs` | 5349–5425 | `ConsoleModeToShell()` sets `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon\Shell` to own exe path |
| Shell restore → explorer.exe | `gcmloader/gcmloaderwindow.xaml.cs` | 4751–4757 | Inside `BackToWindows()` |
| Static restore helper | `gcmloader/gcmloaderwindow.xaml.cs` | 5611–5616 | `winpart()` static method, also writes `explorer.exe` back |
| Explorer re-launch | `gcmloader/gcmloaderwindow.xaml.cs` | 4771–4777 | Kill all `explorer.exe` → 500ms delay → restart |

**Registry:** HKLM exclusively. Always requires elevation. Silent fail if not admin (line 5358, debug log only).

**Exit sequence** (`BackToWindows()`, ~19 steps): registry reset → taskbar restore → service re-enable → Shell → startup apps → kill explorer → restart explorer → Steam video → DisplayFusion → UAC restore → `Environment.Exit(0)`. Deeply entangled with UI and service state.

- **Risk: HIGH**
- **Action: REMOVE** `ConsoleModeToShell()`. Rewrite `BackToWindows()` as simple cleanup + exit.

---

## 2. UAC HANDLING

| Location | Lines | What |
|----------|-------|------|
| `gcmloader/gcmloaderwindow.xaml.cs` | 6522 | `uac("off")` — **hardcoded**, unconditional, inside `SetupSystemAndDesktopAsync()` |
| `gcmloader/gcmloaderwindow.xaml.cs` | 5313–5348 | `uac(string art)` method — sets `ConsentPromptBehaviorAdmin` via HKLM |
| `gcmloader/gcmloaderwindow.xaml.cs` | 4802–4810 | Exit: reads `AppSettings.Load<bool>("uac")` to decide re-enable |
| `TaskHelper/Program.cs` | 35–36 | `--uac=enable/disable` CLI args — duplicate implementation |

**Hardcoded disable at startup. Config-driven restore on exit — but `"uac"` key has no default in `initialconfig()` (silent bug).**

Load-bearing: No. Single line at 6522.

- **Risk: LOW**
- **Action: REMOVE** — delete line 6522, remove UAC restore in exit, delete `uac()` method

---

## 3. CONTROLLER ASSUMPTIONS

| Location | Lines | What |
|----------|-------|------|
| `gcmloader/gcmloaderwindow.xaml.cs` | 9302–9304 | Three gamepad polling loops: Xbox XInput, PlayStation, PlayStation Edge |
| `gcmloader/gcmloaderwindow.xaml.cs` | 9325–9345 | **Undocumented** `XInputGetStateSecret` P/Invoke — reads controller state hidden from other apps |
| `gcmloader/gcmloaderwindow.xaml.cs` | 10344–10396 | D-pad/stick directly controls main UI navigation — **no keyboard/mouse fallback** |
| `gcmloader/gcmloaderwindow.xaml.cs` | 9297–9300 | State arrays sized for 10 controllers |
| `wingamepad/MainWindow.xaml.cs` | entire file | 100% controller-only WPF app, no traditional UI |
| `OverlayWindow/MainWindow.xaml.cs` | 30, 53 | Expander-based nav via D-pad, `currentExpanderIndex` state |

173+ references to `GamepadButtonFlags`, `XInputGetStateSecret`, `SharpDX.XInput` in main window.

No keyboard/mouse input handlers in navigation code. D-pad is not a fallback — it is the only path.

- **Risk: HIGH**
- **Action: REMOVE** gamepad loops and navigation. Replace with standard WinUI 3 keyboard + mouse patterns.

---

## 4. ARCHITECTURE MAP

### Projects

| Project | Type | Purpose |
|---------|------|---------|
| `gcmloader` | WinUI 3 | **Main shell** — 12,654-line `gcmloaderwindow.xaml.cs` monolith |
| `GAMINGCONSOLEMODE` | WinUI 3 | Parallel project, separate `AppSettings`, overlapping purpose |
| `TaskHelper` | .NET Framework console | Admin helper: UAC, scheduled tasks, service control |
| `wingamepad` | WPF | Pure gamepad monitor → fires hotkeys |
| `OverlayWindow` | WPF | Gamepad-navigated overlay |

### Entry Point & Startup Sequence

```
gcmloader/App.xaml.cs:23  OnLaunched
  → admin check (restart-as-admin if needed)
  → MainWindow() constructor (gcmloaderwindow.xaml.cs:2469)
      → InitializeComponent()
      → boot overlay, VRR disable, taskbar hiding loop
      → Loaded event
          → PlayStartupVideo()
              → SetupGamepad()           ← starts 3 async gamepad loops
              → Start()
                  → HideTaskbar, ParkMouseCursor, HWND_TOPMOST
                  → SetupSystemAndDesktopAsync() [background task]
                      → uac("off")       ← UAC DISABLED HERE (line 6522)
                      → KeyboardRedirector.EnableRedirect()  ← HKLM osk.exe redirect
                      → services/tools startup (Boilr, DisplayFusion, CSS loader, etc.)
                  → [wait for video]
                  → TransitionToMainUI()
                  → StartConfiguredLauncherAsync()
                  → ConsoleModeToShell() ← HKLM Shell written HERE
```

### Main Services & Ownership

| Service | File | Lines | Owns |
|---------|------|-------|------|
| Shell registry management | `gcmloader/gcmloaderwindow.xaml.cs` | 4730–5425 | HKLM Winlogon\Shell read/write |
| Gamepad input processing | `gcmloader/gcmloaderwindow.xaml.cs` | 9159–10500+ | XInput polling, button mapping, D-pad nav |
| UI navigation | `gcmloader/gcmloaderwindow.xaml.cs` | 10344–10446 | Focus indices, card selection |
| Launcher integration | `gcmloader/gcmloaderwindow.xaml.cs` | 6462–6512 | Steam / Playnite / Xbox App launch |
| Service control | `gcmloader/gcmloaderwindow.xaml.cs` | 6538–6560+ | Enable/disable Windows services via WMI |
| UAC control | `gcmloader/gcmloaderwindow.xaml.cs` + `TaskHelper/Program.cs` | 5313–5348 / 175–206 | Registry UAC flags |
| Keyboard redirect | `gcmloader/gcmloaderwindow.xaml.cs` | 9159–9229 | HKLM osk.exe substitution |
| Configuration | `gcmloader/AppSettings.cs`, `GAMINGCONSOLEMODE/AppSettings.cs` | — | TOML-based persistent settings |

### Tight Coupling (Severe)

1. Shell logic lives inside `MainWindow` — no abstraction layer
2. D-pad events directly mutate UI focus state — no input abstraction
3. Service control scattered inline across startup/shutdown
4. Two `AppSettings.cs` files in different projects — **settings do not sync**
5. Settings keys (`"uac"`, `"launcher"`, etc.) referenced throughout but **none initialized in `initialconfig()`**

**Refactoring needed before UI work:**
- `ShellManagementService`
- `GamepadInputService` (then remove)
- `ServiceManagementService`
- `RegistryOperations` abstraction
- Unified `AppSettings` with defaults

---

## 5. DEPENDENCIES

### NuGet Packages

| Package | Used For | Conflict | Action |
|---------|----------|----------|--------|
| `SharpDX.XInput` | Gamepad input | CRITICAL — entire input model | REMOVE with controller code |
| `System.Management` | WMI service control | HIGH — tightly coupled to startup | MODIFY (extract `IServiceManager`) |
| `Microsoft.TaskScheduler` | Scheduled tasks | HIGH — shell assumption | MODIFY or REMOVE |
| `Microsoft.WindowsAppSDK` | WinUI 3 runtime | None | KEEP |
| `CommunityToolkit.WinUI.*` | UI controls, animations | None | KEEP |
| `Notification.Wpf` | WPF toast notifications | Medium — WPF project only | Migrate if needed |
| `NAudio` + `AudioSwitcher.AudioApi.CoreAudio` | Audio device switching | None | KEEP |
| `Tomlyn` | TOML config | None | KEEP |
| `Flurl.Http` | HTTP client | None | KEEP |
| `Newtonsoft.Json` | JSON serialization | None | KEEP |
| `Microsoft.Web.WebView2` | Embedded Chromium | None | KEEP |

### Config System Bug

Settings keys used at runtime but **not defined in `initialconfig()`**:

```
"uac", "launcher", "steamlauncherpath", "onboarding", "lossless",
"usewinpart", "usewinpartstartapps", "useboilr", "usecssloader",
"usedeckyloader", "usedisplayfusion", "usepreaudio", "usesteamstartupvideo",
"usestartupvideo", "useseamlessswitchtogcm", "winmode_shortcut"
```

Silent runtime failures if settings file missing or incomplete.

Two separate `AppSettings.cs` files:
- `GAMINGCONSOLEMODE/AppSettings.cs` — 308 lines
- `gcmloader/AppSettings.cs` — 130 lines

Neither is authoritative. Settings written by one project are not guaranteed readable by the other.
