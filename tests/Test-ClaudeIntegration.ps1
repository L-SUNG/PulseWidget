param([string]$ScriptsDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $ScriptsDirectory) { $ScriptsDirectory = Join-Path $root 'scripts' }
$install = Join-Path $ScriptsDirectory 'Install-Claude.ps1'
$uninstall = Join-Path $ScriptsDirectory 'Uninstall-Claude.ps1'
$testRoot = Join-Path $root ('artifacts\claude-integration-test-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $testRoot 'source'
New-Item -ItemType Directory -Path $source -Force | Out-Null
# The installer copies this fixture but never executes it. Bridge execution has
# its own tests in Test-Bridge.ps1.
[IO.File]::WriteAllText((Join-Path $source 'PulseBridge.exe'), 'fixture')
$originalProfile = $env:USERPROFILE
$originalLocalData = $env:LOCALAPPDATA
$originalConfig = $env:CLAUDE_CONFIG_DIR
$script:checks = 0
function Assert($Condition, [string]$Label) {
    if (-not $Condition) { throw "FAIL: $Label" }
    $script:checks++
    Write-Output "PASS: $Label"
}
function Save-Settings($Config) {
    [IO.File]::WriteAllText($settingsPath, ($Config | ConvertTo-Json -Depth 100), (New-Object Text.UTF8Encoding($false)))
}
function Get-Hooks($Config) {
    @($Config.hooks.PSObject.Properties | ForEach-Object { $_.Value } | ForEach-Object { $_.hooks })
}
try {
    # Exercise the real defaults without touching the signed-in user's settings.
    $env:USERPROFILE = Join-Path $testRoot '새 계정 New Account'
    $env:LOCALAPPDATA = Join-Path $env:USERPROFILE 'AppData\Local'
    $env:CLAUDE_CONFIG_DIR = $null
    $claude = Join-Path $env:USERPROFILE '.claude'
    $data = Join-Path $env:LOCALAPPDATA 'PulseWidget'
    $settingsPath = Join-Path $claude 'settings.json'
    New-Item -ItemType Directory -Path $claude -Force | Out-Null
    $expectedExe = (Join-Path $data 'bridge\PulseBridge.exe').Replace('\', '/')
    $expectedHook = '"' + $expectedExe + '" hook'
    $expectedStatus = '"' + $expectedExe + '" statusline'
    $oldExe = 'C:/Users/GIIC_User/AppData/Local/PulseWidget/bridge/PulseBridge.exe'
    $events = @('SessionStart','UserPromptSubmit','PreToolUse','PostToolUse','PostToolUseFailure','PermissionRequest','Notification','Stop','StopFailure','SessionEnd')
    $fixture = [pscustomobject]@{
        theme = 'dark'
        localeLabel = '개인 설정'
        statusLine = [pscustomobject]@{type='command'; command=('"' + $oldExe + '" statusline'); padding=2}
        hooks = [pscustomobject]@{}
    }
    foreach ($eventName in $events) {
        $fixture.hooks | Add-Member -NotePropertyName $eventName -NotePropertyValue @(
            [pscustomobject]@{hooks=@([pscustomobject]@{type='command'; command=('"' + $oldExe + '" hook'); timeout=3})},
            [pscustomobject]@{hooks=@([pscustomobject]@{type='command'; command=$expectedHook; timeout=3})}
        )
    }
    $otherHook = [pscustomobject]@{type='command'; command='echo original'; timeout=17; async=$true}
    $wrapper = [pscustomobject]@{type='command'; command=('"' + $oldExe + '" hook && echo custom')}
    $prompt = [pscustomobject]@{type='prompt'; prompt='Keep this prompt hook'; command=('"' + $oldExe + '" hook')}
    $fixture.hooks.PreToolUse += [pscustomobject]@{matcher='Bash'; hooks=@(
        [pscustomobject]@{type='command'; command=('"' + $oldExe.Replace('/', '\') + '" hook')},
        $otherHook, $wrapper, $prompt
    )}
    $fixture.hooks | Add-Member -NotePropertyName CustomEvent -NotePropertyValue @([pscustomobject]@{matcher='custom'; hooks=@($otherHook)})
    $fixture.hooks | Add-Member -NotePropertyName EmptyEvent -NotePropertyValue @()
    Save-Settings $fixture
    $original = [IO.File]::ReadAllText($settingsPath)
    & $install -BridgeDirectory $source
    $config = Get-Content -Raw -Encoding UTF8 -LiteralPath $settingsPath | ConvertFrom-Json
    Assert ($config.statusLine.command -ceq $expectedStatus) 'Old account statusLine migrates to the current account defaults'
    Assert ($config.statusLine.padding -eq 2 -and $config.theme -eq 'dark' -and $config.localeLabel -ceq '개인 설정') 'Status line options and non-ASCII settings are preserved'
    Assert ((Get-FileHash -LiteralPath (Join-Path $source 'PulseBridge.exe')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $data 'bridge\PulseBridge.exe')).Hash) 'Bridge is installed at the newly configured path'
    foreach ($eventName in $events) {
        $pulse = @($config.hooks.$eventName | ForEach-Object { $_.hooks } | Where-Object { $_.type -eq 'command' -and $_.command -ceq $expectedHook })
        Assert ($pulse.Count -eq 1 -and $pulse[0].timeout -eq 3) "Exactly one current Pulse hook: $eventName"
    }
    Assert ($config.hooks.Notification[0].matcher -eq 'permission_prompt') 'Notification keeps the permission prompt filter'
    $mixed = $config.hooks.PreToolUse[0]
    Assert ($mixed.matcher -eq 'Bash' -and $mixed.hooks.Count -eq 3 -and ($mixed.hooks[0] | ConvertTo-Json -Compress) -ceq ($otherHook | ConvertTo-Json -Compress)) 'Mixed group keeps unrelated hook settings and matcher'
    Assert ($mixed.hooks[1].command -ceq $wrapper.command -and $mixed.hooks[2].type -eq 'prompt') 'Wrapper commands and non-command hooks are preserved'
    Assert ($config.hooks.CustomEvent[0].matcher -eq 'custom' -and $config.hooks.PSObject.Properties['EmptyEvent']) 'Unrelated events and empty arrays are preserved'
    $backups = @(Get-ChildItem -LiteralPath $claude -Filter 'settings.json.pulse-backup-*')
    Assert ($backups.Count -eq 1 -and [IO.File]::ReadAllText($backups[0].FullName) -ceq $original) 'Migration backs up the exact original settings'
    $installed = [IO.File]::ReadAllText($settingsPath)
    & $install -BridgeDirectory $source
    Assert ([IO.File]::ReadAllText($settingsPath) -ceq $installed) 'Reinstall is byte-for-byte idempotent with no duplicate hooks'
    & $uninstall
    $config = Get-Content -Raw -Encoding UTF8 -LiteralPath $settingsPath | ConvertFrom-Json
    Assert (-not $config.statusLine -and @(Get-Hooks $config | Where-Object { $_.command -ceq $expectedHook }).Count -eq 0) 'Uninstall removes the migrated integration'
    Assert ($config.hooks.PreToolUse[0].hooks.Count -eq 3 -and $config.hooks.CustomEvent[0].matcher -eq 'custom' -and $config.hooks.PSObject.Properties['EmptyEvent'] -and $config.localeLabel -ceq '개인 설정') 'Uninstall preserves unrelated hook groups, events and non-ASCII settings'

    # Removing a copied configuration must also work before any migration.
    [IO.File]::WriteAllText($settingsPath, $original)
    & $uninstall
    $config = Get-Content -Raw -Encoding UTF8 -LiteralPath $settingsPath | ConvertFrom-Json
    Assert (-not $config.statusLine -and @(Get-Hooks $config).Count -eq 4) 'Uninstall removes old and current Pulse paths without requiring a successful install'

    # Accept formats that still directly identify the old executable, even when
    # the former drive or user profile no longer exists.
    foreach ($oldCommand in @(
        '"C:\Users\Old Account\AppData\Local\PulseWidget\bridge\PulseBridge.exe" statusline',
        "'D:/Moved Pulse/bridge/PulseBridge.exe' statusline",
        'E:/Portable/PulseBridge.exe statusline',
        '"\\old-server\share\Pulse\PulseBridge.exe" statusline'
    )) {
        Save-Settings ([pscustomobject]@{statusLine=[pscustomobject]@{type='command'; command=$oldCommand}})
        & $install -BridgeDirectory $source | Out-Null
        $config = Get-Content -Raw -Encoding UTF8 -LiteralPath $settingsPath | ConvertFrom-Json
        Assert ($config.statusLine.command -ceq $expectedStatus) "Migrates direct path variant: $oldCommand"
    }

    foreach ($customCommand in @(
        'echo custom',
        ('"' + $oldExe + '" statusline && echo custom'),
        ('powershell -File "' + $oldExe + '" statusline'),
        '"C:/Other/NotPulseBridge.exe" statusline',
        'PulseBridge.exe statusline',
        ('"' + $oldExe + '" statusline --custom')
    )) {
        Save-Settings ([pscustomobject]@{statusLine=[pscustomobject]@{type='command'; command=$customCommand}; hooks=$fixture.hooks})
        $before = [IO.File]::ReadAllText($settingsPath)
        $backupCount = @(Get-ChildItem -LiteralPath $claude -Filter 'settings.json.pulse-backup-*').Count
        $refused = $false
        try { & $install -BridgeDirectory $source | Out-Null } catch { $refused = $_.Exception.Message -like 'A custom Claude statusLine already exists*' }
        Assert ($refused -and [IO.File]::ReadAllText($settingsPath) -ceq $before -and @(Get-ChildItem -LiteralPath $claude -Filter 'settings.json.pulse-backup-*').Count -eq $backupCount) "Custom statusLine is refused without modifying settings: $customCommand"
    }

    $env:CLAUDE_CONFIG_DIR = Join-Path $testRoot 'custom config'
    $customData = Join-Path $testRoot 'custom data'
    & $install -DataDirectory $customData -BridgeDirectory $source | Out-Null
    $config = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $env:CLAUDE_CONFIG_DIR 'settings.json') | ConvertFrom-Json
    Assert ($config.statusLine.command -ceq ('"' + (Join-Path $customData 'bridge\PulseBridge.exe').Replace('\', '/') + '" statusline')) 'Explicit data directory and CLAUDE_CONFIG_DIR overrides remain supported'
    Write-Output "$script:checks Claude integration checks passed. Fixtures: $testRoot"
}
finally {
    $env:USERPROFILE = $originalProfile
    $env:LOCALAPPDATA = $originalLocalData
    $env:CLAUDE_CONFIG_DIR = $originalConfig
}
