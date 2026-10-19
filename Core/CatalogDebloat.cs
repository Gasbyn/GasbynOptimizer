using System.Text;

namespace GasbynOptimizer.Core;

/// <summary>Odstranění balastu v prohlížečích a herních aplikacích (a Microsoft Edge).</summary>
internal static class CatalogDebloat
{
    internal const string CatDebloat = "Debloat";

    private static string EdgeExe =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Edge", "Application", "msedge.exe");

    /// <summary>Edge je odinstalovaný, když neexistuje msedge.exe.</summary>
    internal static OpState EdgeUninstalledState() => File.Exists(EdgeExe) ? OpState.Off : OpState.On;

    private const string UninstallScript = @"
$ErrorActionPreference = 'Stop'
$base = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application'
$setup = Get-ChildItem (Join-Path $base '*\Installer\setup.exe') -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $setup) { throw 'Instalátor Edge nebyl nalezen.' }
New-Item -Path 'HKLM:\SOFTWARE\Microsoft\EdgeUpdate' -Force | Out-Null
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\EdgeUpdate' -Name 'DoNotUpdateToEdgeWithChromium' -Value 1 -Type DWord
Stop-Process -Name msedge -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $setup.FullName -ArgumentList '--uninstall --system-level --verbose-logging --force-uninstall' -Wait -PassThru
if (-not (Test-Path (Join-Path $base 'msedge.exe'))) { exit 0 }
throw ('Odinstalace Edge selhala (kód ' + $p.ExitCode + ').')
";

    internal static string EdgeUninstallScript => Convert.ToBase64String(Encoding.Unicode.GetBytes(UninstallScript));

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "debloat-chrome", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Chrome: odstranit balast a hlášení",
                Description = "Vypne hlášení statistik, běh na pozadí, rychlé spuštění, reklamní karty, nákupního asistenta a zpětnou vazbu přes zásady Chrome.",
                Note = "Chrome v nastavení ukáže „Spravováno vaší organizací“. Vypnutí to vrátí.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "MetricsReportingEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "BackgroundModeEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "StartupBoostEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "PromotionalTabsEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "ShoppingListEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "UserFeedbackAllowed", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Google\Chrome", "UrlKeyedAnonymizedDataCollectionEnabled", 0, null)
                }
            },
            new()
            {
                Id = "debloat-brave", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Brave: odstranit balast",
                Description = "Vypne Brave Rewards, peněženku, VPN, AI chat, hlášení statistik a běh na pozadí přes zásady Brave.",
                Note = "Brave v nastavení ukáže „Spravováno vaší organizací“. Vypnutí to vrátí.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\BraveSoftware\Brave", "BraveRewardsDisabled", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\BraveSoftware\Brave", "BraveWalletDisabled", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\BraveSoftware\Brave", "BraveVPNDisabled", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\BraveSoftware\Brave", "BraveAIChatEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\BraveSoftware\Brave", "MetricsReportingEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\BraveSoftware\Brave", "BackgroundModeEnabled", 0, null)
                }
            },
            new()
            {
                Id = "debloat-edge", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Edge: odstranit balast a hlášení",
                Description = "Vypne rychlé spuštění, nákupního asistenta, postranní panel, Microsoft Rewards, personalizaci a odesílání diagnostiky v Edge.",
                Note = "Edge v nastavení ukáže „Spravováno vaší organizací“. Vypnutí to vrátí.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "EdgeShoppingAssistantEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "HubsSidebarEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "ShowMicrosoftRewards", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "PersonalizationReportingEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "DiagnosticData", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "ShowRecommendationsEnabled", 0, null)
                }
            },
            new()
            {
                Id = "debloat-edge-off", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Edge: vypnout úplně (aktualizace a služby)",
                Description = "Vypne běh Edge na pozadí a zakáže jeho aktualizace a služby (Edge Update, Elevation Service). Prohlížeč zůstane nainstalovaný, ale nic nedělá na pozadí.",
                Note = "Experimentální: Edge se přestane aktualizovat, což může být riziko, pokud ho používáš. Vypnutí vše vrátí.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\EdgeUpdate", "Update{56EB18F8-B008-4CBD-B6D2-8C97FE7E9062}", 0, null),
                    Catalog.Service("edgeupdate", "auto"),
                    Catalog.Service("edgeupdatem", "demand"),
                    Catalog.Service("MicrosoftEdgeElevationService", "demand")
                }
            },
            new()
            {
                Id = "debloat-edge-uninstall", Category = CatalogDebloat.CatDebloat, IsNew = true, Experimental = true, ApplyOnly = true,
                Name = "Edge: odinstalovat úplně",
                Description = "Odinstaluje Microsoft Edge z počítače a zablokuje jeho automatickou přeinstalaci. WebView2 zůstává, aby fungovaly aplikace, které ho potřebují.",
                Note = "Experimentální a nevratné: zpět se Edge dostane jen novou instalací z webu Microsoftu. Zajisti si jiný prohlížeč.",
                Confirm = "Odinstalovat Microsoft Edge?\n\nTuto akci nejde vrátit zpět tlačítkem. Edge půjde nainstalovat jen znovu z webu Microsoftu. Ujisti se, že máš jiný prohlížeč (Chrome, Brave, Firefox).",
                Ops = new TweakOp[]
                {
                    new CommandOp("edge-uninstall", CatalogDebloat.EdgeUninstalledState, Shell.PowerShellExe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + CatalogDebloat.EdgeUninstallScript, Shell.System32("cmd.exe"), "/c exit 0")
                }
            },
            new()
            {
                Id = "debloat-spotify", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Spotify: nespouštět se systémem",
                Description = "Spotify se přestane spouštět samo po startu Windows. Aplikace zůstane nainstalovaná.",
                Note = "Dostupné, jen když Spotify má položku automatického spuštění.",
                Ops = new TweakOp[]
                {
                    new StartupEntryOp("Spotify")
                }
            },
            new()
            {
                Id = "debloat-discord", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Discord: nespouštět se systémem",
                Description = "Discord se přestane spouštět sám po startu Windows. Aplikace zůstane nainstalovaná.",
                Note = "Dostupné, jen když Discord má položku automatického spuštění.",
                Ops = new TweakOp[]
                {
                    new StartupEntryOp("Discord")
                }
            },
            new()
            {
                Id = "debloat-steam", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Steam: nespouštět se systémem",
                Description = "Steam se přestane spouštět sám po startu Windows. Hry a knihovna zůstanou beze změny.",
                Note = "Dostupné, jen když Steam má položku automatického spuštění.",
                Ops = new TweakOp[]
                {
                    new StartupEntryOp("Steam")
                }
            },
            new()
            {
                Id = "debloat-epic", Category = CatalogDebloat.CatDebloat, IsNew = true,
                Name = "Epic Games Launcher: nespouštět se systémem",
                Description = "Epic Games Launcher se přestane spouštět sám po startu Windows. Hry zůstanou beze změny.",
                Note = "Dostupné, jen když Epic Games Launcher má položku automatického spuštění.",
                Ops = new TweakOp[]
                {
                    new StartupEntryOp("EpicGamesLauncher")
                }
            }
        };
    }
}
