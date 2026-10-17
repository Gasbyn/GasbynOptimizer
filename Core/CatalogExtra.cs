using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Další optimalizace: internet, Windows Update, soukromí, hraní, vzhled a animace.</summary>
internal static class CatalogExtra
{
    private static List<TweakOp> NagleOps()
    {
        // Vypnutí Nagleova algoritmu a zpožděných ACK na každém síťovém rozhraní.
        const string root = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
        var ops = new List<TweakOp>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(root, false);
            if (key == null) return ops;

            foreach (var name in key.GetSubKeyNames())
            {
                ops.Add(Catalog.Hklm(root + "\\" + name, "TcpAckFrequency", 1, null));
                ops.Add(Catalog.Hklm(root + "\\" + name, "TCPNoDelay", 1, null));
            }
        }
        catch (Exception ex)
        {
            Log.Write("Rozhraní sítě se nepodařilo načíst: " + ex.Message);
        }
        return ops;
    }

    public static IReadOnlyList<Tweak> Create()
    {
        const string explorerAdv = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        const string personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        const string wuPolicy = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        const string gameConfig = @"System\GameConfigStore";

        return new List<Tweak>
        {
            // ===== Internet a síť =====
            new()
            {
                Id = "nagle", Experimental = true, Category = Catalog.CatInternet,
                Name = "Nižší odezva sítě (vypnout Nagleův algoritmus)",
                Description = "Malé pakety se odesílají hned, bez čekání na seskupení. Může snížit ping v některých online hrách.",
                Note = "Efekt závisí na hře a připojení, na stahování souborů nemá vliv. Týká se síťových karet existujících v době zapnutí.",
                Ops = NagleOps()
            },
            new()
            {
                Id = "teredo", Experimental = true, Category = Catalog.CatInternet,
                Name = "Vypnout Teredo a IPv6 přechodové tunely",
                Description = "Zakáže tunelování IPv6 přes IPv4 (Teredo, 6to4, ISATAP), které většina domácností nepoužívá.",
                Note = "Může ovlivnit párty chat a některé multiplayerové funkce na Xboxu a v Microsoft Storu (NAT).",
                Ops = new TweakOp[]
                {
                    Catalog.HklmStr(@"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition", "Teredo_State", "Disabled", null),
                    Catalog.HklmStr(@"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition", "6to4_State", "Disabled", null),
                    Catalog.HklmStr(@"SOFTWARE\Policies\Microsoft\Windows\TCPIP\v6Transition", "ISATAP_State", "Disabled", null)
                }
            },
            new()
            {
                Id = "llmnr", Category = Catalog.CatInternet, Recommended = true,
                Name = "Vypnout LLMNR",
                Description = "Vypne vyhledávání jmen v místní síti přes multicast, které bývá zneužíváno k útokům na domácí a firemní sítě.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient", "EnableMulticast", 0, null)
                }
            },
            new()
            {
                Id = "remote-assistance", Category = Catalog.CatInternet, Recommended = true,
                Name = "Vypnout vzdálenou pomoc",
                Description = "Nikdo se nemůže připojit k tvé ploše přes funkci Vzdálená pomoc Windows.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0, 1)
                }
            },

            // ===== Windows Update =====
            new()
            {
                Id = "no-auto-reboot", Category = Catalog.CatUpdate, Recommended = true,
                Name = "Neprovádět automatický restart po aktualizacích",
                Description = "Windows nerestartuje počítač sám, když je někdo přihlášený (nepřijdeš o rozdělanou práci nebo stream).",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(wuPolicy + @"\AU", "NoAutoRebootWithLoggedOnUsers", 1, null)
                }
            },
            new()
            {
                Id = "wu-no-drivers", Category = Catalog.CatUpdate,
                Name = "Nestahovat ovladače přes Windows Update",
                Description = "Ovladače (hlavně grafiky) nepřepíše Windows Update starší verzí, instaluješ je sám od výrobce.",
                Note = "Pak je potřeba hlídat aktualizace ovladačů ručně.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(wuPolicy, "ExcludeWUDriversInQualityUpdate", 1, null)
                }
            },
            new()
            {
                Id = "wu-defer-features", Experimental = true, Category = Catalog.CatUpdate,
                Name = "Odložit velké aktualizace Windows o 30 dní",
                Description = "Funkční aktualizace dorazí až měsíc po vydání, kdy jsou nejhorší chyby většinou opravené. Bezpečnostní záplaty to neovlivní.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(wuPolicy, "DeferFeatureUpdates", 1, null),
                    Catalog.Hklm(wuPolicy, "DeferFeatureUpdatesPeriodInDays", 30, null)
                }
            },
            new()
            {
                Id = "store-auto-update", Category = Catalog.CatUpdate,
                Name = "Vypnout automatické aktualizace aplikací z Microsoft Storu",
                Description = "Aplikace z Obchodu se nebudou samy aktualizovat na pozadí.",
                Note = "Aktualizace pak provádíš ručně v Obchodě.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\WindowsStore", "AutoDownload", 2, null)
                }
            },

            // ===== Soukromí =====
            new()
            {
                Id = "location", Category = Catalog.CatPrivacy,
                Name = "Vypnout určování polohy",
                Description = "Windows a aplikace nezjišťují polohu počítače.",
                Note = "Přestanou fungovat funkce vyžadující polohu (mapy, počasí podle místa).",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1, null)
                }
            },
            new()
            {
                Id = "cortana-search", Category = Catalog.CatPrivacy, Recommended = true,
                Name = "Vypnout Cortanu a cloudové hledání",
                Description = "Hledání ve Windows zůstává jen na tvém počítači, bez Cortany a cloudových výsledků.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", 0, null)
                }
            },
            new()
            {
                Id = "clipboard-cloud", Category = Catalog.CatPrivacy, Recommended = true,
                Name = "Vypnout sdílení schránky mezi zařízeními",
                Description = "Obsah schránky se neodesílá přes účet Microsoft na jiná zařízení.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "AllowCrossDeviceClipboard", 0, null)
                }
            },
            new()
            {
                Id = "input-personalization", Category = Catalog.CatPrivacy, Recommended = true,
                Name = "Vypnout sběr psaní a rukopisu",
                Description = "Windows si nepamatuje, co píšeš, a nevytváří z toho osobní slovník.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", 1, 0),
                    Catalog.Hkcu(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", 1, 0)
                }
            },
            new()
            {
                Id = "spotlight", Category = Catalog.CatPrivacy, Recommended = true,
                Name = "Vypnout Windows Spotlight",
                Description = "Zamykací obrazovka a plocha nezobrazují reklamy a „zajímavé“ tipy od Microsoftu.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", 1, null),
                    Catalog.Hkcu(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1, null)
                }
            },
            new()
            {
                Id = "track-progs", Category = Catalog.CatPrivacy, Recommended = true,
                RequiresRestart = false,
                Name = "Nesledovat spouštění aplikací",
                Description = "Start menu si nepamatuje, které programy nejčastěji spouštíš.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "Start_TrackProgs", 0, 1)
                }
            },
            new()
            {
                Id = "online-speech", Category = Catalog.CatPrivacy,
                RequiresRestart = false,
                Name = "Vypnout online rozpoznávání řeči",
                Description = "Hlasové diktování a příkazy se neodesílají do cloudu Microsoftu.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy", "HasAccepted", 0, null)
                }
            },
            new()
            {
                Id = "search-highlights", Category = Catalog.CatPrivacy, Recommended = true,
                RequiresRestart = false,
                Name = "Vypnout Search Highlights",
                Description = "Vyhledávací pole nezobrazuje denní ilustrace a novinky.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0, 1)
                }
            },

            // ===== Hraní a výkon =====
            new()
            {
                Id = "fullscreen-opt", Category = Catalog.CatGaming,
                Name = "Vypnout optimalizace celé obrazovky",
                Description = "Hry v režimu celé obrazovky běží přímo, bez překrytí Windows. Může snížit vstupní zpoždění.",
                Note = "U některých her může být výsledek horší, v takovém případě přepínač vypni.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(gameConfig, "GameDVR_FSEBehaviorMode", 2, null),
                    Catalog.Hkcu(gameConfig, "GameDVR_HonorUserFSEBehaviorMode", 1, null),
                    Catalog.Hkcu(gameConfig, "GameDVR_DXGIHonorFSEWindowsCompatible", 1, null)
                }
            },
            new()
            {
                Id = "windowed-opt", Category = Catalog.CatGaming,
                Name = "Optimalizace her v okně",
                Description = "Hry v okně nebo bez rámečku používají modernější způsob vykreslování s nižší latencí.",
                Note = "Přepíše uživatelské DirectX volby ve Windows. Vyžaduje Windows 10 verze 1903 a novější.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.HkcuStr(@"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", "SwapEffectUpgradeEnable=1;", null)
                }
            },
            new()
            {
                Id = "priority-separation", Experimental = true, Category = Catalog.CatGaming,
                Name = "Přednost aplikace v popředí",
                Description = "Procesor dává více času aplikaci, kterou právě používáš (hře), než službám na pozadí.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, 2)
                }
            },
            new()
            {
                Id = "keyboard-speed", Category = Catalog.CatGaming,
                Name = "Nejrychlejší odezva klávesnice",
                Description = "Nulové zpoždění před opakováním znaku a maximální rychlost opakování.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.HkcuStr(@"Control Panel\Keyboard", "KeyboardDelay", "0", "1"),
                    Catalog.HkcuStr(@"Control Panel\Keyboard", "KeyboardSpeed", "31", "31")
                }
            },
            new()
            {
                Id = "gamebar-tips", Category = Catalog.CatGaming, Recommended = true,
                RequiresRestart = false,
                Name = "Vypnout úvodní tipy Xbox Game Baru",
                Description = "Game Bar se při spuštění hry nezeptá na tipy a přehledy.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\GameBar", "ShowStartupPanel", 0, 1)
                }
            },
            new()
            {
                Id = "xbox-services", Experimental = true, Category = Catalog.CatGaming,
                Name = "Vypnout služby Xboxu",
                Description = "Zakáže služby Xbox Live (přihlášení, cloudové uložené hry, síť, příslušenství).",
                Note = "Nepoužívej, pokud hraješ hry z Xbox aplikace, Game Pass nebo hry vyžadující přihlášení k Xbox Live.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("XblAuthManager", "demand"),
                    Catalog.Service("XblGameSave", "demand"),
                    Catalog.Service("XboxNetApiSvc", "demand"),
                    Catalog.Service("XboxGipSvc", "demand")
                }
            },

            // ===== Systém a služby =====
            new()
            {
                Id = "bsod-no-reboot", Category = Catalog.CatSystem,
                Name = "Nerestartovat sám po modré obrazovce",
                Description = "Po chybě systému zůstane modrá obrazovka zobrazená a stihneš si opsat kód chyby.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\CrashControl", "AutoReboot", 0, 1)
                }
            },
            new()
            {
                Id = "wsearch", Category = Catalog.CatServices,
                Name = "Vypnout indexování souborů (Windows Search)",
                Description = "Zastaví službu indexování, která na pozadí zatěžuje disk a procesor.",
                Note = "Hledání souborů v Průzkumníku a ve Startu bude pomalejší.",
                Ops = new TweakOp[] { Catalog.Service("WSearch", "auto") }
            },

            // ===== Vzhled a animace =====
            new()
            {
                Id = "dark-mode", Category = Catalog.CatVisual,
                Name = "Tmavý režim Windows",
                Description = "Systém i aplikace používají tmavý vzhled.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(personalize, "AppsUseLightTheme", 0, 1),
                    Catalog.Hkcu(personalize, "SystemUsesLightTheme", 0, 1)
                }
            },
            new()
            {
                Id = "first-logon-animation", Category = Catalog.CatVisual, Recommended = true,
                Name = "Vypnout úvodní animaci po přihlášení",
                Description = "Po přihlášení se hned zobrazí plocha, bez úvodní animace „Vítejte“.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableFirstLogonAnimation", 0, 1)
                }
            },
            new()
            {
                Id = "aero-peek", Category = Catalog.CatVisual,
                Name = "Vypnout náhled plochy (Aero Peek)",
                Description = "Najetí na tlačítko Zobrazit plochu nezprůhlední okna, ušetří se vykreslování.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Windows\DWM", "EnableAeroPeek", 0, 1)
                }
            },
            new()
            {
                Id = "listview-effects", Category = Catalog.CatVisual,
                Name = "Vypnout stíny a průhledný výběr v seznamech",
                Description = "Zjednodušení efektů ikon na ploše a v Průzkumníku.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "ListviewShadow", 0, 1),
                    Catalog.Hkcu(explorerAdv, "ListviewAlphaSelect", 0, 1)
                }
            },
            new()
            {
                Id = "aero-shake", Category = Catalog.CatVisual, Recommended = true,
                Name = "Vypnout Aero Shake",
                Description = "Zatřesení oknem už nezminimalizuje všechna ostatní okna.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "DisallowShaking", 1, 0)
                }
            },
            new()
            {
                Id = "compact-view", Category = Catalog.CatVisual, MinBuild = 22000,
                Name = "Kompaktní zobrazení Průzkumníka",
                Description = "Menší rozestupy mezi položkami, na obrazovku se vejde více souborů. " + Catalog.Win11,
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "UseCompactMode", 1, 0)
                }
            },
            new()
            {
                Id = "taskbar-left", Category = Catalog.CatVisual, MinBuild = 22000,
                Name = "Hlavní panel zarovnaný vlevo",
                Description = "Tlačítko Start a ikony jsou vlevo jako ve Windows 10. " + Catalog.Win11,
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "TaskbarAl", 0, 1)
                }
            },

            // ===== Rozhraní =====
            new()
            {
                Id = "show-seconds", Category = Catalog.CatUi,
                Name = "Skrýt sekundy v hodinách",
                Description = "Hodiny na hlavním panelu ukazují jen hodiny a minuty. Méně rušivé a o trochu méně překreslování.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "ShowSecondsInSystemClock", 0, 1)
                }
            },
            new()
            {
                Id = "hidden-files", Category = Catalog.CatUi,
                Name = "Zobrazovat skryté soubory",
                Description = "V Průzkumníku se zobrazí i soubory a složky se skrytým atributem.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "Hidden", 1, 2)
                }
            },
            new()
            {
                Id = "task-view-button", Category = Catalog.CatUi,
                Name = "Skrýt tlačítko Zobrazení úloh",
                Description = "Z hlavního panelu zmizí tlačítko přehledu oken a ploch.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "ShowTaskViewButton", 0, 1)
                }
            },
            new()
            {
                Id = "taskbar-chat", Category = Catalog.CatUi, MinBuild = 22000, Recommended = true,
                Name = "Skrýt ikonu Chat (Teams)",
                Description = "Z hlavního panelu zmizí ikona chatu Teams. " + Catalog.Win11,
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(explorerAdv, "TaskbarMn", 0, 1)
                }
            },
            new()
            {
                Id = "taskbar-search", Category = Catalog.CatUi,
                Name = "Skrýt vyhledávací pole na hlavním panelu",
                Description = "Hledání zůstane dostupné klávesou Windows, jen zmizí pole z hlavního panelu.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 0, 1)
                }
            },
            new()
            {
                Id = "this-pc-icon", Category = Catalog.CatUi,
                Name = "Skrýt ikonu „Tento počítač“ na ploše",
                Description = "Z plochy zmizí ikona Tento počítač a plocha bude čistší.",
                RequiresRestart = false,
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                        "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", 1, 0)
                }
            }
        };
    }
}
