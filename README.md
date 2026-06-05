# Game Console Mode

Game Console Mode is a controller-first shell for Windows that is now built around a Steam backend workflow.

It is meant for couch setups, handheld PCs, HTPCs, and full-screen gaming installs where you want Windows to stay in the background and your launcher experience to feel more like a console.

![Game Console Mode](gcmnews.png)

## What GCM Is Now

GCM is no longer trying to be a loose collection of launchers and side tools.

The current direction is much simpler:

- Steam is the main backend system
- settings live inside GCM itself
- the shell, launcher flow, process view, and Steam tools are designed around controller use

That also means there is no separate desktop settings app anymore. Everything important now lives in the launcher.

## Controller Support

GCM currently supports:

- Steam Controller 2
- Xbox controllers
- DualSense

Xbox and DualSense still offer the fullest shortcut coverage. Steam Controller support is built around the real in-app flow that makes sense for it, including fast access back into the GCM task manager.

## What's New In This Build

- the visual design has been heavily reworked with a cleaner glass-style shell
- the old external settings flow is gone and has been folded into the launcher
- the Steam side of the project has been expanded into a proper built-in backend system
- Tools for Steam has been integrated into GCM instead of living beside it
- the app launcher, audio panel, process cards, controller navigation, and task manager flow have all been reworked around gamepad use

## A Quick Heads-Up

This version changes a lot at once.

That is exciting, but it also means there can still be rough edges and bugs while the new Steam-first direction settles in. If something feels off, please report it. That feedback is what helps turn a big transition build into a stable everyday one.

## Installation

1. Download the latest installer from the GitHub Releases page.
2. Run the setup.
3. Start Game Console Mode from the Start Menu.
4. Open the in-app settings and tune the shell from there.

## Why The Project Looks Different Now

GCM has been evolving for years, and this release is a bigger reset than a normal update.

The focus is now on one coherent fullscreen experience instead of splitting setup, launch logic, and Steam tooling across multiple places. The goal is less friction, fewer desktop detours, and a cleaner path from boot to play.

## Feedback

- Report bugs or ideas here: [GitHub Issues](https://github.com/toonymak1993/GameConsoleMode/issues)
- Join the community here: [Discord](https://discord.gg/FbjYDeEJce)

## A Note On Development

GCM is not a throwaway mockup or a fresh AI-only experiment.

The project has been around for years. AI has been used as development support in parts of the workflow, but the app was not built from scratch that way, and the code has been reviewed throughout development.
