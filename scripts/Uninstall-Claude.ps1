param([string]$ClaudeDirectory, [string]$DataDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Claude-Integration.ps1')
if (-not $ClaudeDirectory) { $ClaudeDirectory = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE '.claude' } }
if (-not $DataDirectory) { $DataDirectory = Join-Path $env:LOCALAPPDATA 'PulseWidget' }
$path = Join-Path $ClaudeDirectory 'settings.json'
if (-not (Test-Path -LiteralPath $path)) { return }
$config = Get-Content -Raw -Encoding UTF8 -LiteralPath $path | ConvertFrom-Json
if (Test-PulseClaudeCommand $config.statusLine 'statusline') { $config.PSObject.Properties.Remove('statusLine') }
Remove-PulseClaudeHooks $config
Copy-Item -LiteralPath $path -Destination ($path + '.pulse-uninstall-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
[System.IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 100), (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'Only Pulse hooks and statusLine have been removed. Other settings are preserved.'
