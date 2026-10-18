namespace GasbynOptimizer.Core;

/// <summary>Hlubší a rizikovější optimalizace. Všechny jsou experimentální a nedoporučené pro většinu lidí.</summary>
internal static class CatalogAdvanced
{
    internal const string CatAdvanced = "Pokročilé";

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
                Id = "adv-paging-executive", Category = CatAdvanced, IsNew = true, Experimental = true, RequiresRestart = true,
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
                Id = "adv-push-notifications", Category = CatAdvanced, IsNew = true, Experimental = true,
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
                Id = "adv-defender-off", Category = CatAdvanced, IsNew = true, Experimental = true, RequiresRestart = true,
                Name = "Vypnout Microsoft Defender",
                Description = "Vypne antivirus Microsoft Defender a ochranu v reálném čase přes zásady.",
                Note = "Silně nedoporučeno bez jiného antiviru. Při zapnuté ochraně proti neoprávněným změnám (Tamper Protection) Windows změnu nepřijme.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware", 1, null),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", "DisableRealtimeMonitoring", 1, null)
                }
            }
        };
    }
}
