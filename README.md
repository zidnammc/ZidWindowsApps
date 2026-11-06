# ZidWindowsApps

[![CI](https://github.com/zidnammc/ZidWindowsApps/actions/workflows/ci.yml/badge.svg)](https://github.com/zidnammc/ZidWindowsApps/actions/workflows/ci.yml)
[![GitHub release](https://img.shields.io/github/v/release/zidnammc/ZidWindowsApps)](https://github.com/zidnammc/ZidWindowsApps/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Downloads](https://img.shields.io/github/downloads/zidnammc/ZidWindowsApps/total.svg)](https://github.com/zidnammc/ZidWindowsApps/releases)

> **Early stage.** This is a work in progress, published as-is. Many features still need polish, and the UI text is currently in Indonesian — an English UI is on the roadmap.

A Windows maintenance center: one desktop app to inspect and clean up a Windows PC — processes, auto-start entries, installed apps, junk files, system specs, and troubleshooting. Built with WPF on .NET 8.

## Screenshots

_Screenshots coming soon._

## Download

Get the installer from [Releases](https://github.com/zidnammc/ZidWindowsApps/releases): `ZidWindowsApps-Setup-SelfContained-win-x64.exe` (49.6 MB, self-contained, no .NET runtime needed). Windows 10 / 11, 64-bit.

Or build from source — see [Build](#build).

## Installation

1. Download `ZidWindowsApps-Setup-SelfContained-win-x64.exe` from [Releases](https://github.com/zidnammc/ZidWindowsApps/releases).
2. Double-click it and approve the Administrator prompt — the installer needs admin rights.
3. Follow the wizard: Welcome → choose the install folder (default `C:\Program Files\ZidWindowsApps`) → choose components (the Desktop shortcut is optional) → Install → Finish. Tick **Run ZidWindowsApps** on the last page to launch it right away.
4. No .NET runtime to install: the self-contained package bundles it. Works on Windows 10 / 11, 64-bit.

If the app is running, the installer stops it first. To uninstall, use Windows Settings → Apps → ZidWindowsApps → Uninstall, or run `Uninstall.exe` in the install folder — this also removes the scheduled cleanup task.

## Features

- **Dashboard** — at-a-glance device status: CPU, RAM, disk space, startup items, running processes, and a health score.
- **Uninstaller** — uninstall desktop and Store apps, then scan for leftovers (files, folders, and registry keys) with a backup before any registry key is deleted.
- **Processes** — list processes, close them politely first, kill only when needed. System processes are hidden by default and critical processes (csrss, lsass, winlogon, …) are protected.
- **Auto-start** — enable/disable startup entries reversibly (the Task Manager `StartupApproved` mechanism), including scheduled tasks and services. Windows services are not listed; third-party services are protected by a confirmation prompt.
- **Cleanup** — per-category checkboxes for temporary files, browser cache (Edge/Chrome), old Downloads, and the Recycle Bin. Cleaned files go to the Recycle Bin. Scheduled cleanup runs via Task Scheduler (`--scheduled-cleanup`, default 03:00, daily/weekly/monthly).
- **Duplicate finder** — find duplicate files by size first, then hash; skips inaccessible folders and can be cancelled.
- **System info** — CPU, RAM, storage, GPU, battery, firmware, network, drivers, TPM, Secure Boot, and BitLocker. Exportable as a TXT report. Serial numbers are never read, and reports are stored only on the local machine.
- **Tools** — battery report, power plans (Balanced / High performance / Battery saver), and opening Windows Settings.
- **Troubleshooting** — guided checks and repairs (SFC, disk, network, …).
- **Settings** — Indonesian/English language, system-tray behaviour, confirmation prompts, and scheduled cleanup.

### Safety first

Destructive actions — uninstall, registry deletion, disabling a service — **always ask for confirmation**, even if confirmations are turned off. Cleanup is guarded by `PathGuard`, which refuses container folders (Windows, Program Files, drive roots, user-profile roots).

## Requirements

- Windows 10 / 11, 64-bit (x64 or ARM64)
- .NET Desktop Runtime 8 or newer (for the light installer; the self-contained installer bundles the runtime)
- Administrator rights for some features (Windows Update cache, HKLM keys, SFC, services)
- To build the installer: .NET SDK 8+ and [NSIS 3](https://nsis.sourceforge.io)

## Build

```powershell
./build.ps1                     # light installer (needs .NET Desktop Runtime 8+ on the target PC)
./build.ps1 -SelfContained      # self-contained x64 installer, runtime bundled
./build.ps1 -SelfContained -Rid win-arm64   # self-contained for Windows ARM64
```

The first self-contained build needs internet access (runtime pack from NuGet). App only, without installer:

```powershell
dotnet publish -c Release -o dist
```

Linux/CI: see `build-linux.sh` (needs the WPF/WinForms reference DLLs copied into `refs/` first — see `refs/README.txt`). The UI is loaded at runtime from `Resources/*.xaml` (embedded in the DLL), so the project builds with a single command on Windows or Linux without the Windows XAML MSBuild tasks.

## Project structure

- `Infrastructure/` — paths, settings, activity log, `PathGuard`, admin detection
- `Models/` — data classes
- `Services/` — logic without UI: PowerShell, Recycle Bin, cleanup, duplicates, processes, installed apps, auto-start, scheduling, system info
- `UI/` — `App`, `MainWindow` (split per tab), `InputDialog`
- `Resources/` — `MainWindow.xaml` and `Styles.xaml` (loaded at runtime, embedded in the DLL)
- `launcher/` — small native launcher (only for Linux/CI builds; on Windows the SDK apphost is used)
- `installer/` — NSIS script (Modern UI 2 wizard)
- `refs/` — reference DLLs for Linux/CI builds (not included; Microsoft binaries)

## Status and known limitations

- Version **1.1.0**. The v1.1.0 self-contained installer was built from this source on 29 Oct 2026.
- This snapshot is the cleaned-up rewrite of the original vibe-coded v1.0 app: the original installer was decompiled, audited, and rewritten into this project structure.
- The app icon (`launcher/app.ico`) is not included in this public snapshot, so builds use the default icon. Re-add the `.ico` file and restore the references in `ZidWindowsApps.csproj`, `launcher/launcher.rc`, and `installer/ZidWindowsApps.nsi` to get the custom icon back.
- Screenshots are coming soon — see [Screenshots](#screenshots). The UI language is Indonesian; English UI strings are planned.
- Some features have only been verified by code review and build checks, not by running every flow on a real Windows machine from this exact tree.

## Contributing

Issues and pull requests are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) first — this is a personal early-stage project, so keep changes small and focused, and describe what you tested on Windows.

## License

MIT — see [LICENSE](LICENSE).
