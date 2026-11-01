namespace GasbynOptimizer.Core;

public enum ChangeKind { Added, Changed, Removed }

/// <summary>Jedna změna: stručný titulek a podrobnější popis (zobrazí se v podrobném Update logu).</summary>
public sealed record ChangeItem(ChangeKind Kind, string Title, string Detail = "");

/// <summary>Vydání s datem a časem (místní čas ČR) a seznamem změn.</summary>
public sealed record ChangeEntry(string Version, DateTime Released, IReadOnlyList<ChangeItem> Items);

/// <summary>Seznam změn po verzích, nejnovější nahoře. Při každém vydání se sem přidá nový záznam.</summary>
public static class Changelog
{
    private static ChangeItem Add(string title, string detail = "") => new(ChangeKind.Added, title, detail);
    private static ChangeItem Chg(string title, string detail = "") => new(ChangeKind.Changed, title, detail);

    public static readonly IReadOnlyList<ChangeEntry> Entries = new List<ChangeEntry>
    {
        new("2.0.0.85", new DateTime(2026, 11, 1, 20, 40, 0), new[]
        {
            Chg("Spánek a hibernace jsou dvě samostatné optimalizace", "Spánek (Sleep) a hibernaci jde zapnout či vypnout každou zvlášť, třeba hibernaci vypnout a spánek nechat. Upraveny názvy a popisy, ať je jasné, že se neovlivňují.")
        }),
        new("2.0.0.84", new DateTime(2026, 11, 1, 17, 20, 0), new[]
        {
            Add("Update log po aktualizaci", "Při prvním spuštění po stažení a po každé aktualizaci se ukáže, co je nového: datum a čas vydání a co bylo přidáno, upraveno nebo odebráno. Ukazování jde v okně vypnout."),
            Add("Podrobný Update log v Nastavení", "Nová karta v Nastavení: přepínač podrobného výpisu (výchozí vypnuto) a tlačítko, které Update log otevře kdykoli."),
            Add("Vypnout režim spánku (Sleep)", "Nová optimalizace v kategorii Systém a napájení. Zakáže stavy spánku, časovač usnutí a skryje „Spánek“ v nabídce vypnutí. Hibernace zůstává samostatná optimalizace.")
        }),
        new("2.0.0.67", new DateTime(2026, 10, 28, 20, 19, 0), new[]
        {
            Add("Hloubkové čištění disku", "V Údržbě: úklid souborů Windows Update, starých balíčků ovladačů a nepoužívaných jazyků jako ve Vyčištění disku."),
            Add("Plynulé animace", "Karty optimalizací naběhnou při hledání, filtrování i přepnutí kategorie, přepínače se plynule posouvají a stránky se plynule objeví."),
            Chg("Mnohem chytřejší hledání", "Výsledky se řadí podle relevance, nevadí překlepy, diakritika ani koncovky, hledá se i v kategoriích, anglicky a podle příbuzných slov. Když nic nenajde, nabídne „Mysleli jste…?“."),
            Chg("Stavy Zapnuto / Vypnuto", "U tlačítkových optimalizací se místo „Aktivní plán“ píše Zapnuto a Vypnuto, u aplikací Odinstalováno a Nainstalováno."),
            Chg("Oprava načítací animace", "Točící se ikona a posuvný pruh už nejsou pozadu ani mimo střed."),
            Chg("Rozšířený úklid dočasných souborů", "Přibyly miniatury ikon a protokoly Windows Update.")
        }),
        new("2.0.0.27", new DateTime(2026, 10, 24, 13, 6, 0), new[]
        {
            Add("Částečné vypnutí funkcí tiskáren", "Vypne tisk přes web, stahování ovladačů a vzdálený přístup k tiskové frontě. Lokální tiskárna dál funguje."),
            Add("Vypnutí offline map", "Windows přestanou na pozadí stahovat a aktualizovat mapy."),
            Add("Vypnutí dotyku a pera", "Dotyková klávesnice, rukopis a Windows Ink (kreslení perem)."),
            Add("Odinstalace aplikací v Debloatu", "25 aplikací Windows s tlačítkem Odinstalovat; Copilot, Hodiny, Fotoaparát, Edge a další jsou označené jako doporučené."),
            Chg("Vypnutí polohy a polohových služeb", "Rozšířeno o poskytovatele polohy, skripty s polohou a přístup aplikací k poloze."),
            Chg("Úplné vypnutí tisku", "Přejmenováno a rozlišeno od částečného vypnutí funkcí tiskáren."),
            Chg("Virtualizace částečně a plně", "VBS (částečně) a hypervizor včetně všech služeb Hyper-V (plně)."),
            Chg("Chytřejší vyhledávání", "Hledání bez diakritiky a koncovek, anglické a příbuzné výrazy.")
        }),
        new("2.0.0.6", new DateTime(2026, 10, 21, 22, 41, 0), new[]
        {
            Add("Optimalizace grafiky", "11 nových optimalizací pro NVIDIA, AMD a obecné nastavení grafiky."),
            Chg("Celé číslo verze v aplikaci", "Aplikace ukazuje celou verzi, ne jen 2.0.")
        })
    };

    public static ChangeEntry? For(string version) => Entries.FirstOrDefault(e => e.Version == version);
}
