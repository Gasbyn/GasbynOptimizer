using System.Text;

namespace GasbynOptimizer.Core;

public enum TweakState { On, Off, Partial, NotApplicable }

public sealed class Tweak
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required IReadOnlyList<TweakOp> Ops { get; init; }

    /// <summary>Zahrnout do tlačítka „Zapnout doporučené“.</summary>
    public bool Recommended { get; init; }

    /// <summary>Po změně je potřeba restart PC (nebo odhlášení) pro plný efekt.</summary>
    public bool RequiresRestart { get; init; } = true;

    /// <summary>Nejnižší číslo buildu Windows, na kterém optimalizace dává smysl (22000 = Windows 11).</summary>
    public int MinBuild { get; init; }

    /// <summary>Upozornění zobrazené u optimalizace (vedlejší účinky, požadavky na hardware).</summary>
    public string Note { get; init; } = "";

    /// <summary>Pravidlo podpory: vrací důvod, proč optimalizace na tomto PC není podporovaná, nebo null.</summary>
    public Func<SystemProfile, string?>? Requires { get; init; }

    /// <summary>Doporučení podle hardwaru. Když není zadáno, platí hodnota Recommended.</summary>
    public Func<SystemProfile, bool>? RecommendWhen { get; init; }

    /// <summary>Méně prověřená nebo riskantnější optimalizace.</summary>
    public bool Experimental { get; init; }

    /// <summary>Nově přidaná optimalizace (filtr Nové).</summary>
    public bool IsNew { get; init; }
}

public sealed record ToggleResult(bool Success, string Message, bool RestartNeeded);

public sealed class TweakService
{
    private readonly object _gate = new();
    private readonly BackupStore _store = BackupStore.Load();

    public static int WindowsBuild => Environment.OSVersion.Version.Build;

    /// <summary>Důvod, proč optimalizace na tomto počítači není podporovaná (null = podporovaná).</summary>
    public string? UnsupportedReason(Tweak tweak, SystemProfile profile)
    {
        if (tweak.MinBuild > 0 && profile.Build < tweak.MinBuild)
        {
            return tweak.MinBuild <= 22000
                ? "Vyžaduje Windows 11."
                : "Vyžaduje novější Windows 11 (build " + tweak.MinBuild + ").";
        }

        try { return tweak.Requires?.Invoke(profile); }
        catch (Exception ex)
        {
            Log.Write("Pravidlo podpory selhalo (" + tweak.Id + "): " + ex.Message);
            return null;
        }
    }

    public bool IsRecommended(Tweak tweak, SystemProfile profile)
    {
        if (UnsupportedReason(tweak, profile) != null) return false;
        try { return tweak.RecommendWhen?.Invoke(profile) ?? tweak.Recommended; }
        catch { return tweak.Recommended; }
    }

    public TweakState Scan(Tweak tweak)
    {
        var on = 0;
        var off = 0;
        foreach (var op in tweak.Ops)
        {
            OpState state;
            try { state = op.Detect(); }
            catch (Exception ex)
            {
                Log.Write("Zjištění stavu selhalo (" + tweak.Id + " / " + op.Key + "): " + ex.Message);
                state = OpState.Off;
            }

            if (state == OpState.On) on++;
            else if (state == OpState.Off) off++;
        }

        if (on == 0 && off == 0) return TweakState.NotApplicable;
        if (off == 0) return TweakState.On;
        if (on == 0) return TweakState.Off;
        return TweakState.Partial;
    }

    public Dictionary<string, TweakState> ScanAll(IReadOnlyList<Tweak> tweaks, IProgress<(int, int)>? progress)
    {
        var result = new Dictionary<string, TweakState>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < tweaks.Count; i++)
        {
            result[tweaks[i].Id] = Scan(tweaks[i]);
            progress?.Report((i + 1, tweaks.Count));
        }
        return result;
    }

    public ToggleResult Apply(Tweak tweak) => Execute(tweak, true);

    public ToggleResult Revert(Tweak tweak) => Execute(tweak, false);

    private ToggleResult Execute(Tweak tweak, bool enable)
    {
        // Změny se provádějí vždy jedna po druhé, aby se nepřepisovala záloha.
        lock (_gate)
        {
            var backup = _store.Get(tweak.Id);
            var errors = new List<string>();

            foreach (var op in tweak.Ops)
            {
                try
                {
                    if (enable) op.Apply(backup);
                    else op.Revert(backup);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    Log.Write((enable ? "ZAPNOUT" : "VRÁTIT") + " " + tweak.Id + " / " + op.Key + " selhalo: " + ex.Message);
                }
            }

            if (!enable && errors.Count == 0)
                _store.Remove(tweak.Id);

            try { _store.Save(); }
            catch (Exception ex) { errors.Add("Zálohu se nepodařilo uložit: " + ex.Message); }

            var action = enable ? "zapnuto" : "vráceno do původního stavu";
            Log.Write(tweak.Id + " " + action + (errors.Count == 0 ? "" : " (s chybami: " + errors.Count + ")"));

            if (errors.Count == 0)
            {
                var text = enable ? "Zapnuto." : "Vráceno do původního stavu.";
                if (tweak.RequiresRestart) text += " Pro plný efekt restartuj PC.";
                return new ToggleResult(true, text, tweak.RequiresRestart);
            }

            var sb = new StringBuilder("Dokončeno s chybami: ");
            sb.Append(string.Join(" | ", errors));
            return new ToggleResult(false, sb.ToString(), false);
        }
    }
}
