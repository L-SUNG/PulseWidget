param([string]$ClaudeDirectory, [string]$DataDirectory)
$ErrorActionPreference = 'Stop'
if (-not $ClaudeDirectory) { $ClaudeDirectory = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE '.claude' } }
if (-not $DataDirectory) { $DataDirectory = Join-Path $env:LOCALAPPDATA 'PulseWidget' }
$path = Join-Path $ClaudeDirectory 'settings.json'
if (-not (Test-Path -LiteralPath $path)) { return }
$config = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
$quoted = '"' + (Join-Path $DataDirectory 'bridge\PulseBridge.exe').Replace('\', '/') + '"'
if ($config.statusLine -and $config.statusLine.command -eq ($quoted + ' statusline')) { $config.PSObject.Properties.Remove('statusLine') }
if ($config.hooks) {
    foreach ($eventName in @($config.hooks.PSObject.Properties.Name)) {
        $remaining = @()
        foreach ($group in @($config.hooks.$eventName)) {
            $keep = @($group.hooks | Where-Object { $_.command -ne ($quoted + ' hook') })
            if ($keep.Count -gt 0) { $group.hooks = $keep; $remaining += $group }
        }
        if ($remaining.Count -eq 0) { $config.hooks.PSObject.Properties.Remove($eventName) }
        else { $config.hooks.$eventName = $remaining }
    }
}
Copy-Item -LiteralPath $path -Destination ($path + '.pulse-uninstall-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
[System.IO.File]::WriteAllText($path, ($config | ConvertTo-Json -Depth 100), (New-Object System.Text.UTF8Encoding($false)))
Write-Output 'Only Pulse hooks and statusLine have been removed. Other settings are preserved.'
