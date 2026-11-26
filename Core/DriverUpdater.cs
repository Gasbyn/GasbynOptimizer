using System.Text.RegularExpressions;

namespace GasbynOptimizer.Core;

public sealed class DriverUpdate
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public string Manufacturer { get; init; } = "";
    public string Class { get; init; } = "";
    public string Date { get; init; } = "";
    public double SizeMb { get; init; }

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (Manufacturer.Length > 0) parts.Add(Manufacturer);
            if (Class.Length > 0) parts.Add(Class);
            if (Date.Length > 0) parts.Add("vydáno " + Date);
            if (SizeMb > 0) parts.Add(SizeMb.ToString("0.#") + " MB");
            return string.Join("  ·  ", parts);
        }
    }
}

public sealed record DriverSearchResult(bool Ok, List<DriverUpdate> Updates, string Message);
public sealed record DriverInstallResult(bool Ok, int Succeeded, int Failed, bool RebootRequired, string Message);

/// <summary>
/// Aktualizace ovladačů přes Windows Update (oficiální rozhraní Windows, bez programů třetích stran).
/// Nabídne jen ovladače, které pro tento počítač zveřejnil výrobce přes Windows Update.
/// </summary>
public static class DriverUpdater
{
    private static readonly Regex GuidPattern = new(@"^[0-9A-Fa-f]{8}(-[0-9A-Fa-f]{4}){3}-[0-9A-Fa-f]{12}$", RegexOptions.Compiled);

    private const string SearchCore = @"
$ErrorActionPreference = 'Stop'
$session = New-Object -ComObject Microsoft.Update.Session
$searcher = $session.CreateUpdateSearcher()
$found = $searcher.Search(""IsInstalled=0 and Type='Driver' and IsHidden=0"")
";

    public static DriverSearchResult Search()
    {
        const string script = SearchCore + @"
foreach ($u in $found.Updates) {
    $date = ''
    try { $date = $u.DriverVerDate.ToString('yyyy-MM-dd') } catch { }
    'DRV|' + $u.Identity.UpdateID + '|' + $u.Title + '|' + $u.DriverManufacturer + '|' + $u.DriverClass + '|' + $date + '|' + [math]::Round($u.MaxDownloadSize / 1MB, 1)
}
'END'";
        var wrapped = "try {" + script + "} catch { 'ERR|' + $_.Exception.Message }";
        var result = Shell.RunPowerShell(wrapped, 300000);
        var updates = new List<DriverUpdate>();
        string? error = null;
        var ended = false;

        foreach (var raw in result.Output.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("ERR|", StringComparison.Ordinal)) error = line[4..];
            else if (line == "END") ended = true;
            else if (line.StartsWith("DRV|", StringComparison.Ordinal))
            {
                var p = line.Split('|');
                if (p.Length < 7 || !GuidPattern.IsMatch(p[1])) continue;
                _ = double.TryParse(p[6].Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var size);
                updates.Add(new DriverUpdate { Id = p[1], Title = p[2], Manufacturer = p[3], Class = p[4], Date = p[5], SizeMb = size });
            }
        }

        if (error != null) return new DriverSearchResult(false, updates, "Windows Update ovladače nenašel: " + error);
        if (!ended) return new DriverSearchResult(false, updates, "Hledání ovladačů neskončilo (Windows Update neodpovídá). Zkus to znovu.");
        return new DriverSearchResult(true, updates, "");
    }

    public static DriverInstallResult Install(IReadOnlyCollection<DriverUpdate> selected)
    {
        var ids = selected.Select(d => d.Id).Where(id => GuidPattern.IsMatch(id)).ToList();
        if (ids.Count == 0) return new DriverInstallResult(false, 0, 0, false, "Není vybraný žádný ovladač.");

        var idList = string.Join(",", ids.Select(id => "'" + id + "'"));
        var script = "try {" + SearchCore + @"
$wanted = @(" + idList + @")
$collection = New-Object -ComObject Microsoft.Update.UpdateColl
foreach ($u in $found.Updates) {
    if ($wanted -contains $u.Identity.UpdateID) {
        if (-not $u.EulaAccepted) { $u.AcceptEula() }
        [void]$collection.Add($u)
    }
}
if ($collection.Count -eq 0) { 'RES|0|0|0'; exit 0 }
$downloader = $session.CreateUpdateDownloader()
$downloader.Updates = $collection
[void]$downloader.Download()
$installer = $session.CreateUpdateInstaller()
$installer.Updates = $collection
$outcome = $installer.Install()
$ok = 0; $bad = 0
for ($i = 0; $i -lt $collection.Count; $i++) {
    $code = $outcome.GetUpdateResult($i).ResultCode
    if ($code -eq 2 -or $code -eq 3) { $ok++ } else { $bad++ }
}
'RES|' + $ok + '|' + $bad + '|' + [int]$outcome.RebootRequired
} catch { 'ERR|' + $_.Exception.Message }";

        Log.Write("Instalace ovladačů přes Windows Update: " + ids.Count);
        var result = Shell.RunPowerShell(script, 1800000);

        foreach (var raw in result.Output.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("ERR|", StringComparison.Ordinal))
                return new DriverInstallResult(false, 0, ids.Count, false, "Instalace ovladačů selhala: " + line[4..]);
            if (line.StartsWith("RES|", StringComparison.Ordinal))
            {
                var p = line.Split('|');
                if (p.Length >= 4 && int.TryParse(p[1], out var ok) && int.TryParse(p[2], out var bad))
                    return new DriverInstallResult(bad == 0, ok, bad, p[3] == "1", "");
            }
        }
        return new DriverInstallResult(false, 0, ids.Count, false, "Instalace ovladačů neskončila (Windows Update neodpovídá).");
    }

    /// <summary>Zásada „Nezahrnovat ovladače do aktualizací Windows“ (tweak v kategorii Aktualizace) ovladače blokuje.</summary>
    public static bool BlockedByPolicy()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate", false);
            return key?.GetValue("ExcludeWUDriversInQualityUpdate") is int v && v == 1;
        }
        catch { return false; }
    }
}
