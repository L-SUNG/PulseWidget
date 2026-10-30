param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')][string]$Version = '1.0.0',
    [string]$CompilerPath,
    [string]$OutputDirectory,
    [switch]$SkipPublish,
    [string]$PayloadDirectory,
    [switch]$TestInstall
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $CompilerPath) {
    $candidates = @(
        (Join-Path $root 'artifacts\tools\inno-7.1.1\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
    )
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { $candidates += $command.Source }
    $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'Inno Setup 7.1.1+ is required. Install it from https://jrsoftware.org/isdl.php or pass -CompilerPath.'
}
if (-not $PayloadDirectory) { $PayloadDirectory = Join-Path $root ('artifacts\installer-payload-' + $Version) }
$PayloadDirectory = [IO.Path]::GetFullPath($PayloadDirectory)
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts\installer' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $SkipPublish) { & (Join-Path $PSScriptRoot 'Build.ps1') -OutputDirectory $PayloadDirectory -Version $Version }
foreach ($required in @('PulseWidget.exe','D3DCompiler_47_cor3.dll','PenImc_cor3.dll','PresentationNative_cor3.dll','vcruntime140_cor3.dll','wpfgfx_cor3.dll','bridge\PulseBridge.exe','scripts\Install-Claude.ps1','scripts\Uninstall-Claude.ps1','README.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PayloadDirectory $required))) { throw "Missing installer payload: $required" }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$name = "PulseWidget-$Version-Setup-x64" + $(if ($TestInstall) { '-TEST' } else { '' })
$arguments = @('--quiet', "--define=AppVersion=$Version", "--define=PayloadDir=$PayloadDirectory", "--output-dir=$OutputDirectory", "--output-filename=$name")
if ($TestInstall) { $arguments += '--define=TestInstall'; $arguments += '--no-compression' }
& $CompilerPath @arguments (Join-Path $root 'installer\PulseWidget.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $OutputDirectory ($name + '.exe')
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText($installer + '.sha256', "$hash  $name.exe`n", (New-Object Text.UTF8Encoding($false)))
Write-Output "Installer: $installer"
