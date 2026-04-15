# Handoff to Qwen Code — DeckTop Priority Work Plan

## Update (post–pre-UI backlog)

The following landed in-repo: **`DeckTop.Settings`** shared library; **`ILauncherService` / `LauncherService`** extraction; **`BackToWindows`** timer/taskbar/steam-injection cleanup; companion hub **`GAMINGCONSOLEMODE/README.md`**; hub baseline **MKB** (focus/navigation attributes on main chrome); **`AGENTS.md`** refresh; optional **`github_update_check`** + deferred post–first-paint work in **gcmloader**; **`FocusNavigationHelper`** stub for future shared nav parity; satellite **`README.md`** for **wingamepad** / **OverlayWindow** + **TaskHelper** audit comment; **wingamepad** `.csproj` deprecation note.

The step-by-step sections below are the **original** priority handoff; use them only where they still match `docs/PRIORITY.md` and the current tree.

---

## Context

You are picking up a C# / WinUI 3 project called **DeckTop** — a fork of GameConsoleMode, a Windows shell-replacement gaming launcher. The previous agent (Claude) performed a full architectural audit and defined a priority work plan. Your job is to execute that plan in order.

**Read these files before writing a single line of code:**

- `AGENTS.md` — project rules, what to never touch, key file map
- `docs/AUDIT.md` — full audit with file:line references
- `docs/PRIORITY.md` — the ordered work plan you will execute

---

## Your Mission

Execute the DeckTop priority work plan defined in `docs/PRIORITY.md`. Work through each step in order. Do not skip ahead. Do not touch UI code.

---

## Step-by-Step Instructions

### Step 1 — Decide the entry point project

Read both:

- `GAMINGCONSOLEMODE/App.xaml.cs`
- `gcmloader/App.xaml.cs`

Determine which project is the real shell host (has the admin check, mutex, and launches the main window). The other project is secondary — document your finding as a comment at the top of each `App.xaml.cs` (`// PRIMARY ENTRY POINT` or `// SECONDARY — see gcmloader`). Do not delete anything yet.

---

### Step 2 — Remove shell registration

**Target file:** `gcmloader/gcmloaderwindow.xaml.cs`

Actions (in this exact order, one at a time):

1. Find and delete method `ConsoleModeToShell()` (lines ~5349–5425). It writes to `HKLM\Winlogon\Shell`. Remove the call site at line ~6503 inside `Start()`.
2. Find method `winpart()` (lines ~5611–5616). Delete it and any call sites.
3. Find method `BackToWindows()` (lines ~4730–4814). This is a 19-step exit sequence. Replace the body with:
  ```csharp
   private async void BackToWindows()
   {
       // TODO: restore taskbar, re-enable services, cleanup audio
       // Shell registration and UAC steps removed — DeckTop is not a shell replacement
       Logger.Log("BackToWindows called — graceful exit");
       Environment.Exit(0);
   }
  ```
   You will fill in the non-shell, non-UAC cleanup steps in a follow-up pass.
4. Search the entire solution for any remaining writes to `Winlogon` or `Shell` registry key. Remove them all.
5. Build the solution. Fix any compile errors caused by removed methods. Do not restore deleted logic.

---

### Step 3 — Remove UAC disable

**Target files:**

- `gcmloader/gcmloaderwindow.xaml.cs`
- `TaskHelper/Program.cs`

Actions:

1. In `gcmloaderwindow.xaml.cs` line ~6522: delete the line `uac("off");`
2. Delete method `uac(string art)` (lines ~5313–5348).
3. In `TaskHelper/Program.cs`: remove the `--uac=enable` and `--uac=disable` argument handling (lines ~35–64) and the `SetUAC()` method (lines ~175–206).
4. Search entire solution for any remaining references to `ConsentPromptBehaviorAdmin` or `PromptOnSecureDesktop`. Remove them.
5. Build and fix compile errors.

---

### Step 4 — Fix the config system

**Target files:**

- `gcmloader/AppSettings.cs`
- `GAMINGCONSOLEMODE/AppSettings.cs`

Actions:

1. Open both `AppSettings.cs` files. They diverge — `gcmloader` version is 130 lines, `GAMINGCONSOLEMODE` is 308 lines. Identify which has the more complete `initialconfig()` method.
2. Add the following keys with safe defaults to `initialconfig()` if they are missing. These keys are referenced throughout the codebase but never initialized:
  ```csharp
   // In initialconfig() or equivalent defaults dictionary:
   { "uac", false },
   { "launcher", "steam" },
   { "steamlauncherpath", "" },
   { "onboarding", false },
   { "lossless", false },
   { "usewinpart", false },
   { "usewinpartstartapps", false },
   { "useboilr", false },
   { "usecssloader", false },
   { "usedeckyloader", false },
   { "usedisplayfusion", false },
   { "usepreaudio", false },
   { "usesteamstartupvideo", false },
   { "usestartupvideo", false },
   { "useseamlessswitchtogcm", false },
   { "winmode_shortcut", "" }
  ```
3. Add a `// REMOVED: uac` comment next to any `"uac"` key — it remains in config for backward compat but has no effect now.
4. Do NOT merge the two files yet — that is a larger refactor. Just make both have safe defaults.
5. Build and fix compile errors.

---

### Step 5 — Extract services from the monolith (partial)

**Target file:** `gcmloader/gcmloaderwindow.xaml.cs` (12,654 lines)

This is the largest step. Do it in sub-tasks:

#### 5a — Extract `ShellManagementService`

Create `gcmloader/Services/ShellManagementService.cs`. Move any remaining registry logic related to `explorer.exe` detection/restart into this class. The window should call `ShellManagementService.RestartExplorer()` rather than containing the logic inline.

#### 5b — Extract `ServiceManagementService`

Create `gcmloader/Services/ServiceManagementService.cs`. Move WMI service enable/disable calls (search for `System.Management`, `ManagementObject`, `StartService`, `StopService`) into this class with an interface `IServiceManager`.

#### 5c — Extract `RegistryOperations`

Create `gcmloader/Services/RegistryOperations.cs`. All remaining `Registry.SetValue` / `RegistryKey.OpenBaseKey` calls outside of shell/UAC context should route through this helper.

**Rule:** Do not move UI code. Only move logic that has no dependency on WinUI controls or the window's visual state.

Build and fix compile errors after each sub-task.

---

### Step 6 — Remove gamepad input system

**Target files:**

- `gcmloader/gcmloaderwindow.xaml.cs`
- `wingamepad/MainWindow.xaml.cs`
- `OverlayWindow/MainWindow.xaml.cs`

Actions:

1. In `gcmloaderwindow.xaml.cs`:
  - Delete `SetupGamepad()` method and its call site
  - Delete `XboxInputLoop()`, `PlayStationInputLoop()`, `PlayStationEdgeInputLoop()` methods (lines ~9302–9304 start them)
  - Delete `XInputGetStateSecret` P/Invoke struct and DllImport (lines ~9325–9345)
  - Delete D-pad navigation handlers (lines ~10344–10396)
  - Delete gamepad state arrays: `_lastButtonStates`, `_lastShortcutButtons`, `_nextAllowedInputTime`, `_isStickCentered`
2. Remove `SharpDX.XInput` NuGet reference from `gcmloader.csproj`.
3. In `wingamepad.csproj` and `OverlayWindow`: add a `<!-- DEPRECATED: scheduled for removal -->` comment to the project file's `<PropertyGroup>`. Do not delete the projects yet — confirm with the human first.
4. Build and fix compile errors.

---

### Step 7 — Add keyboard and mouse navigation

**Target file:** `gcmloader/gcmloaderwindow.xaml.cs` (or whichever window file owns the game list UI)

Actions:

1. Identify the main game card list control (likely a `GridView`, `ListView`, or custom panel).
2. Add `KeyDown` handler on the main window:
  ```csharp
   private void MainWindow_KeyDown(object sender, KeyRoutedEventArgs e)
   {
       switch (e.Key)
       {
           case VirtualKey.Up:    NavigateUp(); break;
           case VirtualKey.Down:  NavigateDown(); break;
           case VirtualKey.Left:  NavigateLeft(); break;
           case VirtualKey.Right: NavigateRight(); break;
           case VirtualKey.Enter: ActivateSelected(); break;
           case VirtualKey.Escape: GoBack(); break;
       }
   }
  ```
3. Ensure all interactive elements (game cards, buttons, settings rows) have:
  - `IsTabStop="True"`
  - Correct `TabIndex` order
  - `PointerPressed` or `Click` handlers where missing
4. Test that Tab key moves focus through all interactive elements in a logical order.

---

## Rules While Working

- **Never write to `HKLM\Winlogon\Shell`**
- **Never call `uac("off")` or set `ConsentPromptBehaviorAdmin`**
- **Never add XInput/SharpDX code**
- Build after every step — do not accumulate compile errors across steps
- If a method is called from multiple places, search the whole solution before deleting
- If unsure whether something is safe to delete, leave it and add `// TODO: verify safe to remove`
- Do not refactor or improve code outside the scope of the current step
- Do not modify `README.md` or `docs/AUDIT.md`

---

## When You Finish Each Step

Update `docs/PRIORITY.md` — add a `[DONE]` marker and one-line summary of what was changed to the relevant step heading. Example:

```markdown
### 2. Remove shell registration [DONE]
<!-- Removed ConsoleModeToShell(), winpart(), rewrote BackToWindows() — 2026-04-14 -->
```

---

## Questions / Blockers

If you encounter something that doesn't match the line numbers in this handoff (the codebase may have changed), re-read the file and adjust. The method names are stable even if line numbers drift.

If you find something that looks like it should be removed but isn't listed here, do not remove it — note it in a comment and continue.

Good luck.