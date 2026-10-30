param([string]$ClaudeDirectory, [string]$DataDirectory, [string]$BridgeDirectory)
$ErrorActionPreference = 'Stop'
if (-not $ClaudeDirectory) { $ClaudeDirectory = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE '.claude' } }
if (-not $DataDirectory) { $DataDirectory = Join-Path $env:LOCALAPPDATA 'PulseWidget' }
if (-not $BridgeDirectory) { $BridgeDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) 'bridge' }
$source = Join-Path $BridgeDirectory 'PulseBridge.exe'
if (-not (Test-Path -LiteralPath $source)) { throw 'Build the release first: scripts/Build.ps1' }
$destination = Join-Path $DataDirectory 'bridge'
$exe = Join-Path $destination 'PulseBridge.exe'
$settingsPath = Join-Path $ClaudeDirectory 'settings.json'
New-Item -ItemType Directory -Force -Path $ClaudeDirectory, $destination | Out-Null
$config = if (Test-Path -LiteralPath $settingsPath) { Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json } else { [pscustomobject]@{} }
# Claude invokes commands through its configured shell; forward-slash paths work
# in Git Bash as well as PowerShell. Keep quoting around spaces.
$quoted = '"' + $exe.Replace('\', '/') + '"'
$statusCommand = $quoted + ' statusline'
$hookCommand = $quoted + ' hook'
$statusProperty = $config.PSObject.Properties['statusLine']
if ($statusProperty -and $config.statusLine -and $config.statusLine.command -ne $statusCommand) {
    throw 'A custom Claude statusLine already exists. It has been left unchanged. See README for manual integration.'
}
$backup = $settingsPath + '.pulse-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
if (Test-Path -LiteralPath $settingsPath) { Copy-Item -LiteralPath $settingsPath -Destination $backup }
Get-ChildItem -LiteralPath $BridgeDirectory -File | Copy-Item -Destination $destination -Force
$config | Add-Member -MemberType NoteProperty -Name statusLine -Value ([pscustomobject]@{type='command'; command=$statusCommand}) -Force
if (-not $config.PSObject.Properties['hooks'] -or -not $config.hooks) { $config | Add-Member -MemberType NoteProperty -Name hooks -Value ([pscustomobject]@{}) -Force }
$events = @('SessionStart','UserPromptSubmit','PreToolUse','PostToolUse','PostToolUseFailure','PermissionRequest','Notification','Stop','StopFailure','SessionEnd')
foreach ($eventName in $events) {
    $existing = @()
    if ($config.hooks.PSObject.Properties[$eventName]) { $existing = @($config.hooks.$eventName) }
    $found = @($existing | ForEach-Object { $_.hooks } | Where-Object { $_.command -eq $hookCommand }).Count -gt 0
    if (-not $found) {
        $entry = [pscustomobject]@{ hooks = @([pscustomobject]@{type='command'; command=$hookCommand; timeout=3}) }
        if ($eventName -eq 'Notification') { $entry | Add-Member -NotePropertyName matcher -NotePropertyValue 'permission_prompt' }
        $config.hooks | Add-Member -MemberType NoteProperty -Name $eventName -Value @($existing + $entry) -Force
    }
}
$json = $config | ConvertTo-Json -Depth 100
$temp = $settingsPath + '.pulse.tmp'
[System.IO.File]::WriteAllText($temp, $json, (New-Object System.Text.UTF8Encoding($false)))
Move-Item -LiteralPath $temp -Destination $settingsPath -Force
Write-Output 'Claude integration configured. Check the widget details to verify quota reception; Desktop activity hooks alone do not confirm quota delivery.'
Write-Output "Backup: $backup"
