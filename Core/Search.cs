using System.Globalization;
using System.Text;

namespace GasbynOptimizer.Core;

/// <summary>
/// Chytré hledání optimalizací: nevadí diakritika, koncovky ani velikost písmen, hledá se v názvu, popisu, poznámce a kategorii
/// a rozumí příbuzným slovům i anglickým názvům (například „lokace“, „poloha“ i „location“ najdou vypnutí polohy).
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

    private static string Stem(string word) => word.Length <= 4 ? word : word[..4];

    public static HashSet<string> Words(string? text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    // Stem -> všechna slova z příbuzných skupin.
    private static readonly Dictionary<string, HashSet<string>> Related = BuildRelated();

    private static Dictionary<string, HashSet<string>> BuildRelated()
    {
        var map = new Dictionary<string, HashSet<string>>();
        foreach (var group in Groups)
        {
            var words = group.Select(Normalize).Select(w => w.Trim()).Where(w => w.Length > 1).ToArray();
            foreach (var word in words)
            {
                var stem = Stem(word);
                if (!map.TryGetValue(stem, out var set)) map[stem] = set = new HashSet<string>();
                foreach (var other in words) set.Add(other);
            }
        }
        return map;
    }

    private static bool Contains(HashSet<string> indexWords, string candidate)
    {
        var stem = Stem(candidate);
        foreach (var word in indexWords)
            if (word.StartsWith(stem, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Všechna zadaná slova musí být v textu nalezena (přímo, bez koncovky, nebo přes příbuzné slovo).</summary>
    public static bool Matches(HashSet<string> indexWords, string query)
    {
        var tokens = Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (Contains(indexWords, token)) continue;

            var found = false;
            if (Related.TryGetValue(Stem(token), out var related))
                foreach (var candidate in related)
                    if (Contains(indexWords, candidate)) { found = true; break; }

            if (!found) return false;
        }
        return true;
    }
}
