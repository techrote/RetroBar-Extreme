# Builds this checkout and installs it over your installed RetroBar, keeping a backup of both
# the program folder and your settings so you can go back with -Restore.
#
#   .\scripts\install-test-build.ps1            build + install + restart RetroBar
#   .\scripts\install-test-build.ps1 -Restore   put the previous install and settings back
param([switch]$Restore)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$install = Join-Path $env:LOCALAPPDATA 'Programs\RetroBar'
$data = Join-Path $env:LOCALAPPDATA 'RetroBar'
$backup = Join-Path $env:LOCALAPPDATA 'RetroBar-test-backup'
$exe = Join-Path $install 'RetroBar.exe'

function Stop-RetroBar {
    Get-Process RetroBar -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

if ($Restore) {
    if (-not (Test-Path "$backup\program")) { throw "No backup found at $backup" }
    Stop-RetroBar
    Remove-Item "$install\*" -Recurse -Force
    Copy-Item "$backup\program\*" $install -Recurse -Force
    Copy-Item "$backup\settings.json", "$backup\Style.json" $data -Force -ErrorAction SilentlyContinue
    Start-Process $exe
    'Restored the previous RetroBar and settings.'
    return
}

$publish = Join-Path $env:TEMP 'retrobar-test-publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish "$repo\RetroBar\RetroBar.csproj" -c Release -f net10.0-windows -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

Stop-RetroBar

# Back up only the first time, so -Restore always returns to your real install.
if (-not (Test-Path "$backup\program")) {
    New-Item -ItemType Directory "$backup\program" -Force | Out-Null
    Copy-Item "$install\*" "$backup\program" -Recurse -Force
    Copy-Item "$data\settings.json", "$data\Style.json" $backup -Force -ErrorAction SilentlyContinue
}

# The shipped samples move between builds (folders get renamed): replace them, never mix.
if (Test-Path "$install\Extras") { Remove-Item "$install\Extras" -Recurse -Force }
Copy-Item "$publish\*" $install -Recurse -Force
Start-Process $exe
"Installed the test build. Undo with: .\scripts\install-test-build.ps1 -Restore"
