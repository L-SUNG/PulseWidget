param(
    [switch]$FrameworkDependent,
    [string]$OutputDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')][string]$Version = '1.0.0'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $root 'dist' }
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
# Isolate incremental publish state. Reusing WPF single-file publish caches can
# drop native runtime DLLs from a previously populated publish directory.
$buildState = Join-Path $root ('artifacts\build-' + [guid]::NewGuid().ToString('N'))
$widgetState = (Join-Path $buildState 'widget') + [IO.Path]::DirectorySeparatorChar
$bridgeState = (Join-Path $buildState 'bridge') + [IO.Path]::DirectorySeparatorChar
dotnet publish (Join-Path $root 'src\PulseWidget.csproj') -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true "-p:Version=$Version" "-p:IntermediateOutputPath=$widgetState" -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw 'Widget publish failed.' }
dotnet publish (Join-Path $root 'bridge\PulseBridge.csproj') -c Release -r win-x64 --self-contained $selfContained -p:PublishSingleFile=true "-p:Version=$Version" "-p:IntermediateOutputPath=$bridgeState" -o (Join-Path $output 'bridge') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Bridge publish failed.' }
if (-not $FrameworkDependent) {
    foreach ($native in @('D3DCompiler_47_cor3.dll','PenImc_cor3.dll','PresentationNative_cor3.dll','vcruntime140_cor3.dll','wpfgfx_cor3.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $output $native))) { throw "Missing WPF runtime: $native" }
    }
}
New-Item -ItemType Directory -Force -Path (Join-Path $output 'scripts') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Claude.ps1'), (Join-Path $PSScriptRoot 'Uninstall-Claude.ps1'), (Join-Path $PSScriptRoot 'Claude-Integration.ps1') -Destination (Join-Path $output 'scripts') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $output -Force
Write-Output "Ready: $output\PulseWidget.exe"
