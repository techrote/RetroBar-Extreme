# Runs the actual installer helper against temporary directories. Build/process commands
# are mocked: no RetroBar process, real installation, settings or network are touched.
$ErrorActionPreference = 'Stop'
$subject = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts\install-test-build.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('retrobar-backup-tests-' + [guid]::NewGuid().ToString('N'))
$oldLocalAppData = $env:LOCALAPPDATA
$oldTemp = $env:TEMP
$script:assertions = 0
$script:stopCalls = 0
$script:launches = 0
$script:failSnapshotCopy = $false
$script:failProgramSwap = $false
$script:failStyleRestore = $false

function Assert([bool]$Condition, [string]$Message) {
    $script:assertions++
    if (-not $Condition) { throw "FAIL: $Message" }
}
function Expect-Failure([scriptblock]$Action, [string]$Message) {
    $failed = $false
    try { & $Action } catch { $failed = $true }
    Assert $failed $Message
}
function Write-Fixture([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text)
}
function New-Fixture([string]$Name) {
    $caseRoot = Join-Path $testRoot $Name
    $env:LOCALAPPDATA = Join-Path $caseRoot 'local'
    $env:TEMP = Join-Path $caseRoot 'temp'
    $fixture = [pscustomobject]@{
        Install = Join-Path $env:LOCALAPPDATA 'Programs\RetroBar'
        Data = Join-Path $env:LOCALAPPDATA 'RetroBar'
        Backup = Join-Path $env:LOCALAPPDATA 'RetroBar-test-backup'
    }
    foreach ($folder in @($fixture.Install, $fixture.Data, $env:TEMP)) {
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
    }
    Write-Fixture (Join-Path $fixture.Install 'RetroBar.exe') 'original-program'
    Write-Fixture (Join-Path $fixture.Install 'custom.txt') 'keep-custom-file'
    New-Item -ItemType Directory -Path (Join-Path $fixture.Install 'Extras') | Out-Null
    Write-Fixture (Join-Path $fixture.Install 'Extras\old-sample.txt') 'old-sample'
    $script:stopCalls = 0
    $script:launches = 0
    return $fixture
}
function Invoke-Subject([switch]$Restore) {
    & $subject -Restore:$Restore | Out-Null
}

# These commands are visible to the invoked script but never launch or stop a program.
function dotnet {
    $arguments = @($args)
    $outputIndex = [Array]::IndexOf($arguments, '-o')
    if ($outputIndex -lt 0) { throw 'The build did not specify a temporary output directory.' }
    $output = [string]$arguments[$outputIndex + 1]
    New-Item -ItemType Directory -Path (Join-Path $output 'Extras') -Force | Out-Null
    Write-Fixture (Join-Path $output 'RetroBar.exe') 'test-program'
    Write-Fixture (Join-Path $output 'Extras\new-sample.txt') 'new-sample'
    $global:LASTEXITCODE = 0
}
function Get-Process {
    [CmdletBinding()]
    param([string]$Name)
    Assert ($Name -eq 'RetroBar') 'Only RetroBar is addressed by process cleanup'
    $script:stopCalls++
}
function Start-Sleep { param([int]$Seconds) }
function Start-Process {
    [CmdletBinding()]
    param([string]$FilePath)
    Assert ($FilePath.StartsWith($testRoot, [StringComparison]::OrdinalIgnoreCase)) 'Only the isolated fixture can be launched'
    Assert (Test-Path -LiteralPath $FilePath -PathType Leaf) 'The restarted executable exists'
    $script:launches++
}
function Copy-Item {
    [CmdletBinding()]
    param([string[]]$LiteralPath, [string]$Destination, [switch]$Recurse, [switch]$Force)
    if ($script:failSnapshotCopy -and $Destination -like '*program.pending-*') {
        $script:failSnapshotCopy = $false
        throw 'Injected original-program backup failure'
    }
    if ($script:failStyleRestore -and $Destination -like '*Style.json.restore-*') {
        $script:failStyleRestore = $false
        throw 'Injected settings restoration failure after the first file'
    }
    Microsoft.PowerShell.Management\Copy-Item @PSBoundParameters
}
function Move-Item {
    [CmdletBinding()]
    param([string[]]$LiteralPath, [string]$Destination, [switch]$Force)
    if ($script:failProgramSwap -and $LiteralPath[0] -like '*RetroBar-test-stage-*') {
        $script:failProgramSwap = $false
        throw 'Injected program promotion failure'
    }
    Microsoft.PowerShell.Management\Move-Item @PSBoundParameters
}

try {
    $fixture = New-Fixture 'round-trip'
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{"Theme":"original"}'
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{"name":"original-style"}'
    Invoke-Subject
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'test-program') 'Test build is installed'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Backup 'program\files\RetroBar.exe')) -eq 'original-program') 'Complete snapshot keeps original program'
    Assert (Test-Path -LiteralPath (Join-Path $fixture.Install 'custom.txt')) 'Custom program files survive test installation'
    Assert (-not (Test-Path -LiteralPath (Join-Path $fixture.Install 'Extras\old-sample.txt'))) 'Shipped samples are replaced rather than mixed'
    Assert (Test-Path -LiteralPath (Join-Path $fixture.Install 'Extras\new-sample.txt')) 'New samples are installed'
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{"Theme":"test"}'
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{"name":"test-style"}'
    Invoke-Subject -Restore
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'original-program') 'Restore returns original executable'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Data 'settings.json')) -eq '{"Theme":"original"}') 'Restore returns saved settings'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Data 'Style.json')) -eq '{"name":"original-style"}') 'Restore returns saved style'
    Write-Host 'PASS: program/settings round trip and shipped sample replacement'

    $fixture = New-Fixture 'initially-absent-settings'
    Invoke-Subject
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{}'
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{}'
    Invoke-Subject -Restore
    Assert (-not (Test-Path -LiteralPath (Join-Path $fixture.Data 'settings.json'))) 'Restore preserves absence of settings'
    Assert (-not (Test-Path -LiteralPath (Join-Path $fixture.Data 'Style.json'))) 'Restore preserves absence of style'
    Write-Host 'PASS: settings absence is restored'

    $fixture = New-Fixture 'repeated-test-install'
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{"Theme":"first"}'
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{"name":"first"}'
    Invoke-Subject
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{"Theme":"latest"}'
    Remove-Item -LiteralPath (Join-Path $fixture.Data 'Style.json')
    Invoke-Subject
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{"name":"test"}'
    Invoke-Subject -Restore
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'original-program') 'Repeated builds keep the original program backup'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Data 'settings.json')) -eq '{"Theme":"latest"}') 'Repeated builds capture latest pre-test settings'
    Assert (-not (Test-Path -LiteralPath (Join-Path $fixture.Data 'Style.json'))) 'A removed style is not resurrected from an older backup'
    Assert (@(Get-ChildItem -LiteralPath $fixture.Backup -Directory -Filter 'settings-previous-*').Count -eq 1) 'Earlier settings snapshots remain recoverable'
    Write-Host 'PASS: repeated test installs preserve original program and latest settings state'

    $fixture = New-Fixture 'incomplete-original-backup'
    $script:failSnapshotCopy = $true
    Expect-Failure { Invoke-Subject } 'A failed backup is reported'
    Assert (-not (Test-Path -LiteralPath (Join-Path $fixture.Backup 'program'))) 'Failed copy never publishes a usable original backup'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'original-program') 'Failed backup leaves original installation intact'
    Assert ($script:launches -eq 1) 'Original taskbar restarts after backup failure'
    Invoke-Subject
    Assert (Test-Path -LiteralPath (Join-Path $fixture.Backup 'program\complete.json')) 'A retry creates a complete backup'
    Write-Host 'PASS: failed backup remains uncommitted and can be retried'

    $fixture = New-Fixture 'legacy-incomplete-backup'
    New-Item -ItemType Directory -Path (Join-Path $fixture.Backup 'program') -Force | Out-Null
    Write-Fixture (Join-Path $fixture.Backup 'program\partial.txt') 'incomplete'
    Expect-Failure { Invoke-Subject -Restore } 'Unverified legacy backup cannot be restored'
    Expect-Failure { Invoke-Subject } 'Unverified legacy backup cannot be silently replaced'
    Assert ($script:stopCalls -eq 0) 'Backup validation fails before stopping the current taskbar'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'original-program') 'Rejected backup never deletes the installed program'
    Assert (Test-Path -LiteralPath (Join-Path $fixture.Backup 'program\partial.txt')) 'Legacy backup remains available for manual recovery'
    Write-Host 'PASS: legacy/incomplete backup is refused without touching the installation'

    $fixture = New-Fixture 'damaged-complete-backup'
    Invoke-Subject
    Write-Fixture (Join-Path $fixture.Backup 'program\files\custom.txt') 'damaged'
    $beforeStops = $script:stopCalls
    Expect-Failure { Invoke-Subject -Restore } 'A damaged backup is rejected'
    Assert ($script:stopCalls -eq $beforeStops) 'Hash validation completes before the taskbar stops'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'test-program') 'Damaged backup does not replace current program'
    Write-Host 'PASS: damaged snapshot is rejected before any destructive step'

    $fixture = New-Fixture 'failed-program-swap'
    $script:failProgramSwap = $true
    Expect-Failure { Invoke-Subject } 'Directory promotion failure is reported'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'original-program') 'Directory swap failure restores the original installation'
    Assert (Test-Path -LiteralPath (Join-Path $fixture.Install 'custom.txt')) 'Custom files survive directory rollback'
    Assert ($script:launches -eq 1) 'Original taskbar restarts after directory rollback'
    Write-Host 'PASS: failed program promotion rolls back'

    $fixture = New-Fixture 'failed-settings-restore'
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{"Theme":"original"}'
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{"name":"original"}'
    Invoke-Subject
    Write-Fixture (Join-Path $fixture.Data 'settings.json') '{"Theme":"before-restore"}'
    Write-Fixture (Join-Path $fixture.Data 'Style.json') '{"name":"before-restore"}'
    $script:failStyleRestore = $true
    Expect-Failure { Invoke-Subject -Restore } 'Partial settings restore failure is reported rather than hidden'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Install 'RetroBar.exe')) -eq 'test-program') 'Settings restore failure rolls back the program too'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Data 'settings.json')) -eq '{"Theme":"before-restore"}') 'Already-restored settings roll back to pre-operation values'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.Data 'Style.json')) -eq '{"name":"before-restore"}') 'Failed style restoration retains pre-operation style'
    Write-Host 'PASS: partial settings failure restores both settings and program'

    Write-Host "$script:assertions assertions; all installer-helper groups passed"
} finally {
    $env:LOCALAPPDATA = $oldLocalAppData
    $env:TEMP = $oldTemp
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}
