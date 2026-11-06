#!/usr/bin/env bash
# Build lintas-platform (Linux/CI): butuh dotnet SDK 8+, nsis, gcc-mingw-w64-x86-64, binutils-mingw-w64-x86-64.
# Salin dulu DLL referensi WPF/WinForms ke refs/ (lihat refs/README.txt).
set -euo pipefail
cd "$(dirname "$0")"
dotnet build ZidWindowsApps.csproj -c Release -o out
( cd launcher && x86_64-w64-mingw32-windres launcher.rc -O coff -o launcher.res \
  && x86_64-w64-mingw32-gcc -O2 -municode -mwindows -static launcher.c launcher.res -o ZidWindowsApps.exe -lshell32 -ladvapi32 -luser32 )
rm -rf dist && mkdir dist
cp out/ZidWindowsApps.dll launcher/ZidWindowsApps.exe dist/
cat > dist/ZidWindowsApps.runtimeconfig.json <<'JSON'
{ "runtimeOptions": { "tfm": "net8.0", "rollForward": "LatestMajor",
  "frameworks": [ { "name": "Microsoft.NETCore.App", "version": "8.0.0" }, { "name": "Microsoft.WindowsDesktop.App", "version": "8.0.0" } ] } }
JSON
makensis -DBIN_DIR="$PWD/dist" -DOUT_FILE="$PWD/ZidWindowsApps-Setup.exe" installer/ZidWindowsApps.nsi
