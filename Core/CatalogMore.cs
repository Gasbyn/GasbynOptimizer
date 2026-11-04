namespace GasbynOptimizer.Core;

/// <summary>Další běžné optimalizace soukromí, rozhraní a systému.</summary>
internal static class CatalogMore
{
    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "onedrive-off", Category = Catalog.CatPrivacy,
                Name = "Vypnout synchronizaci OneDrive",
                Description = "Zakáže OneDrive přes zásadu systému, takže se nespouští ani nesynchronizuje soubory.",
                Note = "Soubory v OneDrive zůstanou na disku, jen se přestanou synchronizovat.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\OneDrive", "DisableFileSyncNGSC", 1, null)
                }
            },
            new()
            {
                Id = "settings-sync-off", Category = Catalog.CatPrivacy,
                Name = "Vypnout synchronizaci nastavení účtu Microsoft",
                Description = "Nesynchronizuje motivy, hesla Wi-Fi ani nastavení mezi počítači přes účet Microsoft.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\SettingSync", "DisableSettingSync", 2, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\SettingSync", "DisableSettingSyncUserOverride", 1, null)
                }
            },
            new()
            {
                Id = "cdp-off", Category = Catalog.CatPrivacy,
                Name = "Vypnout sdílení mezi zařízeními",
                Description = "Vypne platformu připojených zařízení (Sdílení v okolí, pokračování na jiném PC).",
                Note = "Nepůjde Sdílení v okolí ani propojení telefonu s počítačem.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableCdp", 0, null)
                }
            },
            new()
            {
                Id = "explorer-ads", Category = Catalog.CatUi, Recommended = true,
                Name = "Vypnout reklamy a oznámení v Průzkumníku",
                Description = "Skryje upozornění poskytovatele synchronizace (reklamy OneDrive a Office) v Průzkumníku.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0, 1)
                }
            },
            new()
            {
                Id = "start-recommendations", Category = Catalog.CatUi,
                Name = "Vypnout doporučení v nabídce Start",
                Description = "Skryje doporučené položky a oznámení účtu v nabídce Start (Windows 11).",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_IrisRecommendations", 0, 1),
                    Catalog.Hkcu(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_AccountNotifications", 0, 1)
                }
            },
            new()
            {
                Id = "startup-sound", Category = Catalog.CatUi,
                Name = "Vypnout zvuk spuštění Windows",
                Description = "Vypne přehrávání melodie při startu a přihlášení.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\BootAnimation", "DisableStartupSound", 1, 0)
                }
            },
            new()
            {
                Id = "prefetch-off", Category = Catalog.CatSystem, Experimental = true, RequiresRestart = true,
                Name = "Vypnout Prefetch a Superfetch v registru",
                Description = "Vypne předčítání souborů při startu. Má smysl hlavně na SSD, kde ho nepotřebuješ.",
                Note = "Na pevném disku (HDD) systém zpomalí. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnablePrefetcher", 0, 3),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters", "EnableSuperfetch", 0, 3)
                }
            }
        };
    }
}
