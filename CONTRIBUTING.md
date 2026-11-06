# Contributing to ZidWindowsApps

Thanks for your interest! This is a personal, early-stage project maintained by one person, so small, focused contributions work best.

## How to contribute

1. Check the [open issues](https://github.com/zidnammc/ZidWindowsApps/issues) — if your idea isn't there yet, open an issue first and describe it.
2. Fork the repo and create a branch from `main`.
3. Keep changes small and focused — one fix or feature per pull request.
4. Describe what you changed and what you tested on Windows in the pull request.

## What to keep in mind

- **Test on real Windows.** This app manages processes, startup entries, files, and registry keys. Please test your change on Windows 10 or 11 and say which version you used.
- **Safety first.** Anything destructive (uninstall, registry deletion, disabling a service) must ask for confirmation, even if confirmations are turned off. Never delete files outside the paths the user chose.
- **UI text is Indonesian for now.** English UI strings are planned; please don't translate the whole UI in a drive-by PR — open an issue first.
- **No large refactors** without discussing them in an issue first.

## Build

```powershell
dotnet build -c Release
```

Or build the installer with `./build.ps1 -SelfContained` (needs .NET SDK 8+ and NSIS 3).

## Code of conduct

Please read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Be kind and respectful.
