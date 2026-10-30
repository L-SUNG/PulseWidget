$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $root ('artifacts\bridge-test-' + [guid]::NewGuid().ToString('N'))
$data = Join-Path $testRoot 'data'
$claude = Join-Path $testRoot 'claude'
New-Item -ItemType Directory -Path $data, $claude -Force | Out-Null
$originalData = $env:PULSE_DATA_DIR
$env:PULSE_DATA_DIR = $data
$bridge = Join-Path $root 'dist\bridge\PulseBridge.exe'
function Assert($Condition, $Name) { if (-not $Condition) { throw "FAIL: $Name" }; Write-Output "PASS: $Name" }
try {
    '{"session_id":"fixture-a","prompt":"DO NOT STORE THIS","rate_limits":{"five_hour":{"used_percentage":23.5,"resets_at":2000000000},"seven_day":{"used_percentage":41.2,"resets_at":2000600000}},"secret":"NEVER STORE"}' | & $bridge statusline | Out-Null
    $usagePath = Join-Path $data 'claude\usage.json'
    $usage = Get-Content -Raw -LiteralPath $usagePath | ConvertFrom-Json
    Assert ($usage.rate_limits.seven_day.used_percentage -eq 41.2) 'Actual bridge stdin and weekly output'
    Assert ($usage.rate_limits.five_hour.used_percentage -eq 23.5) 'Actual bridge five hour output'
    Assert (-not ((Get-Content -Raw -LiteralPath $usagePath) -match 'secret|prompt|fixture-a|NEVER|STORE')) 'No sensitive input persisted'
    '{"session_id":"fixture-b"}' | & $bridge statusline | Out-Null
    $usage = Get-Content -Raw -LiteralPath $usagePath | ConvertFrom-Json
    Assert ($usage.rate_limits.seven_day.used_percentage -eq 41.2) 'New empty session preserves previously observed quota'
    $before = Get-Content -Raw -LiteralPath $usagePath
    '{"rate_limits":{"seven_day":{},"five_hour":{"used_percentage":null}}}' | & $bridge statusline | Out-Null
    Assert ((Get-Content -Raw -LiteralPath $usagePath) -eq $before) 'Malformed windows cannot erase quota or refresh its timestamp'
    $weekObserved = $usage.rate_limits.seven_day.updatedAt
    '{"rate_limits":{"five_hour":{"used_percentage":24,"resets_at":2000000000}}}' | & $bridge statusline | Out-Null
    $usage = Get-Content -Raw -LiteralPath $usagePath | ConvertFrom-Json
    Assert ($usage.rate_limits.five_hour.used_percentage -eq 24 -and $usage.rate_limits.seven_day.used_percentage -eq 41.2) 'Partial response preserves the other window across bridge restarts'
    Assert ($usage.rate_limits.seven_day.updatedAt -eq $weekObserved) 'Preserved weekly window keeps its own observation time'
    $usage.rate_limits.seven_day.resets_at = 1000000000
    $usage | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $usagePath -Encoding UTF8
    '{"rate_limits":{"five_hour":{"used_percentage":25,"resets_at":2000000000}}}' | & $bridge statusline | Out-Null
    $usage = Get-Content -Raw -LiteralPath $usagePath | ConvertFrom-Json
    Assert ($usage.rate_limits.seven_day.resets_at -eq 1000000000 -and $usage.rate_limits.seven_day.used_percentage -eq 41.2) 'Expired window retains its reset time rather than becoming fresh data'
    '{"session_id":"fixture-a","hook_event_name":"UserPromptSubmit"}' | & $bridge hook
    '{"session_id":"fixture-b","hook_event_name":"UserPromptSubmit"}' | & $bridge hook
    '{"session_id":"fixture-a","hook_event_name":"Stop"}' | & $bridge hook
    $states = @(Get-ChildItem -LiteralPath (Join-Path $data 'claude\sessions') -Filter '*.json' | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json })
    Assert (@($states | Where-Object state -eq 'working').Count -eq 1) 'Concurrent sessions retain independent activity'
    Assert (@($states | Where-Object state -eq 'idle').Count -eq 1) 'Stop event sets idle'
    $settingsPath = Join-Path $claude 'settings.json'
    '{"theme":"dark","hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo original"}]}]}}' | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    & (Join-Path $root 'scripts\Install-Claude.ps1') -ClaudeDirectory $claude -DataDirectory $data -BridgeDirectory (Join-Path $root 'dist\bridge')
    & (Join-Path $root 'scripts\Install-Claude.ps1') -ClaudeDirectory $claude -DataDirectory $data -BridgeDirectory (Join-Path $root 'dist\bridge')
    $config = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    Assert ($config.hooks.Stop.Count -eq 2) 'Install is idempotent and preserves original hook'
    & (Join-Path $root 'scripts\Uninstall-Claude.ps1') -ClaudeDirectory $claude -DataDirectory $data
    $config = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    Assert ($config.theme -eq 'dark' -and $config.hooks.Stop[0].hooks[0].command -eq 'echo original' -and -not $config.statusLine) 'Uninstall preserves unrelated settings'
    '{"statusLine":{"type":"command","command":"echo custom"}}' | Set-Content -LiteralPath $settingsPath -Encoding UTF8
    $refused = $false
    try { & (Join-Path $root 'scripts\Install-Claude.ps1') -ClaudeDirectory $claude -DataDirectory $data -BridgeDirectory (Join-Path $root 'dist\bridge') } catch { $refused = $true }
    Assert $refused 'Custom statusLine is never silently replaced'
    Write-Output '13 bridge/integration checks passed.'
}
finally { $env:PULSE_DATA_DIR = $originalData }
