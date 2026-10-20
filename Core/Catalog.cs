using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>
/// Katalog optimalizací. Každá položka je datově popsaná: co se v systému změní při zapnutí
/// a jaká je výchozí hodnota pro vrácení. Původní hodnoty se navíc zálohují při prvním zapnutí.
/// Zdroje nápadů: ChrisTitusTool, Winhance, EXM a GTweaks (vybrány jen bezpečné a vratné úpravy).
/// </summary>
public static class Catalog
{
    internal const string CatPrivacy = "Soukromí a telemetrie";
    internal const string CatGaming = "Hraní a výkon";
    internal const string CatSystem = "Systém a napájení";
    internal const string CatServices = "Služby a úlohy";
    internal const string CatUi = "Rozhraní a pohodlí";
    internal const string CatInternet = "Internet a síť";
    internal const string CatUpdate = "Windows Update";
    internal const string CatVisual = "Vzhled a animace";

    internal const string Win11 = "Pouze Windows 11.";

    /// <summary>Pořadí kategorií v levém menu.</summary>
    internal static readonly string[] CategoryOrder =
    {
        CatPrivacy, CatUpdate, CatInternet, CatGaming, CatalogPower.CatPower, CatSystem, CatVisual, CatalogFonts.CatFonts, CatUi, CatServices, CatalogHardware.CatHardware, CatalogDebloat.CatDebloat, CatalogAdvanced.CatAdvanced
    };

    // --- pomocné továrny ---------------------------------------------------------------------

    internal static RegistryOp Hklm(string subKey, string name, uint on, uint? off) =>
        new(true, subKey, name, RegistryValueKind.DWord, on, off);

    internal static RegistryOp Hkcu(string subKey, string name, uint on, uint? off) =>
        new(false, subKey, name, RegistryValueKind.DWord, on, off);

    internal static RegistryOp HklmStr(string subKey, string name, string on, string? off) =>
        new(true, subKey, name, RegistryValueKind.String, on, off);

    internal static RegistryOp HkcuStr(string subKey, string name, string on, string? off) =>
        new(false, subKey, name, RegistryValueKind.String, on, off);

    internal static TaskOp SchedTask(string path) => new(path);

    internal static ServiceOp Service(string name, string defaultWord) => new(name, 4, defaultWord);

    private static OpState HibernationState()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power", false);
        return key?.GetValue("HibernateEnabled") is int value && value == 0 ? OpState.On : OpState.Off;
    }

    // --- katalog -----------------------------------------------------------------------------

    public static IReadOnlyList<Tweak> All() => Base().Concat(CatalogExtra.Create()).Concat(CatalogFonts.Create()).Concat(CatalogHardware.Create()).Concat(CatalogPower.Create()).Concat(CatalogAdvanced.Create()).Concat(CatalogMore.Create()).Concat(CatalogDebloat.Create()).Concat(CatalogNetwork.Create()).ToList();

    private static IReadOnlyList<Tweak> Base()
    {
        const string cdm = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        const string mmcss = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        const string classicMenuTree = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";

        return new List<Tweak>
        {
            // ===== Soukromí a telemetrie =====
            new()
            {
                Id = "telemetry", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout telemetrii Windows",
                Description = "Omezí odesílání diagnostických dat společnosti Microsoft na minimum.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, null),
                    Hklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0, null),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1, null)
                }
            },
            new()
            {
                Id = "telemetry-services", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout služby telemetrie",
                Description = "Zakáže služby Connected User Experiences (DiagTrack) a WAP Push (dmwappushservice).",
                Ops = new TweakOp[] { Service("DiagTrack", "auto"), Service("dmwappushservice", "demand") }
            },
            new()
            {
                Id = "telemetry-tasks", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout telemetrické úlohy",
                Description = "Zakáže naplánované úlohy, které sbírají diagnostická data (Compatibility Appraiser, CEIP, zpětná vazba, hlášení chyb, rodinná bezpečnost, mapy a další).",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    SchedTask(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"),
                    SchedTask(@"\Microsoft\Windows\Application Experience\ProgramDataUpdater"),
                    SchedTask(@"\Microsoft\Windows\Application Experience\StartupAppTask"),
                    SchedTask(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"),
                    SchedTask(@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"),
                    SchedTask(@"\Microsoft\Windows\Customer Experience Improvement Program\KernelCeipTask"),
                    SchedTask(@"\Microsoft\Windows\Feedback\Siuf\DmClient"),
                    SchedTask(@"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload"),
                    SchedTask(@"\Microsoft\Windows\Windows Error Reporting\QueueReporting"),
                    SchedTask(@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"),
                    SchedTask(@"\Microsoft\Windows\Customer Experience Improvement Program\BthSQM"),
                    SchedTask(@"\Microsoft\Windows\PI\Sqm-Tasks"),
                    SchedTask(@"\Microsoft\Windows\Autochk\Proxy"),
                    SchedTask(@"\Microsoft\Windows\Maintenance\WinSAT"),
                    SchedTask(@"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem"),
                    SchedTask(@"\Microsoft\Windows\DiskFootprint\Diagnostics"),
                    SchedTask(@"\Microsoft\Windows\NetTrace\GatherNetworkInfo"),
                    SchedTask(@"\Microsoft\Windows\Shell\FamilySafetyMonitor"),
                    SchedTask(@"\Microsoft\Windows\Shell\FamilySafetyRefreshTask"),
                    SchedTask(@"\Microsoft\Windows\WS\WSRefreshBannedAppsListTask"),
                    SchedTask(@"\Microsoft\Windows\Maps\MapsUpdateTask"),
                    SchedTask(@"\Microsoft\Windows\Maps\MapsToastTask")
                }
            },
            new()
            {
                Id = "advertising-id", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout reklamní ID",
                Description = "Aplikace nemohou používat reklamní identifikátor ke sledování napříč aplikacemi.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, 1),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1, null)
                }
            },
            new()
            {
                Id = "activity-history", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout historii aktivit",
                Description = "Windows přestane ukládat a odesílat historii tvých aktivit (Časová osa).",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, null),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, null),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, null)
                }
            },
            new()
            {
                Id = "bing-search", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout webové vyhledávání v nabídce Start",
                Description = "Hledání ve Startu už nebude posílat dotazy do Bingu a zobrazovat webové výsledky.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null),
                    Hkcu(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, null)
                }
            },
            new()
            {
                Id = "consumer-features", Category = CatPrivacy, Recommended = true,
                Name = "Zakázat automatickou instalaci doporučených aplikací",
                Description = "Windows nebude sám stahovat sponzorované aplikace a hry do Startu.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1, null)
                }
            },
            new()
            {
                Id = "suggestions", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout reklamy a návrhy ve Windows",
                Description = "Skryje návrhy ve Startu, tipy na zamykací obrazovce a v Nastavení.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Hkcu(cdm, "SubscribedContent-338388Enabled", 0, 1),
                    Hkcu(cdm, "SubscribedContent-338389Enabled", 0, 1),
                    Hkcu(cdm, "SubscribedContent-353694Enabled", 0, 1),
                    Hkcu(cdm, "SubscribedContent-353696Enabled", 0, 1),
                    Hkcu(cdm, "SubscribedContent-338393Enabled", 0, 1),
                    Hkcu(cdm, "SubscribedContent-310093Enabled", 0, 1),
                    Hkcu(cdm, "SubscribedContent-338387Enabled", 0, 1),
                    Hkcu(cdm, "SystemPaneSuggestionsEnabled", 0, 1),
                    Hkcu(cdm, "SoftLandingEnabled", 0, 1),
                    Hkcu(cdm, "RotatingLockScreenOverlayEnabled", 0, 1),
                    Hkcu(cdm, "SilentInstalledAppsEnabled", 0, 1),
                    Hkcu(cdm, "ContentDeliveryAllowed", 0, 1),
                    Hkcu(cdm, "OemPreInstalledAppsEnabled", 0, 1),
                    Hkcu(cdm, "PreInstalledAppsEnabled", 0, 1),
                    Hkcu(cdm, "PreInstalledAppsEverEnabled", 0, 1)
                }
            },
            new()
            {
                Id = "tailored-experiences", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout přizpůsobená doporučení",
                Description = "Microsoft nebude používat diagnostická data k personalizovaným tipům a reklamám.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, 1)
                }
            },
            new()
            {
                Id = "error-reporting", Category = CatPrivacy, Recommended = true,
                Name = "Vypnout hlášení chyb Windows",
                Description = "Po pádu aplikace se nebudou odesílat hlášení o chybách.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, 0),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Error Reporting", "Disabled", 1, null)
                }
            },
            new()
            {
                Id = "delivery-optimization", Category = CatUpdate, Recommended = true,
                Name = "Vypnout sdílení aktualizací s jinými PC",
                Description = "Windows Update stahuje jen od Microsoftu a nenahrává aktualizace ostatním přes tvůj internet.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0, null)
                }
            },
            new()
            {
                Id = "copilot", Category = CatPrivacy,
                Name = "Vypnout Windows Copilot",
                Description = "Skryje Copilota a zakáže jeho spouštění přes zásady systému.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, null),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1, null)
                }
            },
            new()
            {
                Id = "recall", Category = CatPrivacy, MinBuild = 22621,
                Name = "Vypnout Windows Recall",
                Description = "Zakáže ukládání snímků obrazovky a jejich analýzu funkcí Recall.",
                Note = "Týká se jen počítačů, kde je funkce Recall dostupná.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsAI", "DisableAIDataAnalysis", 1, null)
                }
            },
            new()
            {
                Id = "background-apps", Category = CatPrivacy,
                Name = "Zakázat běh aplikací na pozadí",
                Description = "Aplikace z Microsoft Storu nebudou běžet na pozadí, šetří to RAM a CPU.",
                Note = "Některé aplikace z Obchodu pak nebudou dostávat oznámení, dokud je neotevřeš.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", "LetAppsRunInBackground", 2, null),
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications", "GlobalUserDisabled", 1, 0)
                }
            },
            new()
            {
                Id = "edge-background", Category = CatPrivacy,
                Name = "Zakázat spouštění Edge na pozadí",
                Description = "Edge se nespouští předem při startu systému a nezůstává běžet po zavření.",
                Note = "Edge může zobrazit hlášku, že je spravován organizací. To je očekávané.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "StartupBoostEnabled", 0, null),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0, null)
                }
            },
            new()
            {
                Id = "widgets", Category = CatPrivacy, MinBuild = 22000,
                Name = "Vypnout Widgety",
                Description = "Zakáže panel Widgety a jeho proces na pozadí. " + Win11,
                Ops = new TweakOp[]
                {
                    Hklm(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0, null)
                }
            },

            // ===== Hraní a výkon =====
            new()
            {
                Id = "game-mode", Category = CatGaming, Recommended = true,
                Name = "Zapnout herní režim Windows",
                Description = "Windows upřednostní hru před úlohami na pozadí a omezí rušení aktualizacemi.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, null),
                    Hkcu(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, null)
                }
            },
            new()
            {
                Id = "game-dvr", Category = CatGaming, Recommended = true,
                Name = "Vypnout nahrávání Xbox Game Bar (Game DVR)",
                Description = "Vypne záznam na pozadí, který ubírá výkon. OBS a další nahrávání to neovlivní.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, 1),
                    Hklm(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, null),
                    Hkcu(@"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0, 1)
                }
            },
            new()
            {
                Id = "hags", Category = CatGaming, Requires = Needs.HagsGpu,
                Name = "Plánování GPU s hardwarovou akcelerací (HAGS)",
                Description = "Grafická karta si sama řídí frontu úloh. Nutné například pro generování snímků (DLSS 3 / FSR 3).",
                Note = "Doporučeno pro GeForce RTX 20xx a novější, Radeon RX 5000 a novější. Vyžaduje aktuální ovladač.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1)
                }
            },
            new()
            {
                Id = "mouse-acceleration", Category = CatGaming, Recommended = true,
                Name = "Vypnout akceleraci myši",
                Description = "Pohyb kurzoru odpovídá přesně pohybu myši (důležité pro míření ve hrách).",
                Ops = new TweakOp[]
                {
                    HkcuStr(@"Control Panel\Mouse", "MouseSpeed", "0", "1"),
                    HkcuStr(@"Control Panel\Mouse", "MouseThreshold1", "0", "6"),
                    HkcuStr(@"Control Panel\Mouse", "MouseThreshold2", "0", "10")
                }
            },
            new()
            {
                Id = "sticky-keys", Category = CatGaming, Recommended = true,
                RequiresRestart = false,
                Name = "Vypnout vyskakovací okno lepících kláves",
                Description = "Pětinásobný stisk Shiftu už ve hře neotevře dialog Sticky Keys.",
                Ops = new TweakOp[]
                {
                    HkcuStr(@"Control Panel\Accessibility\StickyKeys", "Flags", "506", "510"),
                    HkcuStr(@"Control Panel\Accessibility\Keyboard Response", "Flags", "122", "126"),
                    HkcuStr(@"Control Panel\Accessibility\ToggleKeys", "Flags", "58", "62")
                }
            },
            new()
            {
                Id = "mmcss", Category = CatGaming, Recommended = true,
                Name = "Optimalizace odezvy systému (MMCSS)",
                Description = "Vypne omezování sítě pro multimédia a upřednostní popředí před úlohami na pozadí.",
                Note = "Efekt je malý a závisí na PC, ale úprava je bezpečná.",
                Ops = new TweakOp[]
                {
                    Hklm(mmcss, "NetworkThrottlingIndex", 0xFFFFFFFF, 10),
                    Hklm(mmcss, "SystemResponsiveness", 0, 20)
                }
            },
            new()
            {
                Id = "games-priority", Category = CatGaming, Recommended = true,
                Name = "Vyšší priorita her v plánovači",
                Description = "Hry dostanou ve Windows vyšší plánovací kategorii a prioritu procesoru.",
                Ops = new TweakOp[]
                {
                    Hklm(mmcss + @"\Tasks\Games", "Priority", 6, 2),
                    HklmStr(mmcss + @"\Tasks\Games", "Scheduling Category", "High", "Medium"),
                    HklmStr(mmcss + @"\Tasks\Games", "SFIO Priority", "High", "Normal")
                }
            },
            new()
            {
                Id = "power-throttling", Category = CatGaming, Recommended = true,
                Name = "Vypnout omezování výkonu procesů (Power Throttling)",
                Description = "Windows nebude snižovat výkon procesů běžících na pozadí, hry a streamování jsou stabilnější.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1, null)
                }
            },
            // ===== Systém a napájení =====
            new()
            {
                Id = "fast-startup", Category = CatSystem, Recommended = true,
                Name = "Vypnout rychlé spuštění",
                Description = "Vypnutí PC je skutečné vypnutí. Odstraní problémy s ovladači a aktualizacemi po „vypnutí“.",
                Ops = new TweakOp[]
                {
                    Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled", 0, 1)
                }
            },
            new()
            {
                Id = "startup-delay", Category = CatSystem, Recommended = true,
                Name = "Odstranit zpoždění spouštěných aplikací",
                Description = "Aplikace ve Startu se po přihlášení spustí hned, bez umělé několikasekundové prodlevy.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, null)
                }
            },
            new()
            {
                Id = "menu-delay", Category = CatVisual, Recommended = true,
                Name = "Zrychlit zobrazování nabídek",
                Description = "Nabídky se otevírají bez prodlevy (výchozích 400 ms).",
                Ops = new TweakOp[]
                {
                    HkcuStr(@"Control Panel\Desktop", "MenuShowDelay", "0", "400")
                }
            },
            new()
            {
                Id = "disable-effects", Category = CatVisual,
                Name = "Vypnout průhlednost a animace",
                Description = "Méně efektů v systému, plynulejší chod na slabších počítačích.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, 1),
                    HkcuStr(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", "1"),
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, 1)
                }
            },
            new()
            {
                Id = "fast-shutdown", Category = CatSystem,
                Name = "Rychlejší vypínání systému",
                Description = "Zkrátí čekání na ukončení služeb a aplikací při vypnutí a restartu.",
                Note = "Aplikace, které se ukládají dlouho, mohou být ukončeny dříve.",
                Ops = new TweakOp[]
                {
                    HklmStr(@"SYSTEM\CurrentControlSet\Control", "WaitToKillServiceTimeout", "2000", "5000"),
                    HkcuStr(@"Control Panel\Desktop", "WaitToKillAppTimeout", "2000", "20000"),
                    HkcuStr(@"Control Panel\Desktop", "HungAppTimeout", "2000", "5000")
                }
            },
            new()
            {
                Id = "hibernation", Category = CatSystem,
                Name = "Vypnout hibernaci",
                Description = "Smaže soubor hiberfil.sys a uvolní několik GB místa na systémovém disku.",
                Note = "Přestane fungovat hibernace a rychlé spuštění.",
                Ops = new TweakOp[]
                {
                    new CommandOp("hibernate", HibernationState,
                        Shell.System32("powercfg.exe"), "/hibernate off",
                        Shell.System32("powercfg.exe"), "/hibernate on")
                }
            },

            // ===== Služby a úlohy =====
            new()
            {
                Id = "unneeded-services", Category = CatServices, Recommended = true,
                Name = "Vypnout nepotřebné služby",
                Description = "Fax, ukázkový režim obchodu (RetailDemo), sdílení médií WMP, Mapy a vzdálený registr.",
                Ops = new TweakOp[]
                {
                    Service("Fax", "demand"),
                    Service("RetailDemo", "demand"),
                    Service("WMPNetworkSvc", "demand"),
                    Service("MapsBroker", "auto"),
                    Service("RemoteRegistry", "demand")
                }
            },
            new()
            {
                Id = "sysmain", Category = CatServices, Requires = Needs.Ssd, RecommendWhen = p => p.SystemDriveIsSsd == true,
                Name = "Vypnout SysMain (Superfetch)",
                Description = "Zastaví předčítání aplikací do paměti. Na SSD nemá význam a šetří to RAM a zápisy.",
                Note = "Doporučeno jen pro systém na SSD disku.",
                Ops = new TweakOp[] { Service("SysMain", "auto") }
            },

            // ===== Rozhraní a pohodlí =====
            new()
            {
                Id = "file-extensions", Category = CatUi, Recommended = true,
                RequiresRestart = false,
                Name = "Zobrazovat přípony souborů",
                Description = "V Průzkumníku uvidíš skutečné přípony (např. .exe, .pdf), což pomáhá odhalit podvrhy.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0, 1)
                }
            },
            new()
            {
                Id = "explorer-this-pc", Category = CatUi,
                RequiresRestart = false,
                Name = "Otevírat Průzkumník na „Tento počítač“",
                Description = "Průzkumník se místo Rychlého přístupu otevře na seznamu disků.",
                Ops = new TweakOp[]
                {
                    Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "LaunchTo", 1, 2)
                }
            },
            new()
            {
                Id = "classic-context-menu", Category = CatUi, MinBuild = 22000,
                Name = "Klasická kontextová nabídka",
                Description = "Vrátí plnou nabídku po kliknutí pravým tlačítkem bez „Zobrazit další možnosti“. " + Win11,
                Ops = new TweakOp[]
                {
                    new RegistryOp(false, classicMenuTree + @"\InprocServer32", "", RegistryValueKind.String, "", null, classicMenuTree)
                }
            }
        };
    }
}
