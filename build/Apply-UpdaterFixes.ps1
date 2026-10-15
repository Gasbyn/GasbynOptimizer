param(
    [string]$Path = "Services/AppUpdater.cs"
)

$ErrorActionPreference = "Stop"
$text = Get-Content -LiteralPath $Path -Raw

# 1) UTF-8 with BOM so Windows PowerShell shows Czech diacritics correctly
if ($text -match 'encoderShouldEmitUTF8Identifier: false') {
    $text = $text.Replace(
        'encoderShouldEmitUTF8Identifier: false',
        'encoderShouldEmitUTF8Identifier: true')
    Write-Host "UTF-8 BOM enabled for ApplyUpdate.ps1"
} else {
    Write-Host "UTF-8 BOM already set or pattern missing"
}

# 2) Console UTF-8 inside the update script
if ($text -notmatch '\[Console\]::OutputEncoding') {
    $old = "    `$ErrorActionPreference = 'Stop'`r`n    `$stageDirectory = Join-Path `$WorkDirectory 'stage'"
    $new = @"
    `$ErrorActionPreference = 'Stop'
    try {
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        `$OutputEncoding = [System.Text.Encoding]::UTF8
    } catch { }
    `$stageDirectory = Join-Path `$WorkDirectory 'stage'
"@
    if ($text.Contains($old)) {
        $text = $text.Replace($old, $new.TrimEnd())
        Write-Host "Console UTF-8 block added (CRLF)"
    } else {
        $old2 = "    `$ErrorActionPreference = 'Stop'`n    `$stageDirectory = Join-Path `$WorkDirectory 'stage'"
        if ($text.Contains($old2)) {
            $text = $text.Replace($old2, $new.TrimEnd().Replace("`r`n", "`n"))
            Write-Host "Console UTF-8 block added (LF)"
        } else {
            Write-Host "WARN: ErrorActionPreference block not found"
        }
    }
} else {
    Write-Host "Console UTF-8 already present"
}

# 3) Force-kill parent process if Wait-Process times out
$oldWait = @"
        `$parent = Get-Process -Id `$ParentProcessId -ErrorAction SilentlyContinue
        if (`$null -ne `$parent) {
            Wait-Process -Id `$ParentProcessId -Timeout 60 -ErrorAction Stop
        }
        if (Get-Process -Id `$ParentProcessId -ErrorAction SilentlyContinue) {
            throw 'Gasbyn Optimizer se nepodařilo bezpečně zavřít.'
        }
"@
$newWait = @"
        `$parent = Get-Process -Id `$ParentProcessId -ErrorAction SilentlyContinue
        if (`$null -ne `$parent) {
            try {
                Wait-Process -Id `$ParentProcessId -Timeout 45 -ErrorAction Stop
            } catch {
                Stop-Process -Id `$ParentProcessId -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
                try { Wait-Process -Id `$ParentProcessId -Timeout 20 -ErrorAction SilentlyContinue } catch { }
            }
        }
        if (Get-Process -Id `$ParentProcessId -ErrorAction SilentlyContinue) {
            Stop-Process -Id `$ParentProcessId -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
        }
        if (Get-Process -Id `$ParentProcessId -ErrorAction SilentlyContinue) {
            throw 'Gasbyn Optimizer se nepodarilo bezpecne zavrit pred aktualizaci (proces stale bezi).'
        }
"@

if ($text -match 'Stop-Process -Id \`$ParentProcessId -Force') {
    Write-Host "Force-kill wait already present"
} else {
    $replaced = $false
    foreach ($pair in @(
        @($oldWait.Replace("`n", "`r`n"), $newWait.Replace("`n", "`r`n")),
        @($oldWait, $newWait)
    )) {
        if ($text.Contains($pair[0].TrimEnd())) {
            $text = $text.Replace($pair[0].TrimEnd(), $pair[1].TrimEnd())
            $replaced = $true
            Write-Host "Force-kill wait logic applied"
            break
        }
    }
    if (-not $replaced) {
        # Looser match: only the Wait-Process 60 line
        if ($text -match 'Wait-Process -Id \`$ParentProcessId -Timeout 60') {
            $text = $text.Replace(
                'Wait-Process -Id $ParentProcessId -Timeout 60 -ErrorAction Stop',
                @"
try { Wait-Process -Id `$ParentProcessId -Timeout 45 -ErrorAction Stop } catch { Stop-Process -Id `$ParentProcessId -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 2 }
"@.Trim())
            Write-Host "Patched Wait-Process timeout only (fallback)"
        } else {
            Write-Host "WARN: Wait-Process block not found"
        }
    }
}

[System.IO.File]::WriteAllText((Resolve-Path $Path), $text)
Write-Host "AppUpdater.cs size: $((Get-Item $Path).Length)"
