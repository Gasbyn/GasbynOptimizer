namespace GasbynOptimizer.Core;

/// <summary>Jedno doporučení: optimalizace, důvody a míra jistoty.</summary>
public sealed class Advice
{
    public required Tweak Tweak { get; init; }
    public required int Score { get; init; }
    public required IReadOnlyList<string> Reasons { get; init; }
    public string Risk { get; init; } = "nízké";
    public string ReasonText => string.Join(" ", Reasons);
}

public sealed record AdvisorInput(Tweak Tweak, TweakState State, bool Supported, bool Recommended);

/// <summary>
/// Chytrý detektor: lokální pravidla nad zjištěným hardwarem a softwarem. Nic neodesílá po síti.
/// Skóre vychází ze značky „doporučeno“ a z kontextu počítače (notebook/PC, SSD/HDD, RAM, GPU, nainstalované programy).
/// Rizikové a experimentální věci navrhuje jen v pokročilém režimu a s nižším skóre.
/// </summary>
public static class Advisor
{
    public static List<Advice> Recommend(IEnumerable<AdvisorInput> items, SystemProfile p, bool advancedMode, int max = 14)
    {
        var hasObs = AppPresence.ByExe("obs64.exe", @"%ProgramFiles%\obs-studio\bin\64bit\obs64.exe");
        var hasSteam = AppPresence.ByExe("steam.exe", @"%ProgramFiles(x86)%\Steam\steam.exe");
        var hasEpic = AppPresence.ByExe("EpicGamesLauncher.exe", @"%ProgramFiles(x86)%\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe");
        var gamer = hasSteam || hasEpic || p.HasGpu(GpuVendor.Nvidia) || p.HasGpu(GpuVendor.Amd) && p.IsLaptop != true;
        var lowRam = p.RamGb > 0 && p.RamGb <= 8;
        var laptop = p.IsLaptop == true;
        var hdd = p.SystemDriveIsSsd == false;
        var ssd = p.SystemDriveIsSsd == true;

        var result = new List<Advice>();

        foreach (var input in items)
        {
            var t = input.Tweak;
            if (t.ApplyOnly || !input.Supported) continue;
            if (input.State is TweakState.On or TweakState.NotApplicable) continue;
            if ((t.Experimental || t.Category == CatalogAdvanced.CatAdvanced) && !advancedMode) continue;

            var key = (t.Id + " " + t.Name + " " + t.Category).ToLowerInvariant();
            var score = input.Recommended ? 50 : 15;
            var reasons = new List<string>();
            if (input.Recommended) reasons.Add("Je to ověřená, bezpečná úprava, kterou doporučujeme většině počítačů.");

            bool Has(params string[] words) => words.Any(w => key.Contains(w));

            if (Has("telemetr", "reklam", "inzerc", "advert", "sledov", "diagnostic", "diagnost", "cortana", "webové hledání", "web-search"))
            {
                score += 12;
                reasons.Add("Zvyšuje soukromí a ubírá práci na pozadí.");
            }

            if (gamer && Has("hra", "hraní", "game", "fps", "latenc", "gpu", "hags", "plánování grafiky", "herní"))
            {
                score += 22;
                reasons.Add(hasSteam || hasEpic
                    ? "Máš nainstalovaný herní launcher, takže tohle ocení při hraní."
                    : "Máš dedikovanou grafiku, takže se to hodí do her.");
            }

            if (lowRam && Has("efekt", "animac", "průhled", "transparen", "visual", "přednačít", "prefetch", "spouštěn", "bloat", "debloat"))
            {
                score += 18;
                reasons.Add("Máš jen " + Math.Round(p.RamGb) + " GB paměti RAM, každý ušetřený proces pomůže.");
            }

            if (hdd && Has("sysmain", "superfetch", "prefetch"))
            {
                score -= 60;
            }
            else if (ssd && Has("sysmain", "superfetch", "prefetch", "defrag", "optimalizace disku", "indexov"))
            {
                score += 14;
                reasons.Add("Systém běží na SSD, kde tohle nepřináší téměř nic.");
            }

            if (laptop && Has("ultimate", "vysoký výkon", "high performance", "výkonnostní plán", "plán napájení", "usb suspend", "úspora energie", "core parking"))
            {
                score -= 45;
            }
            else if (!laptop && Has("ultimate", "vysoký výkon", "výkonnostní plán"))
            {
                score += 10;
                reasons.Add("Stolní PC nemá problém s baterií, takže plný výkon dává smysl.");
            }

            if (laptop && Has("baterie", "battery", "úspora"))
            {
                score += 10;
                reasons.Add("Notebook ocení delší výdrž.");
            }

            if (hasObs && Has("game bar", "herní panel", "capture", "zachyt"))
            {
                score -= 25;
                reasons.Add("Používáš OBS, proto u nahrávání raději nic neměnit.");
            }

            var risky = t.Experimental || t.Confirm != null || t.Category == CatalogAdvanced.CatAdvanced;
            if (risky) score -= 20;
            else if (t.RequiresRestart) score -= 3;

            if (reasons.Count == 0) continue;
            if (score < 45) continue;

            result.Add(new Advice
            {
                Tweak = t,
                Score = Math.Min(score, 99),
                Reasons = reasons,
                Risk = risky ? "střední" : "nízké"
            });
        }

        return result.OrderByDescending(a => a.Score).ThenBy(a => a.Tweak.Name).Take(max).ToList();
    }
}
