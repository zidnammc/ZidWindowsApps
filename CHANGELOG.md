# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.1.0] — 2026-10-29

First public release.

### Added

- Dashboard with CPU, RAM, disk, startup, processes, and a health score
- Uninstaller for desktop and Store apps with leftover scan (files, folders,
  registry keys) and registry backup
- Process manager with protection for critical processes
- Auto-start manager (reversible, Task Manager `StartupApproved` mechanism)
- Cleanup per category: temp files, browser cache, old Downloads, Recycle Bin;
  scheduled cleanup via Task Scheduler
- Duplicate finder (size first, then hash)
- System info: CPU, RAM, storage, GPU, battery, firmware, network, drivers,
  TPM, Secure Boot, BitLocker; TXT export
- Battery report, power plans, troubleshooting checks (SFC, disk, network)
- Settings: Indonesian/English language, tray behaviour, confirmation prompts
- Self-contained installer (NSIS), no .NET runtime needed on the target PC

### Notes

- Early-stage release, published as-is. UI text is Indonesian; English UI is
  planned.
- The app icon is not included in this public snapshot; builds use the
  default icon.
