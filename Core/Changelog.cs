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
        new("2.0.0.219", new DateTime(2026, 11, 26, 22, 55, 0), new[]
        {
            Add("Průzkumník: skrýt tlačítko Domů", "V levém panelu Průzkumníka zmizí položka Domů (Home) a Průzkumník se otevře na Tento počítač. Jen Windows 11."),
        }),
        new("2.0.0.218", new DateTime(2026, 11, 26, 22, 20, 0), new[]
        {
            Add("Síť: vypnout MLD/IGMP a ICMP přesměrování", "Nová experimentální optimalizace v Síti: vypne multicastové hlášení (IGMP/MLD) a přijímání ICMP přesměrování. Pokud kvůli ní přestane fungovat FiveM, jde kdykoli vypnout."),
        }),
        new("2.0.0.217", new DateTime(2026, 11, 26, 22, 30, 0), new[]
        {
            Add("Aktualizace aplikací jen ručně", "Nové optimalizace v kategorii Windows Update vypnou aktualizace na pozadí pro Chrome (Google Update), Brave, Firefox, Opera, Adobe a Dropbox. Zakážou i verzované služby (např. GoogleUpdaterService159.0.8140.0) a aplikace je po samokontrole sama znovu vypne, když se po aktualizaci objeví nová. Edge má stejné vypnutí doplněné o úlohy."),
            Add("Vysoká priorita pro známé aplikace", "Jedno tlačítko nastaví vysokou prioritu OBS Studiu, Streamlabs, Fortnite, Robloxu, Minecraftu, CS2 a Valorantu. Seznam je v popisku."),
            Add("Windows Update jen ručně", "Windows se přestane aktualizovat automaticky, aktualizace spustíš ručně."),
        }),
        new("2.0.0.214", new DateTime(2026, 11, 22, 16, 10, 0), new[]
        {
            Add("Automatické aktualizace za běhu", "Když je aplikace zapnutá, každou minutu se zkontroluje nová verze. Drobná verze (změní se jen poslední číslo) se nainstaluje sama na pozadí. U větší verze s novými funkcemi se ukáže okno Teď / Později, aplikace se přesune do popředí a problikává v liště. Při Později se aktualizuje sama při příštím spuštění."),
            Add("Aktualizátor ovladačů", "V Údržbě, Aktualizace, je přepínač Aplikace / Drivery. Drivery vyhledá a nainstaluje přes Windows Update, takže nabídne jen ovladače, které pro tvůj počítač zveřejnil výrobce. Před instalací se vytvoří bod obnovení. Když Windows Update v systému chybí, kategorie Drivery se nezobrazí."),
            Add("Wi-Fi optimalizace", "Vypnutí úsporného režimu Wi-Fi adaptéru (doporučeno), nepřipojování k doporučeným otevřeným sítím (doporučeno), vypnutí Wi-Fi při připojeném kabelu a vypnutí mobilního hotspotu a Wi-Fi Direct. Na počítači bez Wi-Fi jsou nepodporované."),
            Add("Kamera: zakázat aplikacím přístup", "Aplikace nedostanou přístup ke kameře, kamera zůstane v systému. Lehčí varianta pro ty, kdo kameru nepoužívají."),
            Add("Kamera: vypnout podporu kamer", "Úplné vypnutí podpory kamer ve Windows. Experimentální, kamera přestane fungovat ve všech programech (Discord, Zoom, OBS)."),
            Chg("Méně kategorií", "Kategorie Pokročilé, Debloat, Služby, Vzhled, Disk, Wi-Fi a Hraní a výkon nahradilo devět přehlednějších: Soukromí, Výkon a hry, Disk a úložiště, Síť a Wi-Fi, Windows Update, Vzhled a pohodlí, Systém a služby, Předinstalované aplikace a Zabezpečení a funkce Windows."),
            Chg("Spouštěné aplikace v Údržbě", "Spouštěné aplikace už nejsou v levém menu, najdeš je v Údržbě.")
        }),
        new("2.0.0.202", new DateTime(2026, 11, 18, 14, 40, 0), new[]
        {
            Chg("Aktualizované podmínky použití", "Podmínky jsou doplněné o odinstalaci aplikací, mazání starých instalačních souborů, kontrolu aktualizací přes winget a export nastavení. Všichni je musí potvrdit znovu."),
            Chg("Spolehlivější zobrazení podmínek", "Souhlas se pamatuje v nastavení i v registru. Po nové instalaci, odinstalaci nebo smazání nastavení se podmínky ukážou znovu.")
        }),
        new("2.0.0.201", new DateTime(2026, 11, 17, 11, 30, 0), new[]
        {
            Add("Disk: optimalizace podle typu disku", "Nová kategorie Disk. Má úpravy pro HDD, SSD a NVMe. Doporučené jsou jen tam, kde to dává smysl. Optimalizace pro jiný typ disku, než máš (například HDD na počítači s NVMe), jsou dostupné jen v pokročilém režimu."),
            Chg("Vypnutí Prefetch a Superfetch jen pro SSD", "Na pevném disku by systém zpomalil, proto je teď nepodporované a jde použít jen v pokročilém režimu."),
            Chg("Filtr „Zobrazit nepodporované“", "Nepodporované optimalizace jsou ve výchozím stavu skryté. Zapneš je přepínačem nad seznamem. Použít jdou jen s pokročilým režimem v Nastavení."),
            Chg("Úklid zbytků aplikací maže i staré instalátory", "Smaže instalační soubory (setup, installer) ve Stažených souborech starší než 4 dny. Instalátor Gasbyn Optimizeru se smaže hned."),
            Chg("Odinstalace ukazuje víc aplikací", "Přibyly aplikace z Microsoft Store (třeba Hodiny), instalace jen pro tvůj účet a programy, které mají odinstalaci jen přes Windows Installer. Spouštěné aplikace ukazují i naplánované úlohy při přihlášení a 32bitové záznamy."),
            Chg("Loga aplikací", "V Odinstalaci aplikací a v Aktualizaci aplikací se u každé aplikace ukáže její logo."),
            Chg("Oprava odinstalace Microsoft Edge", "Odinstalace mimo EU končila chybou 532. Teď povolí odinstalaci oficiálním přepínačem a po dobu odinstalace vytvoří atrapu, kterou instalátor Edge hledá. Chybová hláška už neukazuje celý skript.")
        }),
        new("2.0.0.155", new DateTime(2026, 11, 11, 10, 30, 0), new[]
        {
            Chg("Chytrý detektor je přímo v optimalizacích", "Zvláštní stránka zmizela. Optimalizace, které se hodí právě na tvůj počítač, teď mají štítek Doporučeno stejně jako ty ručně doporučené, a v Podrobněji je napsáno proč. Zapnout doporučené zapne i je."),
            Add("Doporučení odinstalovat nepoužívané programy", "V Údržbě, Odinstalace aplikací, mají programy, které jsi 31 dní a déle nespustil, štítek Doporučeno a jsou nahoře. Údaj je ze záznamu Windows o spuštěných programech, hry spuštěné přes launcher se nezapočítávají."),
            Chg("Debloat ukazuje jen odstranitelné aplikace", "Aplikace, které na počítači nejsou nebo nejdou odinstalovat, se ukážou jen v pokročilém režimu."),
            Chg("Oprava odinstalace „error 0“", "Další odinstalace se při běžící odinstalaci ignoruje. Výsledek se určuje podle toho, jestli aplikace opravdu zmizela ze seznamu, a hláška říká, co se stalo, místo holého kódu.")
        }),
        new("2.0.0.146", new DateTime(2026, 11, 9, 10, 10, 0), new[]
        {
            Chg("Výchozí jazyk je čeština", "Aplikace i instalátor se vždy spustí česky, bez ohledu na jazyk Windows. Angličtinu si vybereš v Nastavení nebo v průvodci instalací.")
        }),
        new("2.0.0.145", new DateTime(2026, 11, 9, 9, 55, 0), new[]
        {
            Add("Samokontrola při každém spuštění", "Aplikace po startu zkontroluje, jestli Windows nebo jiný program nevrátil optimalizace, které jsi zapnul(a). Když ano, nabídne jejich opětovné zapnutí. Jde vypnout nebo spustit ručně v Nastavení."),
            Add("Chytrý detektor", "Nová stránka, která podle tvého hardwaru (notebook nebo PC, SSD nebo HDD, paměť, grafika) a nainstalovaných programů doporučí optimalizace i s vysvětlením proč. Všechno se vyhodnocuje lokálně, nic se neodesílá."),
            Add("Správce spouštěných aplikací", "Nová stránka se seznamem programů, které se spouští s Windows. Jde je vypnout a zapnout stejným způsobem jako ve Správci úloh, nic se nemaže."),
            Add("Export a import nastavení", "V Nastavení si vybereš, kam se zapnuté optimalizace uloží do souboru, a odkud se zase načtou."),
            Add("Jazyk a vzhled", "Výchozí je čeština a tmavý vzhled. V Nastavení jde přepnout na angličtinu a světlý vzhled, jazyk jde vybrat i při instalaci. Texty u jednotlivých optimalizací zatím zůstávají česky."),
            Add("Odkazy na Discord", "V Nastavení jsou odkazy na hlášení problémů, hodnocení a podporu změn."),
            Chg("Méně falešných poplachů antiviru", "K vydání se přikládá soubor SHA256SUMS.txt s kontrolním součtem instalátoru. Postup, jak se podpisem kódu zbavit varování úplně, je v dokumentaci."),
            Chg("Označení NOVÉ", "U sloučených optimalizací z minulé verze štítek NOVÉ zmizel. V této verzi se žádná optimalizace nepřidává ani neupravuje.")
        }),
        new("2.0.0.112", new DateTime(2026, 11, 8, 22, 0, 0), new[]
        {
            Chg("Sjednocené duplicitní optimalizace", "Přepínače, které dělaly totéž, jsou teď jeden. Telemetrie (základní), reklamy a doporučení, webové hledání a Cortana, synchronizace mezi zařízeními, SysMain a Prefetch, úspora energie síťových karet, efekty a animace, vzdálená připojení, hloubková telemetrie a Edge. Všechny kroky zůstaly, jen jsou pohromadě."),
            Add("Podrobný popisek u každé optimalizace", "Pod každou optimalizací je odkaz „Podrobněji“: co přepínač dělá, proč ho chceš a na co si dát pozor, napsáno lidskou řečí."),
            Chg("Barevné štítky", "Doporučeno je zelené, Nové modré, Experimentální fialové a Nepodporované červené, ať se dají rozlišit na první pohled."),
            Chg("FPS tester je Performance Tester", "Přejmenováno, protože v testeru časem přibude i ping a další měření.")
        }),
        new("2.0.0.89", new DateTime(2026, 11, 8, 21, 10, 0), new[]
        {
            Chg("Méně falešných poplachů antiviru", "Aplikace už nespouští PowerShell s kódovaným příkazem a parametrem „-ExecutionPolicy Bypass“, podle kterého antiviry (např. AVG, hláška „IDP.HELU.PSE96 – Command line detection“) poznávají škodlivé skripty. Skripty se předávají čitelně přes standardní vstup a příkazový řádek je čistý."),
            Add("Nápověda k antiviru v Nastavení", "Nová karta s postupem, co dělat, když antivirus aplikaci zablokuje (výjimka, nahlášení falešného poplachu, Discord).")
        }),
        new("2.0.0.88", new DateTime(2026, 11, 8, 20, 50, 0), new[]
        {
            Chg("Vypnuto vrací výchozí nastavení Windows", "Když optimalizaci vypneš, nastaví se výchozí hodnota podle Windows (registr, typ spouštění služeb, plán napájení Vyvážený), ne jen uložená záloha. Výjimkou jsou služby a nastavení ovladačů výrobců (NVIDIA, ASUS, MSI, AMD, síťové karty), kde Windows žádný výchozí stav neurčují a vrací se původní hodnota."),
            Chg("Opravené výchozí hodnoty", "Plánování GPU s hardwarovým urychlením se při vypnutí vrací na výchozí volbu Windows, Vzdálená plocha a Vzdálená pomoc na výchozí hodnoty a služby Mapy, Windows Search, Update Orchestrator, Centrum zabezpečení, Edge Update a Hyper-V (vmms) na správný typ spouštění.")
        }),
        new("2.0.0.86", new DateTime(2026, 11, 4, 9, 0, 0), new[]
        {
            Add("Rozšířené čištění cache, TEMP a logů aplikací", "Stávající tlačítko v Údržbě čistí starší cache, dočasné soubory a diagnostické logy dalších podporovaných aplikací včetně CapCut, Streamer.bot, Macro Deck, Streamlabs Desktop, Process Lasso a VS Code. Cache se čistí až po 7 dnech, TEMP po 3 dnech, logy po 14 dnech a crash reporty po 30 dnech. Běžící aplikace se přeskočí; nastavení, účty, cookies, projekty, světy, hry, stažené soubory a zdrojová média zůstávají."),
            Chg("Metadata vydavatele v EXE a instalátoru", "Windows nyní u souboru zobrazuje výrobce Gasbyn, produkt Gasbyn Optimizer, popis Windows Optimization Tool, copyright a verzi sestavení. Metadata jsou ověřena během CI buildu. Nejde o kryptografický podpis a samo o sobě to neodstraní SmartScreen ani antivirová varování.")
        }),
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
