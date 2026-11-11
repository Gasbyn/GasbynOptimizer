using System.Globalization;
using System.Text;

namespace GasbynOptimizer.Core;

/// <summary>
/// Chytré hledání optimalizací s bodováním relevance: nevadí diakritika, koncovky, velikost písmen ani překlepy,
/// hledá se v názvu, popisu, poznámce, kategorii i stavu, funguje psaní jen začátku slova a rozumí příbuzným slovům
/// a anglickým názvům (například „lokace“, „poloha“ i „location“ najdou vypnutí polohy).
/// </summary>
public static class SearchIndex
{
    // Skupiny příbuzných slov (bez diakritiky). Dotaz na kterékoli slovo najde i ostatní ve skupině.
    private static readonly string[][] Groups =
    {
        new[] { "poloha", "polohu", "polohy", "polohove", "lokace", "lokaci", "lokalizace", "location", "locations", "gps", "geolokace", "geolocation", "misto", "mista" },
        new[] { "mapa", "mapy", "mapu", "map", "maps", "offline", "navigace" },
        new[] { "tisk", "tisknout", "tiskarna", "tiskarny", "tiskaren", "print", "printer", "printing", "spooler" },
        new[] { "dotyk", "dotykove", "dotykova", "touch", "touchscreen", "obrazovka" },
        new[] { "pero", "tuzka", "tuzky", "rukopis", "kresleni", "kreslit", "pen", "ink", "stylus", "handwriting", "drawing" },
        new[] { "virtualizace", "virtualni", "virtual", "virtualization", "hyper", "hypervisor", "vm", "vbs", "hvci", "sandbox", "wsl" },
        new[] { "kamera", "fotak", "fotoaparat", "camera", "webcam" },
        new[] { "hodiny", "budik", "alarm", "clock", "casovac" },
        new[] { "copilot", "ai", "umela", "inteligence", "asistent", "cortana" },
        new[] { "bezdratove", "bezdratovy", "wireless", "wifi", "bluetooth", "wlan", "rádio", "radio" },
        new[] { "grafika", "grafiku", "grafiky", "grafickou", "gpu", "graphics", "nvidia", "amd", "radeon", "geforce", "karta" },
        new[] { "procesor", "procesoru", "cpu", "intel", "ryzen" },
        new[] { "zvuk", "zvuky", "audio", "sound", "hluk" },
        new[] { "disk", "ssd", "hdd", "storage", "ulozisté", "uloziste" },
        new[] { "prohlizec", "prohlizece", "browser", "chrome", "brave", "edge", "firefox" },
        new[] { "odinstalovat", "odinstalace", "uninstall", "remove", "delete", "smazat", "odstranit", "debloat" },
        new[] { "vypnout", "vypnuti", "vypnete", "disable", "disabled", "off", "zakazat", "zakaz", "zastavit", "stop" },
        new[] { "zapnout", "zapnuti", "enable", "enabled", "povolit" },
        new[] { "bezpecnost", "bezpecne", "security", "zabezpeceni", "defender", "firewall", "antivirus", "ochrana" },
        new[] { "pamet", "pameti", "memory", "ram" },
        new[] { "rychlost", "rychlejsi", "zrychlit", "zrychleni", "speed", "fast", "boost", "vykon", "vykonu", "performance" },
        new[] { "reklama", "reklamy", "reklam", "ads", "advertising", "sledovani", "tracking" },
        new[] { "soubory", "souboru", "docasne", "temp", "cache", "kos", "recycle" },
        new[] { "hlaseni", "reporting", "report", "logovani", "logging", "log", "chyby", "chyb", "errors", "telemetrie", "telemetri", "telemetry", "diagnostika", "diagnostiku", "diagnostics" },
        new[] { "vzdalene", "vzdalena", "vzdalenou", "remote", "rdp", "plocha", "plochu", "desktop" },
        new[] { "xbox", "gamepad", "ovladac", "ovladace", "controller", "joystick", "herni" },
        new[] { "telefon", "telefony", "phone", "iphone", "apple", "android", "mobil", "mobilni", "mobile" },
        new[] { "klavesnice", "keyboard", "klavesy" },
        new[] { "mys", "mouse", "akcelerace", "acceleration" },
        new[] { "sit", "site", "siti", "internet", "network", "ethernet", "tcp", "dns", "pripojeni", "connection", "ping", "latence", "latency" },
        new[] { "aktualizace", "aktualizaci", "update", "updates", "upgrade", "windows update" },
        new[] { "hrani", "hry", "hra", "game", "games", "gaming", "fps" },
        new[] { "soukromi", "privacy", "osobni", "data" },
        new[] { "napajeni", "power", "energie", "uspora", "baterie", "battery", "saving", "spanek", "sleep" },
        new[] { "vzhled", "vzhledu", "appearance", "theme", "motiv", "animace", "animation", "efekty", "effects", "pisma", "font", "fonts" },
        new[] { "sluzby", "sluzba", "service", "services", "proces", "procesy" },
        new[] { "pokrocile", "pokrocily", "advanced", "experimentalni", "experimental" },
        new[] { "usb", "zarizeni", "device", "devices" },
        new[] { "hardware", "komponenty", "deska", "motherboard" },
        new[] { "notebook", "laptop", "prenosny" },
        new[] { "cisteni", "cistit", "uklid", "clean", "cleanup", "cleaner" },
        new[] { "spotify", "discord", "steam", "epic", "obs", "launcher", "spusteni", "autostart", "startup" },
        new[] { "sdileni", "sdilet", "share", "sharing" },
        new[] { "onedrive", "cloud", "synchronizace", "sync", "oblak" },
        new[] { "oznameni", "notifikace", "notification", "notifications", "upozorneni" },
        new[] { "mitigace", "mitigations", "spectre", "meltdown", "sehop" }
    };

    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return sb.ToString();
    }

    public static string[] Tokens(string? text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Slova hledaného textu s váhou (název má větší váhu než popis).</summary>
    public sealed class Doc
    {
        public Dictionary<string, double> Words { get; } = new();
        public string Name { get; init; } = "";

        public void Add(string? text, double weight)
        {
            foreach (var word in Tokens(text))
                if (!Words.TryGetValue(word, out var known) || known < weight) Words[word] = weight;
        }
    }

    // Společný začátek dvou slov (český ohyb: tiskarna/tiskarny/tiskaren, poloha/polohu/polohy).
    private static int CommonPrefix(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < n && a[i] == b[i]) i++;
        return i;
    }

    private static bool SameStem(string a, string b)
    {
        var min = Math.Min(a.Length, b.Length);
        if (min < 4) return false;
        return CommonPrefix(a, b) >= Math.Max(4, min - 2) && Math.Abs(a.Length - b.Length) <= 3;
    }

    private static int Distance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return max + 1;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            var best = cur[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var v = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                cur[j] = v;
                if (v < best) best = v;
            }
            if (best > max) return max + 1;
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    private static bool Transposition(string a, string b)
    {
        if (a.Length != b.Length || a.Length < 4) return false;
        var diff = new List<int>();
        for (var i = 0; i < a.Length && diff.Count <= 2; i++)
            if (a[i] != b[i]) diff.Add(i);
        return diff.Count == 2 && diff[1] == diff[0] + 1 && a[diff[0]] == b[diff[1]] && a[diff[1]] == b[diff[0]];
    }

    /// <summary>Jak dobře se hledané slovo shoduje se slovem v textu (0 = vůbec).</summary>
    private static double MatchWord(string token, string word)
    {
        if (word == token) return 1.0;
        if (token.Length >= 2 && word.StartsWith(token, StringComparison.Ordinal)) return 0.88;
        if (token.Length >= 3 && word.Contains(token, StringComparison.Ordinal)) return 0.55;
        if (SameStem(token, word)) return 0.72;
        if (token.Length >= 4 && Transposition(token, word)) return 0.6;
        if (token.Length >= 5)
        {
            var max = token.Length >= 9 ? 2 : 1;
            var d = Distance(token, word, max);
            if (d <= max) return 0.5 - d * 0.1;
            // Překlep v rozepsaném začátku slova (například „lokacw“ -> „lokace“).
            if (word.Length > token.Length)
            {
                var head = word[..token.Length];
                if (Distance(token, head, 1) <= 1) return 0.4;
            }
        }
        return 0;
    }

    // Příbuzná slova hledaného výrazu (jen ze skupin, ve kterých se vyskytuje).
    private static List<string> Synonyms(string token)
    {
        var result = new HashSet<string>();
        foreach (var group in NormalizedGroups)
            if (group.Any(w => w == token || (token.Length >= 3 && w.StartsWith(token, StringComparison.Ordinal)) || SameStem(w, token)))
                foreach (var w in group) if (w != token) result.Add(w);
        return result.ToList();
    }

    private static readonly string[][] NormalizedGroups =
        Groups.Select(g => g.Select(w => Normalize(w).Trim()).Where(w => w.Length > 1 && !w.Contains(' ')).ToArray()).ToArray();

    private static readonly Dictionary<string, List<string>> SynonymCache = new();

    private static List<string> SynonymsOf(string token)
    {
        lock (SynonymCache)
        {
            if (!SynonymCache.TryGetValue(token, out var list))
            {
                if (SynonymCache.Count > 400) SynonymCache.Clear();
                SynonymCache[token] = list = Synonyms(token);
            }
            return list;
        }
    }

    /// <summary>Hledaný výraz rozdělený na slova a jejich příbuzná slova; připravit jednou pro všechny optimalizace.</summary>
    public sealed class Query
    {
        public string Text { get; }
        public string[] Tokens { get; }
        public List<string>[] Related { get; }

        public Query(string? text)
        {
            Text = Normalize(text).Trim();
            Tokens = SearchIndex.Tokens(text);
            Related = Tokens.Select(SynonymsOf).ToArray();
        }

        public bool IsEmpty => Tokens.Length == 0;
    }

    /// <summary>Bodové hodnocení optimalizace pro hledaný výraz. 0 = nevyhovuje (musí se najít všechna zadaná slova).</summary>
    public static double Score(Doc doc, Query query)
    {
        if (query.IsEmpty) return 1;

        double total = 0;
        for (var i = 0; i < query.Tokens.Length; i++)
        {
            var token = query.Tokens[i];
            double best = 0;

            foreach (var (word, weight) in doc.Words)
            {
                var direct = MatchWord(token, word);
                if (direct > 0) best = Math.Max(best, direct * weight);

                // Příbuzná slova (synonyma, anglické názvy) mají o něco menší váhu.
                if (best < weight * 0.6)
                    foreach (var related in query.Related[i])
                    {
                        var m = MatchWord(related, word);
                        if (m >= 0.72) { best = Math.Max(best, m * weight * 0.6); break; }
                    }
            }

            if (best <= 0) return 0;
            total += best;
        }

        var name = Normalize(doc.Name);
        if (query.Text.Length > 0 && name.Contains(query.Text, StringComparison.Ordinal)) total += 12;
        if (name.StartsWith(query.Text, StringComparison.Ordinal)) total += 8;
        return total;
    }

    /// <summary>Návrh slova, když nic nevyhovuje (nejpodobnější slovo z názvů optimalizací).</summary>
    public static string? Suggest(IEnumerable<Doc> docs, string text)
    {
        var tokens = Tokens(text);
        if (tokens.Length == 0) return null;
        var last = tokens[^1];
        string? best = null;
        double bestScore = 0;
        foreach (var doc in docs)
            foreach (var (word, weight) in doc.Words)
            {
                if (weight < 8 || word.Length < 4) continue;
                var m = MatchWord(last, word);
                if (m > bestScore && m < 1.0) { bestScore = m; best = word; }
            }
        return bestScore >= 0.4 ? best : null;
    }
}
