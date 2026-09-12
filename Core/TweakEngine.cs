using GasbynOptimizer.Models;

namespace GasbynOptimizer.Core;

public sealed class TweakEngine
{
    private readonly Dictionary<string, Tweak> _tweaks = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<Tweak> Tweaks => _tweaks.Values;

    public void Register(Tweak tweak) => _tweaks[tweak.Id] = tweak;

    public Tweak? Get(string id) => _tweaks.TryGetValue(id, out var tweak) ? tweak : null;

    public TweakResult Scan(string id)
    {
        var tweak = Get(id);
        if (tweak is null)
            return TweakResult.Fail($"Tweak '{id}' nebyl nalezen.");

        try
        {
            var status = tweak.GetStatus(new TweakContext());
            return TweakResult.Ok($"{tweak.Name}: {status}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Nepodařilo se zjistit stav: {ex.Message}");
        }
    }
}
