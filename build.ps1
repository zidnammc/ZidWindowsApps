# Build di Windows (butuh .NET SDK 8+ dan NSIS: https://nsis.sourceforge.io)
#
#   ./build.ps1                      -> installer ringan (butuh .NET Desktop Runtime 8+ di PC tujuan)
#   ./build.ps1 -SelfContained       -> installer self-contained x64 (runtime ikut dibundel, tanpa prasyarat)
#   ./build.ps1 -SelfContained -Rid win-arm64   -> self-contained untuk Windows ARM64
#
# Build pertama membutuhkan internet untuk mengunduh runtime pack dari NuGet.
param(
    [string]$Configuration = "Release",
    [switch]$SelfContained,
    [ValidateSet("win-x64", "win-arm64")][string]$Rid = "win-x64"
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if ($SelfContained) {
    $dist = "dist-selfcontained-$Rid"
    $out  = "ZidWindowsApps-Setup-SelfContained-$Rid.exe"
    # ReadyToRun mempercepat startup. Trimming TIDAK dipakai (tidak aman untuk WPF + runtime XAML).
    dotnet publish ZidWindowsApps.csproj -c $Configuration -r $Rid --self-contained true `
        -p:PublishReadyToRun=true -p:UseAppHost=true -o $dist
} else {
    $dist = "dist"
    $out  = "ZidWindowsApps-Setup.exe"
    dotnet publish ZidWindowsApps.csproj -c $Configuration -o $dist -p:UseAppHost=true
}
if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal" }

$makensis = (Get-Command makensis -ErrorAction SilentlyContinue).Source
if (-not $makensis) { $makensis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe" }
if (-not (Test-Path $makensis)) { throw "makensis tidak ditemukan. Pasang NSIS dulu." }

$args = @("/DBIN_DIR=$PSScriptRoot\$dist", "/DOUT_FILE=$PSScriptRoot\$out")
if ($SelfContained) { $args += "/DSELF_CONTAINED" }
$args += "$PSScriptRoot\installer\ZidWindowsApps.nsi"

& $makensis @args
if ($LASTEXITCODE -ne 0) { throw "makensis gagal" }

$size = [math]::Round((Get-Item "$PSScriptRoot\$out").Length / 1MB, 1)
Write-Host "Selesai: $PSScriptRoot\$out ($size MB)"
