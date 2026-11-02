# Only direct, absolute PulseBridge commands belong to this integration. Do not
# claim wrappers, chained commands, other executables or non-command hook types.
function Test-PulseClaudeCommand($Entry, [string]$Mode) {
    if (-not $Entry -or $Entry.type -ne 'command' -or $Entry.command -isnot [string]) { return $false }
    $match = [regex]::Match($Entry.command, '\A\s*(?:"(?<path>[^"]+)"|''(?<path>[^'']+)''|(?<path>[^\s"'';&|<>]+))\s+(?<mode>statusline|hook)\s*\z', 'IgnoreCase')
    if (-not $match.Success -or $match.Groups['mode'].Value -ne $Mode) { return $false }
    $path = $match.Groups['path'].Value.Replace('\', '/')
    return $path -match '\A(?:[a-z]:/|//[^/]+/[^/]+/)(?:[^/]+/)*PulseBridge\.exe\z'
}

function Remove-PulseClaudeHooks($Config) {
    if (-not $Config.hooks) { return }
    foreach ($eventName in @($Config.hooks.PSObject.Properties.Name)) {
        $remaining = @()
        $changed = $false
        foreach ($group in @($Config.hooks.$eventName)) {
            $keep = @($group.hooks | Where-Object { -not (Test-PulseClaudeCommand $_ 'hook') })
            if ($keep.Count -eq @($group.hooks).Count) {
                $remaining += $group
                continue
            }
            $changed = $true
            if ($keep.Count -gt 0) { $group.hooks = $keep; $remaining += $group }
        }
        if (-not $changed) { continue }
        if ($remaining.Count -eq 0) { $Config.hooks.PSObject.Properties.Remove($eventName) }
        else { $Config.hooks.$eventName = $remaining }
    }
}
