param(
    [string]$WorkspacePath = "MainWindow.Workspace.cs"
)

$ErrorActionPreference = "Stop"
$ws = Get-Content -LiteralPath $WorkspacePath -Raw

if ($ws -notmatch 'EnsureTweakDeckHost\(gamingOnly\)') {
    $replaced = $false
    foreach ($pair in @(
        @("_workspaceSwitchStatuses.Clear();`r`n`r`n        if (gamingOnly)", "_workspaceSwitchStatuses.Clear();`r`n        EnsureTweakDeckHost(gamingOnly);`r`n`r`n        if (gamingOnly)"),
        @("_workspaceSwitchStatuses.Clear();`n`n        if (gamingOnly)", "_workspaceSwitchStatuses.Clear();`n        EnsureTweakDeckHost(gamingOnly);`n`n        if (gamingOnly)")
    )) {
        if ($ws.Contains($pair[0])) {
            $ws = $ws.Replace($pair[0], $pair[1])
            $replaced = $true
            Write-Host "Patched EnsureTweakDeckHost"
            break
        }
    }
    if (-not $replaced) { Write-Host "WARN: EnsureTweakDeckHost pattern not found" }
} else {
    Write-Host "EnsureTweakDeckHost already present"
}

if ($ws -notmatch '_tweakDeckHost is not null') {
    $old1 = "_workspaceSwitches[id] = toggle;`r`n        card.Child = root;`r`n        SecondaryContent.Children.Add(card);"
    $old2 = "_workspaceSwitches[id] = toggle;`n        card.Child = root;`n        SecondaryContent.Children.Add(card);"
    $new1 = "_workspaceSwitches[id] = toggle;`r`n        card.Child = root;`r`n        if (_tweakDeckHost is not null)`r`n        {`r`n            _tweakDeckHost.Children.Add(card);`r`n            _tweakDeckIndex.Add((id, card, title, description, badges, IsWorkspaceTweakIncompatible(id)));`r`n        }`r`n        else`r`n            SecondaryContent.Children.Add(card);"
    if ($ws.Contains($old1)) {
        $ws = $ws.Replace($old1, $new1)
        Write-Host "Patched deck host routing (CRLF)"
    } elseif ($ws.Contains($old2)) {
        $ws = $ws.Replace($old2, $new1.Replace("`r`n", "`n"))
        Write-Host "Patched deck host routing (LF)"
    } else {
        Write-Host "WARN: unique deck host pattern not found"
    }
} else {
    Write-Host "Deck host routing already present"
}

if ($ws.Contains("High Performance power plan")) {
    $pattern = '(?s)\r?\n\s*AddWorkspaceTweakCard\(\s*"High Performance power plan".*?\);'
    $ws2 = [regex]::Replace($ws, $pattern, "", 1)
    if ($ws2 -ne $ws) {
        $ws = $ws2
        Write-Host "Removed HP power plan card"
    } else {
        Write-Host "WARN: HP card regex did not match"
    }
} else {
    Write-Host "HP card already absent"
}

if ($ws -match 'AMD Radeon-only GPU tuning') {
    $idx = $ws.IndexOf('AMD Radeon-only GPU tuning')
    if ($idx -ge 0) {
        $regionStart = [Math]::Max(0, $idx - 800)
        $regionEnd = [Math]::Min($ws.Length, $idx + 1200)
        $region = $ws.Substring($regionStart, $regionEnd - $regionStart)
        if ($region -match 'IsEnabled = false') {
            $region2 = $region.Replace('IsEnabled = false,', 'IsEnabled = advanced,')
            $ws = $ws.Substring(0, $regionStart) + $region2 + $ws.Substring($regionEnd)
            Write-Host "Patched AMD IsEnabled = advanced"
        } else {
            Write-Host "AMD already enabled or pattern missing"
        }
    }
} else {
    Write-Host "AMD block not found"
}

[System.IO.File]::WriteAllText((Resolve-Path $WorkspacePath), $ws)
Write-Host "Workspace.cs size: $((Get-Item $WorkspacePath).Length)"
