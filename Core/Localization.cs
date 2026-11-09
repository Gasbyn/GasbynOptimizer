using System.Windows;
using System.Windows.Controls;

namespace GasbynOptimizer.Core;

/// <summary>
/// Jednoduchá lokalizace: čeština je výchozí a anglické texty se hledají podle českého originálu.
/// Co ve slovníku není, zůstane česky (například popisy jednotlivých optimalizací).
/// </summary>
public static class L
{
    public static string Language { get; private set; } = "cs";
    public static bool English => Language == "en";

    private static string? InstallerChoice()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"Software\GasbynOptimizer", false);
            return key?.GetValue("Language") as string;
        }
        catch { return null; }
    }

    /// <summary>
    /// Vybere jazyk. Nová volba z instalátoru (registr) má přednost jednou, pak platí uložené nastavení.
    /// Bez volby se použije jazyk Windows.
    /// </summary>
    public static void Init(AppSettings settings)
    {
        var installer = InstallerChoice();
        if (!string.IsNullOrEmpty(installer) && installer != settings.InstallerLanguage)
        {
            settings.InstallerLanguage = installer;
            if (installer == "english") settings.Language = "en";
            else if (installer == "czech") settings.Language = "cs";
            settings.Save();
        }

        if (settings.Language is not ("cs" or "en"))
        {
            // Výchozí je vždy čeština, angličtinu si uživatel vybere sám.
            settings.Language = "cs";
            settings.Save();
        }

        Language = settings.Language;
    }

    public static void Add(IDictionary<string, string> more)
    {
        foreach (var (k, v) in more) En[k] = v;
    }

    public static string T(string cs) => English && En.TryGetValue(cs, out var en) ? en : cs;

    /// <summary>Přeloží texty v celém stromu prvků okna (jen to, co je ve slovníku).</summary>
    public static void Apply(DependencyObject root)
    {
        if (!English) return;
        Walk(root);
    }

    private static void Walk(DependencyObject node)
    {
        switch (node)
        {
            case TextBlock tb when tb.Inlines.Count <= 1 && !string.IsNullOrEmpty(tb.Text):
                tb.Text = T(tb.Text);
                break;
            case ContentControl { Content: string s } cc:
                cc.Content = T(s);
                break;
        }

        if (node is FrameworkElement { ToolTip: string tip } fe)
            fe.ToolTip = T(tip);

        foreach (var child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject d) Walk(d);
    }

    private static readonly Dictionary<string, string> En = new()
    {
        ["Všechny optimalizace"] = "All optimizations",
        ["Hraní a výkon"] = "Gaming & performance",
        ["Vzhled"] = "Appearance",
        ["Služby"] = "Services",
        ["Debloat"] = "Debloat",
        ["Pokročilé"] = "Advanced",
        ["Údržba"] = "Maintenance",
        ["Nastavení"] = "Settings",
        ["Performance Tester (brzy)"] = "Performance Tester (soon)",
        ["Chytrý detektor"] = "Smart detector",
        ["Spouštěné aplikace"] = "Startup apps",
        ["Zapnout doporučené"] = "Enable recommended",
        ["Vrátit vše"] = "Revert all",
        ["Restartovat nyní"] = "Restart now",
        ["Změny jsou provedeny. Pro jejich plný efekt je potřeba restartovat počítač."] = "Changes applied. Restart your PC for the full effect.",
        ["Pokročilý režim"] = "Advanced mode",
        ["Zapnout pokročilý režim"] = "Enable advanced mode",
        ["Bod obnovení systému"] = "System restore point",
        ["Automaticky vytvořit bod obnovení před první změnou"] = "Automatically create a restore point before the first change",
        ["Vytvořit bod obnovení teď"] = "Create restore point now",
        ["Tento počítač"] = "This computer",
        ["Antivirus a falešné poplachy"] = "Antivirus and false positives",
        ["Discord (hlášení problémů)"] = "Discord (report problems)",
        ["O aplikaci"] = "About",
        ["Otevřít datovou složku"] = "Open data folder",
        ["Update log"] = "Update log",
        ["Ukazovat Update log po aktualizaci"] = "Show the Update log after updating",
        ["Podrobný Update log (popisy změn a starší verze)"] = "Detailed Update log (change descriptions and older versions)",
        ["Zobrazit Update log"] = "Show Update log",
        ["Podmínky a podpora"] = "Terms and support",
        ["Podmínky použití"] = "Terms of use",
        ["Discord server"] = "Discord server",
        ["Hlášení problémů"] = "Report a problem",
        ["Hodnocení"] = "Ratings",
        ["Podpora změn"] = "Change support",
        ["Jazyk"] = "Language",
        ["Vzhled aplikace"] = "App theme",
        ["Tmavý"] = "Dark",
        ["Světlý"] = "Light",
        ["Změna se projeví po restartu aplikace."] = "The change takes effect after restarting the app.",
        ["Samokontrola"] = "Self-check",
        ["Zkontrolovat teď"] = "Check now",
        ["Export a import nastavení"] = "Export and import settings",
        ["Exportovat nastavení…"] = "Export settings…",
        ["Importovat nastavení…"] = "Import settings…",
        ["Údržba"] = "Maintenance",
        ["Úklid"] = "Cleanup",
        ["Aktualizace aplikací"] = "App updates",
        ["Odinstalace aplikací"] = "Uninstall apps",
        ["Prohlížeče"] = "Browsers",
        ["Zapnuto"] = "On",
        ["Vypnuto"] = "Off",
        ["Použít"] = "Apply",
        ["Zavřít"] = "Close",
        ["DOPORUČENO"] = "RECOMMENDED",
        ["NOVÉ"] = "NEW",
        ["EXPERIMENTÁLNÍ"] = "EXPERIMENTAL",
        ["NEPODPOROVANÉ"] = "UNSUPPORTED",
        ["Podrobněji ▾"] = "Details ▾",
        ["Skrýt podrobnosti ▴"] = "Hide details ▴"
    };
}
