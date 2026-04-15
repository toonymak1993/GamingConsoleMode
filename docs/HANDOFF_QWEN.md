# Handoff to Qwen Code — DeckTop Priority Work Plan

## Update (post–pre-UI backlog)

The following landed in-repo: `**DeckTop.Settings**` shared library; `**ILauncherService` / `LauncherService**` extraction; `**BackToWindows**` timer/taskbar/steam-injection cleanup; companion hub `**GAMINGCONSOLEMODE/README.md**`; hub baseline **MKB** (focus/navigation attributes on main chrome); `**AGENTS.md`** refresh; optional `**github_update_check**` + deferred post–first-paint work in **gcmloader**; `**FocusNavigationHelper`** stub for future shared nav parity; satellite `**README.md**` for **wingamepad** / **OverlayWindow** + **TaskHelper** audit comment; **wingamepad** `.csproj` deprecation note.

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

## Legacy Appendix (trimmed)

The original step-by-step migration script has been intentionally trimmed because most steps are now complete and several directives are outdated versus current project policy.

For current execution guidance, always use:

- `docs/PRIORITY.md` as the source of truth
- `AGENTS.md` for guardrails and architecture map

Legacy intent preserved:

- `gcmloader` is the primary desktop entry point
- Winlogon shell replacement and UAC policy manipulation are out of scope
- shared settings should remain centralized through `DeckTop.Settings`
- controller behavior is optional and gated by `use_controller_navigation` (default `false`)

