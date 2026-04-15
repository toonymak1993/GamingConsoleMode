# DeckTop — Pre-UI Priority Work Order

> These tasks must be completed before touching the UI layer.
> Order is load-bearing: each step reduces blast radius for the next.

---

## Priority Order

### 1. Establish the single entry point [DONE]
<!-- gcmloader = PRIMARY (mutex + admin); GAMINGCONSOLEMODE = SECONDARY — comments in App.xaml.cs — 2026-04-15 -->
**Files:** `GAMINGCONSOLEMODE/`, `gcmloader/`

`GAMINGCONSOLEMODE` and `gcmloader` overlap significantly with separate `AppSettings.cs` files and diverging startup logic. Determine which project is the true shell host. The other becomes a library or gets deleted.

**Why first:** Every other task targets "the main project." You need to know which one that is.

---

### 2. Remove shell registration [DONE]
<!-- Removed ConsoleModeToShell(), winpart(), rewrote BackToWindows(); stripped Winlogon Shell writes — 2026-04-15 -->
**Files:** `gcmloader/gcmloaderwindow.xaml.cs` lines 5349–5425, 4751–4757, 5611–5616, 4771–4777

Delete `ConsoleModeToShell()`. Rewrite `BackToWindows()` as simple cleanup + `Environment.Exit(0)`. DeckTop is a desktop app, not a shell replacement — it must not write to `HKLM\Winlogon\Shell`.

**Why second:** Shell registration requires elevation and corrupts the Windows boot path if something goes wrong. No other work should happen while this footgun exists.

---

### 3. Remove UAC disable [DONE]
<!-- Removed uac() and startup call; removed TaskHelper --uac / SetUAC — 2026-04-15 -->
**File:** `gcmloader/gcmloaderwindow.xaml.cs` line 6522

Delete `uac("off")` call. Remove UAC restore block from exit sequence (lines 4802–4810). Delete `uac()` method (lines 5313–5348). Remove duplicate in `TaskHelper/Program.cs` lines 35–36, 175–206.

**Why third:** Easy win. One line disable, few lines restore. Security regression with zero desktop benefit.

---

### 4. Fix the config system [DONE]
<!-- AddDefaultSettingsKeys in both AppSettings — Handoff keys + mini-launcher paths + winmode_shortcut table; no merge — 2026-04-15 -->
**Files:** `DeckTop.Settings/AppSettings.cs` (shared TOML); thin facades in `gcmloader` / `GAMINGCONSOLEMODE`

- Single implementation: `DeckTop.Settings.AppSettings` (`Save` / `Load` / defaults in `AddDefaultSettingsKeys`); WinUI projects forward or add WinUI-only helpers — not duplicate TOML stacks
- Add every referenced key to `initialconfig()` with safe defaults
- Keys missing defaults: `"uac"`, `"launcher"`, `"steamlauncherpath"`, `"onboarding"`, `"lossless"`, `"usewinpart"`, `"usewinpartstartapps"`, `"useboilr"`, `"usecssloader"`, `"usedeckyloader"`, `"usedisplayfusion"`, `"usepreaudio"`, `"usesteamstartupvideo"`, `"usestartupvideo"`, `"useseamlessswitchtogcm"`, `"winmode_shortcut"`

**Why fourth:** Silent config failures corrupt behavior of every feature above it in the stack. Fix before building anything new.

---

### 5. Extract services from the monolith [DONE]
<!-- gcmloader/Services: ShellManagementService, IServiceManager+ServiceManagementService sc.exe/ServiceController, RegistryOperations Steam+HKCU DWORDs; LauncherService deferred — 2026-04-15 -->
**File:** `gcmloader/gcmloaderwindow.xaml.cs` (12,654 lines)

Extract into separate service classes:
- `ShellManagementService` — explorer.exe kill/restart helpers
- `ServiceManagementService` — Windows service enable/disable via `ServiceController` + `sc.exe` (not WMI)
- `RegistryOperations` — shared low-level registry helpers
- `LauncherService` — Steam / Playnite / Xbox App launch logic (deferred)

Leave UI code in the window. Services get interfaces so they can be tested or swapped.

**Why fifth:** Impossible to safely modify UI or add features while business logic and UI state are in the same 12,000-line file.

---

### 6. Controller navigation is optional (desktop default) [DONE]
<!-- `use_controller_navigation` in settings.toml (default false); `SetupGamepad()` only when true; battery widget uses WGI only; shortcut overlay Escape/backdrop close — 2026-04-15 -->
**Files:** `gcmloader/AppSettings.cs`, `GAMINGCONSOLEMODE/AppSettings.cs`, `gcmloader/gcmloaderwindow.xaml.cs`, `gcmloader/GlobalShortcutWindow.*`

- Add **`use_controller_navigation`** (default **`false`**) — DeckTop is desktop-first; when **`true`**, legacy gamepad polling (Xbox / PS / Edge), D-pad UI navigation, gamepad shortcut combos, and pad mouse mode behave as before.
- When **`false`**, do not start `SetupGamepad()` / background input loops (no gamepad polling).
- Battery status uses **Windows.Gaming.Input** only (no SharpDX path).
- Shortcut overlay: dismiss via **Escape**, **click outside the card**, or existing toggle; document key in TOML (no dedicated Settings page yet).

**Why sixth:** Unblocks mouse/keyboard work (#7) for typical users while preserving handheld/controller parity behind one flag. `wingamepad/` / `OverlayWindow/` remain quarantined optional projects.

---

### 7. Add keyboard and mouse navigation [DONE]
<!-- RootGrid KeyDown + FocusArea routing mirroring HandleGamepadInput; type-to-search; TabIndex; Volume/quick-launch/card pointer parity; Gamepad UI toggle — 2026-04-14 -->
**File:** `gcmloader/gcmloaderwindow.xaml.cs` (post-extraction)

Replace D-pad navigation with:
- Standard WinUI 3 `KeyDown` handlers for arrow keys / Tab
- `PointerPressed` / `Click` handlers on all interactive elements
- Focus management via `FocusManager` / `TabIndex`

**Why last in pre-UI phase:** Requires #6 complete (optional controller path + desktop default). Once this lands, UI work can begin safely.

---

## What to Leave Alone (for now)

| Thing | Why |
|-------|-----|
| `TaskHelper` scheduled task logic | Needed for launcher startup; revisit after #5 |
| `NAudio` / `AudioSwitcher` | Audio switching works fine for desktop use |
| `Tomlyn` / `AppSettings` load/save pattern | Keep TOML, just fix the defaults |
| `Microsoft.Web.WebView2` | Useful for embedded store/browser views |
| Launcher integration (Steam/Playnite) | Core feature — touch after monolith split |
| `DisplayFusion` / `LosslessScaling` hooks | Third-party integrations, low risk, keep as-is |
