namespace GasbynOptimizer.Core;

/// <summary>Hlubší a rizikovější optimalizace. Všechny jsou experimentální a nedoporučené pro většinu lidí.</summary>
internal static class CatalogAdvanced
{
    internal const string CatAdvanced = "Pokročilé";

    /// <summary>Hypervizor je vypnutý, když bcdedit hlásí hypervisorlaunchtype Off.</summary>
    internal static OpState HypervisorState()
    {
        var result = Shell.Run(Shell.System32("bcdedit.exe"), "/enum {current}", 15000);
        if (result.ExitCode != 0) return OpState.Missing;
        return System.Text.RegularExpressions.Regex.IsMatch(result.Output, @"hypervisorlaunchtype\s+Off", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            ? OpState.On : OpState.Off;
    }

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "adv-diagnostics", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout hlubší diagnostiku Windows",
                Description = "Zakáže diagnostické služby (Diagnostic Policy, Diagnostic Execution, Troubleshooting Platform). Windows pak sám nehledá a neřeší problémy.",
                Note = "Nedoporučeno: přestanou fungovat automatické poradce při potížích a část hlášení o chybách. Vypnutí vrátí původní stav.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("DPS", "auto"),
                    Catalog.Service("WdiServiceHost", "demand"),
                    Catalog.Service("WdiSystemHost", "demand"),
                    Catalog.Service("diagsvc", "demand")
                }
            },
            new()
            {
                Id = "adv-printing", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout podporu tiskáren a tisku",
                Description = "Zakáže službu Tisková fronta (Spooler) a oznámení tiskáren. Šetří paměť a zavírá známý cíl útoků.",
                Note = "Nedoporučeno, pokud tiskneš: nepůjde tisknout ani do PDF přes tiskárnu. Vypnutí vrátí původní stav.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("Spooler", "auto"),
                    Catalog.Service("PrintNotify", "demand")
                }
            },
            new()
            {
                Id = "adv-midi", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout službu MIDI",
                Description = "Zakáže Windows MIDI službu. Hudební nástroje a programy pro MIDI pak nebudou fungovat.",
                Note = "Vypni jen pokud MIDI zařízení nepoužíváš.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("midisrv", "demand")
                }
            },
            new()
            {
                Id = "adv-server-sharing", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout sdílení souborů a síťové servery",
                Description = "Zakáže službu Server (sdílení složek a tiskáren v síti), Prohlížeč počítačů a odnímání sdílení.",
                Note = "Nedoporučeno v domácí síti se sdílením složek: ostatní počítače se k tvému nedostanou. Vypnutí vrátí původní stav.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("LanmanServer", "auto"),
                    Catalog.Service("Browser", "demand")
                }
            },
            new()
            {
                Id = "adv-remote-desktop", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout vzdálenou plochu",
                Description = "Zakáže služby vzdálené plochy (RDP) a související pomocné služby. Zmenší plochu pro útoky z internetu.",
                Note = "Po vypnutí se na tento PC nepůjde připojit vzdálenou plochou.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("TermService", "demand"),
                    Catalog.Service("UmRdpService", "demand"),
                    Catalog.Service("SessionEnv", "demand")
                }
            },
            new()
            {
                Id = "adv-windows-update", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout Windows Update úplně",
                Description = "Zakáže automatické aktualizace (zásada) a služby Windows Update a Update Orchestrator.",
                Note = "Silně nedoporučeno: Windows nebude dostávat opravy zabezpečení. Zapni jen dočasně a aktualizace spouštěj ručně po vrácení.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", "NoAutoUpdate", 1, null),
                    Catalog.Service("wuauserv", "demand"),
                    Catalog.Service("UsoSvc", "auto")
                }
            },
            new()
            {
                Id = "adv-bluetooth", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout Bluetooth služby",
                Description = "Zakáže služby Bluetooth. Šetří paměť, když Bluetooth nepoužíváš.",
                Note = "Nepůjdou Bluetooth myši, sluchátka ani ovladače. Vypnutí vrátí původní stav.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("bthserv", "demand"),
                    Catalog.Service("BthAvctpSvc", "demand")
                }
            },
            new()
            {
                Id = "adv-smartcard-bio", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout čipové karty a biometrii",
                Description = "Zakáže služby čteček čipových karet a Windows Hello otisku/obličeje.",
                Note = "Nepůjde přihlášení otiskem ani obličejem (Windows Hello).",
                Ops = new TweakOp[]
                {
                    Catalog.Service("SCardSvr", "demand"),
                    Catalog.Service("ScDeviceEnum", "demand"),
                    Catalog.Service("SCPolicySvc", "demand"),
                    Catalog.Service("WbioSrvc", "demand")
                }
            },
            new()
            {
                Id = "adv-sensors-location", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout senzory a polohové služby",
                Description = "Zakáže služby senzorů a geolokace. Hodí se na stolní PC.",
                Note = "Na notebooku se může rozbít automatické otáčení obrazovky nebo jas podle světla.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("SensrSvc", "demand"),
                    Catalog.Service("SensorService", "demand"),
                    Catalog.Service("SensorDataService", "demand"),
                    Catalog.Service("lfsvc", "demand")
                }
            },
            new()
            {
                Id = "adv-smartscreen", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout SmartScreen",
                Description = "Vypne kontrolu stahovaných souborů a programů přes Microsoft SmartScreen.",
                Note = "Nedoporučeno: ztratíš varování před škodlivými programy.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableSmartScreen", 0, null)
                }
            },
            new()
            {
                Id = "adv-defender-cloud", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout cloudové hlášení Defenderu",
                Description = "Vypne odesílání vzorků a hlášení Microsoft Defenderu (cloudová ochrana a automatické odesílání).",
                Note = "Defender nadále chrání, ale hůř se brání úplně novým hrozbám.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "SpynetReporting", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "SubmitSamplesConsent", 2, null)
                }
            },
            new()
            {
                Id = "adv-hvci", Category = CatAdvanced, Experimental = true, RequiresRestart = true,
                Name = "Vypnout izolaci jádra (Integrita paměti)",
                Description = "Vypne virtualizační ochranu jádra (HVCI). V některých hrách zvýší FPS.",
                Note = "Nedoporučeno: snižuje ochranu proti škodlivému kódu v jádře. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0, null)
                }
            },
            new()
            {
                Id = "adv-spectre", Category = CatAdvanced, Experimental = true, RequiresRestart = true,
                Name = "Vypnout ochranu Spectre a Meltdown",
                Description = "Vypne softwarové opravy procesorových chyb Spectre a Meltdown. Na starších procesorech zrychlí systém.",
                Note = "Silně nedoporučeno: oslabuje zabezpečení procesoru. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverride", 3, null),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "FeatureSettingsOverrideMask", 3, null)
                }
            },
            new()
            {
                Id = "adv-paging-executive", Category = CatAdvanced, Experimental = true, RequiresRestart = true,
                Name = "Držet jádro systému v paměti RAM",
                Description = "Zakáže stránkování jádra a ovladačů na disk, takže zůstanou v RAM. Hodí se při větším množství paměti (16 GB a víc).",
                Note = "Experimentální: na PC s málem RAM může způsobit nedostatek paměti. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1, 0)
                }
            },
            new()
            {
                Id = "adv-push-notifications", Category = CatAdvanced, Experimental = true,
                Name = "Vypnout službu push oznámení",
                Description = "Zakáže službu oznámení Windows (WpnService). Aplikace přestanou zobrazovat oznámení.",
                Note = "Experimentální: nechodí oznámení z aplikací ani z Obchodu.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("WpnService", "auto")
                }
            },
            new()
            {
                Id = "adv-defender-off", Category = CatAdvanced, Experimental = true, RequiresRestart = true,
                Name = "Vypnout Microsoft Defender",
                Description = "Vypne antivirus Microsoft Defender a ochranu v reálném čase přes zásady.",
                Note = "Silně nedoporučeno bez jiného antiviru. Při zapnuté ochraně proti neoprávněným změnám (Tamper Protection) Windows změnu nepřijme.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, null)
                }
            },
            new()
            {
                Id = "adv-xbox-controller", Category = CatAdvanced, IsNew = true, Experimental = true,
                Name = "Vypnout podporu herních ovladačů (gamepad)",
                Description = "Zakáže služby Xbox Accessory Management a Microsoft GameInput. Gamepady a ovladače připojené přes Xbox protokol pak nemusí fungovat.",
                Note = "Nepoužívej, pokud hraješ s gamepadem (Xbox ovladač, Xbox příslušenství). Klávesnice a myš nejsou dotčené.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("XboxGipSvc", "demand"),
                    Catalog.Service("GameInputSvc", "demand")
                }
            },
            new()
            {
                Id = "adv-security-services", Category = CatAdvanced, IsNew = true, Experimental = true, RequiresRestart = true,
                Name = "Vypnout zbytečné bezpečnostní služby",
                Description = "Zakáže Centrum zabezpečení, službu stavu zabezpečení a webovou ochranu Defenderu.",
                Note = "Experimentální: Windows nebude hlásit stav antiviru a firewallu. Nenalezené služby se přeskočí.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("wscsvc", "auto"),
                    Catalog.Service("SecurityHealthService", "demand"),
                    Catalog.Service("webthreatdefsvc", "demand"),
                    Catalog.Service("Sense", "demand")
                }
            },
            new()
            {
                Id = "adv-firewall-off", Category = CatAdvanced, IsNew = true, Experimental = true,
                Name = "Vypnout Windows Firewall",
                Description = "Vypne Windows Defender Firewall pro všechny typy sítí přes zásady.",
                Note = "Silně nedoporučeno bez jiného firewallu, hlavně na veřejných sítích.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\DomainProfile", "EnableFirewall", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\StandardProfile", "EnableFirewall", 0, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\PublicProfile", "EnableFirewall", 0, null)
                }
            },
            new()
            {
                Id = "adv-sehop", Category = CatAdvanced, IsNew = true, Experimental = true, RequiresRestart = true,
                Name = "Vypnout SEHOP (ochrana výjimek)",
                Description = "Vypne kontrolu řetězce výjimek, jednu z mitigací proti zneužití paměti. Dřívější doporučení pro starší hry a programy.",
                Note = "Experimentální: oslabuje ochranu proti zneužití paměti. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Session Manager\kernel", "DisableExceptionChainValidation", 1, null)
                }
            },
            new()
            {
                Id = "adv-vbs", Category = CatAdvanced, IsNew = true, Experimental = true, RequiresRestart = true,
                Name = "Částečně vypnout virtualizaci: VBS",
                Description = "Vypne zabezpečení na bázi virtualizace (VBS). V hrách zlepší výkon, Hyper-V a virtuální stroje zůstávají funkční.",
                Note = "Nedoporučeno: snižuje ochranu jádra. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0, null)
                }
            },
            new()
            {
                Id = "adv-hypervisor", Category = CatAdvanced, IsNew = true, Experimental = true, RequiresRestart = true,
                Name = "Úplně vypnout virtualizaci (Hyper-V)",
                Description = "Vypne hypervizor Windows a služby Hyper-V. Přestane fungovat Hyper-V, WSL 2, Windows Sandbox i Android podsystémy. Uvolní procesor od režie virtualizace.",
                Note = "Experimentální: nepoužívej, pokud pracuješ s virtuálními stroji nebo WSL 2. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new CommandOp("hypervisor", CatalogAdvanced.HypervisorState, Shell.System32("bcdedit.exe"), "/set hypervisorlaunchtype off", Shell.System32("bcdedit.exe"), "/set hypervisorlaunchtype auto"),
                    Catalog.Service("HvHost", "demand"),
                    Catalog.Service("vmcompute", "demand"),
                    Catalog.Service("vmms", "demand")
                }
            },
            new()
            {
                Id = "adv-remote-all", Category = CatAdvanced, IsNew = true, Experimental = true,
                Name = "Vypnout všechna vzdálená připojení",
                Description = "Zakáže vzdálenou plochu, vzdálenou pomoc, vzdálenou správu (WinRM), směrování a vzdálený přístup a objevování zařízení v síti (SSDP, UPnP).",
                Note = "Nepůjde se k PC připojit odjinud (ani přes nástroje pro vzdálenou správu). Router může přestat automaticky otevírat porty pro hry.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Terminal Server", "fDenyTSConnections", 1, null),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\Remote Assistance", "fAllowToGetHelp", 0, null),
                    Catalog.Service("WinRM", "demand"),
                    Catalog.Service("RemoteAccess", "demand"),
                    Catalog.Service("SSDPSRV", "demand"),
                    Catalog.Service("upnphost", "demand")
                }
            },
            new()
            {
                Id = "adv-iphone", Category = CatAdvanced, IsNew = true, Experimental = true,
                Name = "Vypnout podporu iPhone",
                Description = "Zakáže služby Apple pro iPhone a iPad (Apple Mobile Device, Bonjour, iPod). Existují jen s nainstalovaným iTunes nebo aplikacemi Apple.",
                Note = "Přestane fungovat připojení iPhonu k PC přes iTunes a Apple zařízení.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("Apple Mobile Device Service", "auto"),
                    Catalog.Service("Bonjour Service", "auto"),
                    Catalog.Service("iPod Service", "demand")
                }
            },
            new()
            {
                Id = "adv-android", Category = CatAdvanced, IsNew = true, Experimental = true,
                Name = "Vypnout podporu Android telefonů",
                Description = "Vypne propojení telefonu s počítačem (Telefon propojený s počítačem, služba telefonu).",
                Note = "Experimentální: přenos souborů kabelem (MTP) funguje dál, vypíná se jen propojení Windows s telefonem.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableMmx", 0, null),
                    Catalog.Service("PhoneSvc", "demand")
                }
            }
        };
    }
}
