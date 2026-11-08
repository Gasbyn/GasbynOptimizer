namespace GasbynOptimizer.Core;

/// <summary>
/// Sjednocení duplicitních optimalizací: několik dřívějších přepínačů, které dělají totéž nebo se překrývají,
/// je teď jeden přepínač se všemi jejich kroky (každý krok se provede jen jednou).
/// </summary>
internal static class CatalogMerge
{
    private sealed record Group(string NewId, string[] Members, string Name, string Description, string Note,
        string? Category = null, bool Recommended = false, bool Experimental = false);

    private static readonly Group[] Groups =
    {
        new("telemetry-all",
            new[] { "telemetry", "telemetry-services", "telemetry-tasks", "error-reporting", "input-personalization" },
            "Vypnout telemetrii a odesílání dat Microsoftu",
            "Jedním přepínačem vypne všechno základní sledování: diagnostická data, služby a naplánované úlohy telemetrie, hlášení chyb a sběr psaní a rukopisu.",
            "Hlubší vypnutí diagnostiky a logování je v Pokročilých.", Recommended: true),

        new("ads-all",
            new[] { "suggestions", "consumer-features", "explorer-ads", "start-recommendations", "spotlight", "tailored-experiences", "advertising-id" },
            "Vypnout reklamy, návrhy a doporučení ve Windows",
            "Vypne reklamní ID, reklamy a návrhy v nabídce Start, Průzkumníku a Nastavení, Windows Spotlight, přizpůsobená doporučení a automatickou instalaci doporučených aplikací.",
            "", Recommended: true),

        new("web-search-all",
            new[] { "bing-search", "cortana-search" },
            "Vypnout webové hledání a Cortanu",
            "Hledání ve Windows prohledává jen tvůj počítač. Vypne výsledky z Bingu v nabídce Start, cloudové hledání a Cortanu.",
            "", Recommended: true),

        new("sync-all",
            new[] { "clipboard-cloud", "settings-sync-off", "cdp-off", "activity-history" },
            "Vypnout synchronizaci a sdílení mezi zařízeními",
            "Windows nebude odesílat do cloudu historii aktivit, schránku ani nastavení a nebude sdílet věci mezi tvými zařízeními.",
            "Přestanou fungovat funkce jako Časová osa, sdílená schránka (Win+V v cloudu) nebo přenos nastavení na jiný počítač."),

        new("sysmain-all",
            new[] { "sysmain", "prefetch-off" },
            "Vypnout SysMain (Superfetch) a Prefetch",
            "Windows přestane na pozadí předem načítat aplikace do paměti a ukládat si, co spouštíš. Šetří disk a procesor.",
            "Na starém počítači s klasickým diskem (HDD) mohou aplikace startovat pomaleji. Na SSD to nevadí."),

        new("net-power-all",
            new[] { "net-adapter-power", "net-eee" },
            "Vypnout úsporu energie síťových karet (včetně EEE)",
            "Síťová karta (Wi-Fi i kabel) se nebude kvůli šetření energie vypínat ani zpomalovat. Odstraní výpadky a zpoždění po chvíli nečinnosti.",
            "Na notebooku na baterii může mírně zvýšit spotřebu. Nastavení existuje jen u karet, které ho podporují, ostatní se přeskočí."),

        new("effects-all",
            new[] { "disable-effects", "listview-effects", "aero-peek", "first-logon-animation" },
            "Vypnout průhlednost, animace a efekty",
            "Vypne průhlednost a animace oken, stíny a průhledný výběr v seznamech, náhled plochy (Aero Peek) a úvodní animaci po přihlášení. Systém působí svižněji.",
            "Windows vypadají jednodušeji, bez plynulých přechodů."),

        new("adv-remote-all",
            new[] { "adv-remote-all", "adv-remote-desktop", "remote-assistance" },
            "Vypnout všechna vzdálená připojení",
            "Zakáže vzdálenou plochu (RDP), vzdálenou pomoc, vzdálenou správu (WinRM), směrování a vzdálený přístup a objevování zařízení v síti (SSDP, UPnP).",
            "Nepůjde se k PC připojit odjinud. Router může přestat automaticky otevírat porty pro hry.",
            Category: CatalogAdvanced.CatAdvanced, Experimental: true),

        new("adv-telemetry-deep",
            new[] { "adv-diagnostics", "adv-reporting-off" },
            "Telemetrie: hloubkové vypnutí diagnostiky a logování",
            "Zakáže diagnostické služby a poradce při potížích, kolektor diagnostiky, program zlepšování, kompatibilitu aplikací a diagnostické logování. Doplňuje základní „Vypnout telemetrii“.",
            "Nedoporučeno: automatičtí poradci při potížích nebudou fungovat a Windows nebude hlásit kompatibilitu starších aplikací. Vyžaduje restart.",
            Category: CatalogAdvanced.CatAdvanced, Experimental: true),

        new("debloat-edge",
            new[] { "debloat-edge", "edge-background" },
            "Edge: odstranit balast a hlášení",
            "Vypne v Edge běh na pozadí, rychlý start, nákupního asistenta, boční lištu, Rewards, hlášení a doporučení.",
            "", Category: CatalogDebloat.CatDebloat),
    };

    public static IReadOnlyList<Tweak> Apply(IReadOnlyList<Tweak> source)
    {
        var list = source.ToList();
        foreach (var g in Groups)
        {
            var members = g.Members.Select(id => list.FirstOrDefault(t => t.Id == id)).Where(t => t != null).Select(t => t!).ToList();
            if (members.Count == 0) continue;

            var ops = new List<TweakOp>();
            foreach (var op in members.SelectMany(m => m.Ops))
                if (ops.All(o => o.Key != op.Key)) ops.Add(op);

            var first = members[0];
            var merged = new Tweak
            {
                Id = g.NewId,
                Name = g.Name,
                Description = g.Description,
                Note = g.Note,
                Category = g.Category ?? first.Category,
                Ops = ops,
                Recommended = g.Recommended,
                Experimental = g.Experimental,
                RequiresRestart = members.Any(m => m.RequiresRestart),
                MinBuild = members.Max(m => m.MinBuild),
                IsNew = true
            };

            var index = list.IndexOf(first);
            list.RemoveAll(t => members.Contains(t));
            list.Insert(Math.Min(index, list.Count), merged);
        }
        return list;
    }
}
