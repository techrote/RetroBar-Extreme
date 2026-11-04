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
$programSnapshot = Join-Path $backup 'program'
$settingsSnapshot = Join-Path $backup 'settings-current'
$settingNames = @('settings.json', 'Style.json')
$operation = [guid]::NewGuid().ToString('N')

function Stop-RetroBar {
    Get-Process RetroBar -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
}

function Copy-DirectoryContents([string]$Source, [string]$Destination) {
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Get-ProgramInventory([string]$Folder) {
    $prefix = $Folder.TrimEnd([char[]]@('\', '/')) + [IO.Path]::DirectorySeparatorChar
    @(Get-ChildItem -LiteralPath $Folder -File -Recurse -Force | ForEach-Object {
        [pscustomobject]@{
            Name = $_.FullName.Substring($prefix.Length)
            Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    } | Sort-Object Name)
}

function Assert-ProgramSnapshot([string]$Folder) {
    $recordPath = Join-Path $Folder 'complete.json'
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) {
        throw "No complete program backup at $Folder. Preserve any old backup for manual recovery; this script will not overwrite it or restore an unverified copy."
    }
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    $files = Join-Path $Folder 'files'
    if ($record.Version -ne 1 -or $record.Kind -ne 'program' -or -not (Test-Path -LiteralPath (Join-Path $files 'RetroBar.exe') -PathType Leaf)) {
        throw "Invalid program backup at $Folder"
    }
    $expected = @($record.Files | Sort-Object Name)
    $actual = @(Get-ProgramInventory $files)
    if ($expected.Count -ne $actual.Count) { throw "Incomplete program backup at $Folder" }
    for ($i = 0; $i -lt $actual.Count; $i++) {
        if ($expected[$i].Name -ne $actual[$i].Name -or $expected[$i].Hash -ne $actual[$i].Hash) {
            throw "Damaged program backup at $Folder ($($actual[$i].Name))"
        }
    }
}

function Assert-SettingsSnapshot([string]$Folder) {
    $recordPath = Join-Path $Folder 'complete.json'
    if (-not (Test-Path -LiteralPath $recordPath -PathType Leaf)) { throw "No complete settings backup at $Folder" }
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    if ($record.Version -ne 1 -or $record.Kind -ne 'settings' -or @($record.Files).Count -ne $settingNames.Count) {
        throw "Invalid settings backup at $Folder"
    }
    foreach ($name in $settingNames) {
        $entries = @($record.Files | Where-Object Name -EQ $name)
        if ($entries.Count -ne 1 -or $entries[0].Present -isnot [bool]) { throw "Invalid settings record for $name" }
        $entry = $entries[0]
        $file = Join-Path $Folder $name
        if ($entry.Present) {
            if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $entry.Hash) {
                throw "Damaged settings backup at $file"
            }
        } elseif (Test-Path -LiteralPath $file) { throw "Unexpected file in settings backup: $file" }
    }
    return $record
}

function New-SettingsSnapshot([string]$Destination) {
    $stage = "$Destination.pending-$operation"
    try {
        New-Item -ItemType Directory -Path $stage | Out-Null
        $entries = @(foreach ($name in $settingNames) {
            $source = Join-Path $data $name
            $present = Test-Path -LiteralPath $source -PathType Leaf
            if (-not $present -and (Test-Path -LiteralPath $source)) { throw "Settings path is not a file: $source" }
            $hash = $null
            if ($present) {
                Copy-Item -LiteralPath $source -Destination (Join-Path $stage $name)
                $hash = (Get-FileHash -LiteralPath (Join-Path $stage $name) -Algorithm SHA256).Hash
            }
            [pscustomobject]@{ Name = $name; Present = $present; Hash = $hash }
        })
        @{ Version = 1; Kind = 'settings'; Files = $entries } | ConvertTo-Json -Depth 4 |
            Set-Content -LiteralPath (Join-Path $stage 'complete.json') -Encoding UTF8
        $null = Assert-SettingsSnapshot $stage
        Move-Item -LiteralPath $stage -Destination $Destination
    } finally {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    }
}

function Restore-SettingsSnapshot([string]$Folder) {
    $record = Assert-SettingsSnapshot $Folder
    New-Item -ItemType Directory -Path $data -Force | Out-Null
    foreach ($entry in $record.Files) {
        $target = Join-Path $data $entry.Name
        if ($entry.Present) {
            $temporary = "$target.restore-$operation"
            try {
                Copy-Item -LiteralPath (Join-Path $Folder $entry.Name) -Destination $temporary
                if (Test-Path -LiteralPath $target -PathType Leaf) {
                    # PowerShell coerces $null to an empty string for string parameters.
                    [IO.File]::Replace($temporary, $target, [System.Management.Automation.Language.NullString]::Value)
                } else {
                    [IO.File]::Move($temporary, $target)
                }
            } finally {
                if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
            }
        } elseif (Test-Path -LiteralPath $target) {
            # Absence is part of the snapshot too; never resurrect a stale Style.json.
            Remove-Item -LiteralPath $target -Force
        }
    }
}

New-Item -ItemType Directory -Path $backup -Force | Out-Null
# Prevent two test installs from racing their backups, settings or directory swaps.
$operationLock = [IO.File]::Open((Join-Path $backup 'operation.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$publish = Join-Path $env:TEMP "retrobar-test-publish-$operation"
$stage = Join-Path (Split-Path -Parent $install) "RetroBar-test-stage-$operation"
$rescue = Join-Path $backup "program-before-operation-$operation"
$settingsBeforeRestore = Join-Path $backup "settings-before-restore-$operation"
$stopped = $false
$movedOriginal = $false
$installedNew = $false
$restoringSettings = $false

try {
    if ($Restore) {
        # Validate everything before stopping RetroBar or touching its current installation.
        Assert-ProgramSnapshot $programSnapshot
        $null = Assert-SettingsSnapshot $settingsSnapshot
        Copy-DirectoryContents (Join-Path $programSnapshot 'files') $stage
    } else {
        if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "No installed RetroBar found at $exe" }
        if (Test-Path -LiteralPath $programSnapshot) { Assert-ProgramSnapshot $programSnapshot }
        dotnet publish "$repo\RetroBar\RetroBar.csproj" -c Release -f net10.0-windows -o $publish
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $publish 'RetroBar.exe') -PathType Leaf)) { throw 'Build failed.' }
        # Build the replacement separately, retaining custom files but replacing shipped samples.
        Copy-DirectoryContents $install $stage
        $extras = Join-Path $stage 'Extras'
        if (Test-Path -LiteralPath $extras) { Remove-Item -LiteralPath $extras -Recurse -Force }
        Copy-DirectoryContents $publish $stage
    }

    Stop-RetroBar
    $stopped = $true

    if ($Restore) {
        # This snapshot makes partial settings-restoration failures reversible.
        New-SettingsSnapshot $settingsBeforeRestore
    } else {
        if (-not (Test-Path -LiteralPath $programSnapshot)) {
            $programStage = Join-Path $backup "program.pending-$operation"
            try {
                Copy-DirectoryContents $install (Join-Path $programStage 'files')
                @{ Version = 1; Kind = 'program'; Files = @(Get-ProgramInventory (Join-Path $programStage 'files')) } |
                    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $programStage 'complete.json') -Encoding UTF8
                Assert-ProgramSnapshot $programStage
                # An interrupted copy never creates the committed 'program' directory.
                Move-Item -LiteralPath $programStage -Destination $programSnapshot
            } finally {
                if (Test-Path -LiteralPath $programStage) { Remove-Item -LiteralPath $programStage -Recurse -Force }
            }
        }
        $nextSettings = Join-Path $backup "settings-next-$operation"
        New-SettingsSnapshot $nextSettings
        $previousSettings = Join-Path $backup "settings-previous-$operation"
        if (Test-Path -LiteralPath $settingsSnapshot) { Move-Item -LiteralPath $settingsSnapshot -Destination $previousSettings }
        try { Move-Item -LiteralPath $nextSettings -Destination $settingsSnapshot }
        catch {
            if (Test-Path -LiteralPath $previousSettings) { Move-Item -LiteralPath $previousSettings -Destination $settingsSnapshot }
            throw
        }
    }

    if (Test-Path -LiteralPath $install) {
        Move-Item -LiteralPath $install -Destination $rescue
        $movedOriginal = $true
    }
    Move-Item -LiteralPath $stage -Destination $install
    $installedNew = $true
    if ($Restore) {
        $restoringSettings = $true
        Restore-SettingsSnapshot $settingsSnapshot
    }
    Start-Process $exe
    $stopped = $false
    # Keep the replaced program and settings snapshots available for manual recovery.
    if ($Restore) { 'Restored the previous RetroBar and settings.' }
    else { 'Installed the test build. Undo with: .\scripts\install-test-build.ps1 -Restore' }
} catch {
    $failure = $_
    if ($restoringSettings) {
        try { Restore-SettingsSnapshot $settingsBeforeRestore }
        catch { Write-Warning "Settings rollback failed. The pre-restore snapshot is preserved at $settingsBeforeRestore. $($_.Exception.Message)" }
    }
    if ($movedOriginal) {
        try {
            if ($installedNew) { Move-Item -LiteralPath $install -Destination (Join-Path $backup "failed-program-$operation") }
            Move-Item -LiteralPath $rescue -Destination $install
        } catch { Write-Warning "Program rollback failed. The previous program is preserved at $rescue. $($_.Exception.Message)" }
    }
    if ($stopped -and (Test-Path -LiteralPath $exe -PathType Leaf)) {
        try { Start-Process $exe } catch { Write-Warning "Could not restart $exe. $($_.Exception.Message)" }
    }
    throw $failure
} finally {
    $operationLock.Dispose()
    foreach ($temporaryFolder in @($stage, $publish)) {
        if (Test-Path -LiteralPath $temporaryFolder) { Remove-Item -LiteralPath $temporaryFolder -Recurse -Force }
    }
}
