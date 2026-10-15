param(
    [string]$WorkspacePath = "MainWindow.Workspace.cs"
)

$ErrorActionPreference = "Stop"
$ws = Get-Content -LiteralPath $WorkspacePath -Raw

if ($ws -notmatch 'EnsureTweakDeckHost\(gamingOnly\)') {
    $patterns = @(
        @("_workspaceSwitchStatuses.Clear();`r`n`r`n        if (gamingOnly)", "_workspaceSwitchStatuses.Clear();`r`n        EnsureTweakDeckHost(gamingOnly);`r`n`r`n        if (gamingOnly)"),
        @("_workspaceSwitchStatuses.Clear();`n`n        if (gamingOnly)", "_workspaceSwitchStatuses.Clear();`n        EnsureTweakDeckHost(gamingOnly);`n`n        if (gamingOnly)")
    )
    foreach ($p in $patterns) {
        if ($ws.Contains($p[0])) {
            $ws = $ws.Replace($p[0], $p[1])
            Write-Host "Patched EnsureTweakDeckHost"
            break
        }
    }
} else {
    Write-Host "EnsureTweakDeckHost already present"
}

if ($ws -notmatch '_tweakDeckHost is not null') {
    $insert = @"
        card.Child = root;
        if (_tweakDeckHost is not null)
        {
            _tweakDeckHost.Children.Add(card);
            _tweakDeckIndex.Add((id, card, title, description, badges, incompatible));
        }
        else
            SecondaryContent.Children.Add(card);
"@
    $needles = @(
        "        card.Child = root;`r`n        SecondaryContent.Children.Add(card);",
        "        card.Child = root;`n        SecondaryContent.Children.Add(card);"
    )
    $done = $false
    foreach ($n in $needles) {
        if ($ws.Contains($n)) {
            $ws = $ws.Replace($n, $insert.TrimEnd())
            Write-Host "Patched deck host routing"
            $done = $true
            break
        }
    }
    if (-not $done) { Write-Host "WARN: deck host routing pattern not found" }
} else {
    Write-Host "Deck host routing already present"
}

if ($ws.Contains("High Performance power plan")) {
    $pattern = '(?s)\r?\n\s*AddWorkspaceTweakCard\(\s*"High Performance power plan".*?"SPOT.*?"\);'
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

if ($ws -notmatch 'Nepodporovan') {
    $newBlock = @"
            if (_loadingWorkspaceSwitches) return;
            if (incompatible)
            {
                var ok = MessageBox.Show(
                    "Tato optimalizace neni podporovana na tve sestave." +
                    Environment.NewLine + Environment.NewLine +
                    "Opravdu ji chces zapnout?",
                    "Nepodporovana optimalizace",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (ok != MessageBoxResult.Yes)
                {
                    _loadingWorkspaceSwitches = true;
                    toggle.IsChecked = false;
                    _loadingWorkspaceSwitches = false;
                    return;
                }
            }
            await ChangeWorkspaceTweakAsync(id, true, toggle, status);
"@
    $olds = @(
        "            if (!_loadingWorkspaceSwitches)`r`n                await ChangeWorkspaceTweakAsync(id, true, toggle, status);",
        "            if (!_loadingWorkspaceSwitches)`n                await ChangeWorkspaceTweakAsync(id, true, toggle, status);"
    )
    $done = $false
    foreach ($o in $olds) {
        if ($ws.Contains($o)) {
            $ws = $ws.Replace($o, $newBlock.TrimEnd())
            Write-Host "Patched incompat confirmation"
            $done = $true
            break
        }
    }
    if (-not $done) { Write-Host "WARN: incompat confirm pattern not found" }
} else {
    Write-Host "Incompat confirm already present"
}

if ($ws -match 'AMD Radeon-only GPU tuning' -and $ws -match 'IsEnabled = false') {
    $ws = $ws.Replace("IsEnabled = false,", "IsEnabled = advanced,")
    $ws = $ws.Replace(
        'ToolTip = warning,',
        'ToolTip = advanced ? "Pokrocily rezim: potvrd omezeni." : "Zamceno - zapni Pokrocily rezim.",')
    $ws = $ws.Replace(
        'AMD Radeon-only GPU tuning — unavailable',
        'AMD Radeon-only GPU tuning')
    $ws = $ws.Replace(
        'AMD Radeon-only GPU tuning - unavailable',
        'AMD Radeon-only GPU tuning')
    Write-Host "Patched AMD unlock (basic)"
} else {
    Write-Host "AMD pattern skipped or already patched"
}

[System.IO.File]::WriteAllText((Resolve-Path $WorkspacePath), $ws)
Write-Host "Workspace.cs size: $((Get-Item $WorkspacePath).Length)"
