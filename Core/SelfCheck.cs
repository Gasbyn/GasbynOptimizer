using System.Text;

namespace GasbynOptimizer.Core;

public sealed record SelfCheckIssue(Tweak Tweak, TweakState Now);

public sealed record SelfCheckResult(int Checked, IReadOnlyList<SelfCheckIssue> Drifted, IReadOnlyList<string> Problems, DateTime When)
{
    public bool AllOk => Drifted.Count == 0 && Problems.Count == 0;
}

/// <summary>
/// Samokontrola: porovná to, co aplikace kdysi zapnula (záznamy v state.json), se skutečným stavem systému.
/// Windows Update nebo jiný program občas nastavení vrátí. Kontrola nic nemění, jen to zjistí a nabídne opravu.
/// </summary>
public static class SelfCheck
{
    public static SelfCheckResult Run(TweakService service, IReadOnlyList<Tweak> tweaks)
    {
        var drifted = new List<SelfCheckIssue>();
        var problems = new List<string>();
        var checkedCount = 0;

        try
        {
            AppPaths.EnsureDataDir();
            var probe = Path.Combine(AppPaths.DataDir, ".write-test");
            File.WriteAllText(probe, "ok", new UTF8Encoding(false));
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            problems.Add("Datová složka aplikace není zapisovatelná: " + ex.Message);
        }

        try
        {
            if (File.Exists(AppPaths.StatePath + ".bad"))
                problems.Add("Soubor se zálohou byl poškozený a byl uložen jako state.json.bad. Záloha původních hodnot se mohla ztratit.");
        }
        catch { /* bez práv se jen nezjistí */ }

        var store = BackupStore.Load();
        var byId = tweaks.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var id in store.Ids.ToList())
        {
            if (!byId.TryGetValue(id, out var tweak) || tweak.ApplyOnly) continue;
            checkedCount++;
            try
            {
                var state = service.Scan(tweak);
                if (state is TweakState.Off or TweakState.Partial)
                    drifted.Add(new SelfCheckIssue(tweak, state));
            }
            catch (Exception ex)
            {
                problems.Add("Optimalizaci „" + tweak.Name + "“ se nepodařilo zkontrolovat: " + ex.Message);
            }
        }

        Log.Write("Samokontrola: zkontrolováno " + checkedCount + ", vráceno systémem " + drifted.Count + ", problémů " + problems.Count);
        return new SelfCheckResult(checkedCount, drifted, problems, DateTime.Now);
    }
}
