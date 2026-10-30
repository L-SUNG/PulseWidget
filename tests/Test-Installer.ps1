param([string]$CompilerPath, [string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$testName = 'PulseWidget.InstallerTest'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PulseWidget.InstallerTest_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'An earlier installer test is still registered. Uninstall that test before running again.' }
if ((Get-Item -LiteralPath $runKey).GetValue($testName)) { throw 'The installer test startup value already exists.' }
$liveStartup = (Get-Item -LiteralPath $runKey).GetValue('PulseWidget')
$settings = Join-Path $env:LOCALAPPDATA 'PulseWidget\settings.json'
$liveSettings = if (Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
$liveProcesses = @(Get-Process PulseWidget -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
$testRoot = Join-Path $root ('artifacts\installer-test-' + [guid]::NewGuid().ToString('N'))
$destination = Join-Path $testRoot 'install with spaces'
$payload = Join-Path $root ('artifacts\installer-payload-' + $Version)
$desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Pulse Widget Installer Test.lnk'
$menu = Join-Path ([Environment]::GetFolderPath('Programs')) 'Pulse Widget Installer Test'
if ((Test-Path -LiteralPath $desktopLink) -or (Test-Path -LiteralPath $menu)) { throw 'Test shortcuts already exist.' }
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$build = @{ Version = $Version; PayloadDirectory = $payload; OutputDirectory = $testRoot; SkipPublish = $true; TestInstall = $true }
if ($CompilerPath) { $build.CompilerPath = $CompilerPath }
& (Join-Path $root 'scripts\Build-Installer.ps1') @build
$installer = Join-Path $testRoot "PulseWidget-$Version-Setup-x64-TEST.exe"
function Assert($condition, $label) { if (-not $condition) { throw "FAIL: $label" }; Write-Output "PASS: $label" }
function Run-Setup([string]$file, [string]$arguments) {
    # Inno starts a second-phase process. Wait for the entire process tree,
    # including registry/shortcut cleanup, not only the bootstrap process.
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    return $process.ExitCode
}
function Install([string]$name, [string]$tasks = '') {
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /LANG=korean /DIR="' + $destination + '" /LOG="' + (Join-Path $testRoot ($name + '.log')) + '" ' + $tasks
    return Run-Setup $installer $arguments
}
function Uninstall([string]$name) {
    return Run-Setup (Join-Path $destination 'unins000.exe') ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="' + (Join-Path $testRoot ($name + '.log')) + '"')
}
try {
    $mutex = New-Object Threading.Mutex($false, 'Local\PulseWidget.InstallerTest')
    try { Assert ((Install 'running-blocked' '/TASKS="startup,desktopicon"') -ne 0) 'A running widget blocks installation without being killed' }
    finally { $mutex.Dispose() }
    Assert (-not (Test-Path -LiteralPath $uninstallKey)) 'Blocked installation makes no product registration'
    Assert ((Install 'first' '/TASKS="startup,desktopicon"') -eq 0) 'Per-user installation succeeds in a path with spaces'
    foreach ($relative in @('PulseWidget.exe','D3DCompiler_47_cor3.dll','bridge\PulseBridge.exe','scripts\Install-Claude.ps1','scripts\Uninstall-Claude.ps1')) {
        Assert ((Get-FileHash -LiteralPath (Join-Path $payload $relative)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $destination $relative)).Hash) "Installed payload matches: $relative"
    }
    Assert (Test-Path -LiteralPath $uninstallKey) 'Windows Installed Apps registration exists'
    $expectedRun = '"' + (Join-Path $destination 'PulseWidget.exe') + '"'
    Assert ((Get-Item -LiteralPath $runKey).GetValue($testName) -eq $expectedRun) 'Startup uses the quoted installed executable path'
    Assert ((Test-Path -LiteralPath $desktopLink) -and (Test-Path -LiteralPath (Join-Path $menu 'Pulse Widget Installer Test.lnk'))) 'Desktop and Start menu shortcuts are created'
    Assert (@(Get-ChildItem -LiteralPath $destination -Recurse -File -Filter '*.pdb').Count -eq 0) 'Debug symbols are excluded'
    Assert (-not (Test-Path -LiteralPath (Join-Path $destination 'settings.json'))) 'User preferences are not packaged'

    Remove-ItemProperty -LiteralPath $runKey -Name $testName
    Set-Content -LiteralPath (Join-Path $destination 'README.md') -Value 'repair marker'
    Assert ((Install 'upgrade') -eq 0) 'Reinstall updates the same installation'
    Assert (-not (Get-Item -LiteralPath $runKey).GetValue($testName)) 'Upgrade preserves startup disabled in the widget'
    Assert ((Get-FileHash -LiteralPath (Join-Path $payload 'README.md')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $destination 'README.md')).Hash) 'Upgrade replaces modified application files'

    Assert ((Install 'startup-enabled' '/TASKS="startup"') -eq 0) 'Explicit installer selection re-enables startup'
    Assert ((Get-Item -LiteralPath $runKey).GetValue($testName) -eq $expectedRun) 'Enabled startup survives reinstall'
    Assert (-not (Test-Path -LiteralPath $desktopLink)) 'Unchecking desktop shortcut removes the previous shortcut'
    Assert ((Uninstall 'uninstall') -eq 0) 'Uninstaller completes'
    Assert (-not (Test-Path -LiteralPath (Join-Path $destination 'PulseWidget.exe')) -and -not (Test-Path -LiteralPath $uninstallKey)) 'Uninstall removes installed application and registration'
    Assert (-not (Get-Item -LiteralPath $runKey).GetValue($testName)) 'Uninstall removes its own startup entry'
    Assert (-not (Test-Path -LiteralPath (Join-Path $menu 'Pulse Widget Installer Test.lnk'))) 'Uninstall removes its shortcuts'

    Assert ((Install 'portable-handoff' '/TASKS="startup"') -eq 0) 'Install for portable-startup ownership check'
    $portableCommand = '"C:\Pulse Installer Test Portable\PulseWidget.exe"'
    Set-ItemProperty -LiteralPath $runKey -Name $testName -Value $portableCommand
    Assert ((Uninstall 'portable-uninstall') -eq 0) 'Uninstall after a portable copy takes over startup'
    Assert ((Get-Item -LiteralPath $runKey).GetValue($testName) -eq $portableCommand) 'Uninstall preserves startup owned by another copy'
}
finally {
    # Only the uniquely named test installation is eligible for cleanup.
    if (Test-Path -LiteralPath (Join-Path $destination 'unins000.exe')) { $null = Uninstall 'cleanup' }
    Remove-ItemProperty -LiteralPath $runKey -Name $testName -ErrorAction SilentlyContinue
    Assert ((Get-Item -LiteralPath $runKey).GetValue('PulseWidget') -eq $liveStartup) 'Real widget startup is unchanged'
    $after = if (Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
    Assert ($after -eq $liveSettings) 'Real widget preferences remain byte-for-byte unchanged'
    Assert ((Compare-Object $liveProcesses @(Get-Process PulseWidget -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)).Count -eq 0) 'Real running widget is not stopped or replaced by the tests'
}
Write-Output "Installer checks passed. Logs: $testRoot"
