using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Písma a další úpravy vzhledu, které nejsou v ostatních katalozích.</summary>
internal static class CatalogFonts
{
    internal const string CatFonts = "Písma";

    public static IReadOnlyList<Tweak> Create()
    {
        const string explorerAdv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string explorerKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer";

        return new List<Tweak>
        {
            // ===== Písma =====
            new()
            {
                Id = "online-fonts", Category = CatFonts, Recommended = true,
                Name = "Nestahovat písma z internetu",
                Description = "Windows nebude na pozadí stahovat dodatečná písma od Microsoftu. Menší síťový provoz a méně pozadí.",
                Note = "Některé aplikace Office mohou chybějící písma nabídnout až po ruční instalaci.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableFontProviders", 0, null)
                }
            },
            new()
            {
                Id = "text-scale", Category = CatFonts,
                Name = "Zvětšit text v systému o 10 %",
                Description = "Text v systému a aplikacích bude o 10 % větší, bez změny velikosti ostatních prvků.",
                Note = "Projeví se po odhlášení nebo restartu. Vypnutí vrátí 100 %.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Accessibility", "TextScaleFactor", 110, 100)
                }
            },

            // ===== Vzhled a animace =====
            new()
            {
                Id = "acrylic-logon", Category = Catalog.CatVisual,
                Name = "Vypnout rozostření pozadí přihlašovací obrazovky",
                Description = "Přihlašovací obrazovka se zobrazí bez rozostřeného pozadí (rychlejší na slabších grafikách).",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "DisableAcrylicBackgroundOnLogon", 1, null)
                }
            },
            new()
            {
                Id = "thumbnails-off", Category = Catalog.CatVisual,
                Name = "Místo náhledů zobrazovat ikony",
                Description = "Průzkumník nebude vytvářet miniatury obrázků a videí. Rychlejší otevírání velkých složek.",
                Note = "V seznamech souborů uvidíš místo náhledů jen ikony.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "IconsOnly", 1, 0)
                }
            },
            new()
            {
                Id = "wallpaper-quality", Category = Catalog.CatVisual,
                Name = "Plná kvalita tapety",
                Description = "Windows přestane tapetu komprimovat a zobrazí ji v plné kvalitě JPEG.",
                Note = "Projeví se po novém nastavení tapety.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Control Panel\Desktop", "JPEGImportQuality", 100, null)
                }
            },

            // ===== Systém a pohodlí =====
            new()
            {
                Id = "autoplay-off", Category = Catalog.CatSystem, Recommended = true,
                Name = "Vypnout automatické přehrávání vyměnitelných disků",
                Description = "Po připojení USB disku nebo karty se nespustí nic samo. Bezpečnější a bez rušivých oken.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerKey + @"\AutoplayHandlers", "DisableAutoplay", 1, 0)
                }
            },
            new()
            {
                Id = "recent-files", Category = Catalog.CatUi,
                Name = "Nezobrazovat nedávné a časté soubory",
                Description = "Rychlý přístup v Průzkumníku nebude ukazovat nedávno otevřené soubory ani často používané složky.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerKey, "ShowRecent", 0, 1),
                    Catalog.Hkcu(explorerKey, "ShowFrequent", 0, 1)
                }
            }
        };
    }
}
