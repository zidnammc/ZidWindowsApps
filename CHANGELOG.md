# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.2.2-beta] — 2026-11-22

### Fixed

- **Empty Recycle Bin always failed** with "Ukuran Recycle Bin tidak dapat dibaca"
  (the parameter is incorrect). The `SHQUERYRBINFO` struct was declared with
  `Pack = 1` (20 bytes); 64-bit Windows expects 24 bytes, so every query was
  rejected with `E_INVALIDARG`.
- Emptying an already-empty Recycle Bin no longer reports an error
  (`E_UNEXPECTED` is treated as "nothing to do").
- The dashboard card showed **"Belum dipindai"** right after a cleanup. It now
  shows the last cleanup (size, number of files, time, and whether it was the
  scheduled task). The summary survives restarts and is also written by the
  scheduled cleanup.
- Cleanup under-reported freed space when a batch failed part-way. Files that the
  batch had already moved are now counted, and a cancelled cleanup still records
  what it finished.
- The "Empty Recycle Bin" button disappeared after a restart. It is now always
  available on the Cleanup page.
- The tray icon used the generic Windows application icon instead of the app icon.
- Restoring from the tray after minimising opened a small window instead of the
  previous (maximised) size.
- Labels in button rows touched the neighbouring button (Cleanup footer, Settings).

### Changed

- Free-space telemetry refreshes right after a cleanup or after emptying the
  Recycle Bin.
- README screenshots are rendered from the real UI with anonymous sample data
  (`tools/FluentReview <dir> readme`).
- Repository tidy-up: `.gitattributes`, merged `.gitignore`, anonymised preview
  data, CI also builds the review tool, `bug-and-fixed-report.md` moved to `docs/`.

## [1.2.1-beta.1] — 2026-11-21

### Changed

- The main window opens maximised (Fluent and legacy UI). Title bar, taskbar, and
  window controls stay available.

## [1.2.1-beta] — 2026-11-21

### Fixed

- Cleanup: "select all" no longer ticks Downloads files; they must still be
  selected one by one. Clearing the selection also clears Downloads.
- Settings are saved through a unique temporary file each time. Settings from
  1.2.0 are still read.
- The self-contained uninstaller removes only the files listed when the package was
  built. An install folder that still contains extra files is kept.

### Changed

- Icon and documentation images compressed losslessly.
- Installer name, Windows version info, and window title show the beta version.

## [1.2.0] — 2026-11-21

### Added

- New Fluent WPF interface: nine modules backed by real Windows services, Ctrl+K
  quick search, light/dark theme, and a collapsible sidebar (both persisted).
- Health score from memory use, system-drive free space, auto-start items, and
  processes.
- Duplicate finder keeps one original and re-verifies hashes before moving copies.
- Optional "Empty Recycle Bin" step after cleanup, with its own confirmation.

### Changed

- The previous interface is still available with `ZidWindowsApps.exe --legacy-ui`.

## [1.1.0] — 2026-10-29

First public release.

### Added

- Dashboard with CPU, RAM, disk, startup, processes, and a health score
- Uninstaller for desktop and Store apps with leftover scan (files, folders,
  registry keys) and registry backup
- Process manager with protection for critical processes
- Auto-start manager (reversible, Task Manager `StartupApproved` mechanism)
- Cleanup per category: temp files, browser cache, old Downloads; scheduled
  cleanup via Task Scheduler
- Duplicate finder (size first, then hash)
- System info: CPU, RAM, storage, GPU, battery, firmware, network, drivers,
  TPM, Secure Boot, BitLocker; TXT export
- Battery report, power plans, troubleshooting checks (SFC, disk, network)
- Settings: Indonesian/English navigation labels, tray behaviour, confirmation
  prompts
- Self-contained installer (NSIS), no .NET runtime needed on the target PC

[1.2.2-beta]: https://github.com/zidnammc/ZidWindowsApps/releases/tag/v1.2.2-beta
[1.1.0]: https://github.com/zidnammc/ZidWindowsApps/releases/tag/v1.1.0
