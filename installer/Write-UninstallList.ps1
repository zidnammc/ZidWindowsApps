param(
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$OutputFile
)
$ErrorActionPreference = 'Stop'
$root = (Get-Item -LiteralPath $PublishDirectory).FullName
$files = Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object Extension -ne '.pdb'
$directories = Get-ChildItem -LiteralPath $root -Recurse -Directory -Force | Sort-Object FullName -Descending
$commands = foreach ($item in @($files) + @($directories)) {
    $relative = $item.FullName.Substring($root.Length + 1).Replace('/', '\')
    if ($relative -notmatch '^[a-zA-Z0-9 ._\\-]+$') {
        throw "Nama file publish tidak didukung: $relative"
    }
    $command = if ($item.PSIsContainer) { 'RMDir' } else { 'Delete' }
    '{0} "$INSTDIR\{1}"' -f $command, $relative
}
[IO.File]::WriteAllLines($OutputFile, [string[]]$commands, [Text.UTF8Encoding]::new($true))
