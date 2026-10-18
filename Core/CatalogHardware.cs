namespace GasbynOptimizer.Core;

/// <summary>Optimalizace pro konkrétní hardware a verzi Windows. Podle zjištěného systému se označí jako podporované či ne.</summary>
internal static class CatalogHardware
{
    internal const string CatHardware = "Hardware a zařízení";

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "nvidia-telemetry", Category = CatHardware, IsNew = true, Experimental = true,
                Requires = Needs.Nvidia,
                RecommendWhen = p => p.HasGpu(GpuVendor.Nvidia),
                Name = "NVIDIA: vypnout telemetrii ovladače",
                Description = "Vypne odesílání telemetrie NVIDIA (nastavení v registru, telemetrické úlohy a starší telemetrickou službu). Ovladač a GeForce Experience fungují dál.",
                Note = "Experimentální: klíče registru pocházejí z veřejných návodů. Novější ovladače starou službu a úlohy už nemají, ty se pak přeskočí. Vypnutí vrátí původní stav.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\NVIDIA Corporation\NvControlPanel2\Client", "OptInOrOutPreference", 0, null),
                    Catalog.Hklm(@"SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableRID44231", 0, null),
                    Catalog.Hklm(@"SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableRID64640", 0, null),
                    Catalog.Hklm(@"SOFTWARE\NVIDIA Corporation\Global\FTS", "EnableRID66610", 0, null),
                    Catalog.Service("NvTelemetryContainer", "auto"),
                    Catalog.SchedTask(@"\NvTmRep_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}"),
                    Catalog.SchedTask(@"\NvTmMon_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}"),
                    Catalog.SchedTask(@"\NvTmRepOnLogon_{B2FE1952-0186-46C3-BAEC-A80AA35AC5B8}")
                }
            },
            new()
            {
                Id = "amd-crash-defender", Category = CatHardware, IsNew = true, Experimental = true,
                Requires = Needs.AmdGpu,
                Name = "AMD: vypnout službu Crash Defender",
                Description = "Zakáže službu AMD Crash Defender, která sbírá data o pádech ovladače.",
                Note = "Experimentální: ovladač pak neodesílá hlášení o pádech. Pokud službu ovladač nemá, optimalizace je nedostupná.",
                Ops = new TweakOp[] { Catalog.Service("AMD Crash Defender Service", "auto") }
            },
            new()
            {
                Id = "intel-usage-report", Category = CatHardware, IsNew = true,
                Requires = Needs.IntelCpu,
                RecommendWhen = p => p.CpuVendor == "Intel",
                Name = "Intel: vypnout hlášení o používání",
                Description = "Zakáže službu System Usage Report od Intelu, která odesílá statistiky o používání počítače.",
                Note = "Služba existuje jen tam, kde je nainstalovaný Intel Driver & Support Assistant nebo podobný software Intelu.",
                Ops = new TweakOp[] { Catalog.Service("SystemUsageReportSvc_QUEENCREEK", "auto") }
            },
            new()
            {
                Id = "asus-armoury", Category = CatHardware, IsNew = true, Experimental = true,
                Requires = Needs.Asus,
                Name = "ASUS: vypnout služby Armoury Crate a ASUS aplikací",
                Description = "Zakáže služby na pozadí od ASUS (Armoury Crate, ASUS Link, diagnostika). Šetří paměť a procesor.",
                Note = "Experimentální: přestane fungovat ovládání osvětlení (Aura) a profilů přes Armoury Crate. Nenalezené služby se přeskočí.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("ArmouryCrate.Service", "auto"),
                    Catalog.Service("AsusAppService", "auto"),
                    Catalog.Service("ASUSLinkNear", "auto"),
                    Catalog.Service("ASUSLinkRemote", "auto"),
                    Catalog.Service("ASUSSoftwareManager", "auto"),
                    Catalog.Service("ASUSSwitch", "auto"),
                    Catalog.Service("ASUSSystemAnalysis", "auto"),
                    Catalog.Service("ASUSSystemDiagnosis", "auto"),
                    Catalog.Service("asComSvc", "auto"),
                    Catalog.Service("AsusCertService", "auto")
                }
            },
            new()
            {
                Id = "msi-center", Category = CatHardware, IsNew = true, Experimental = true,
                Requires = Needs.Msi,
                Name = "MSI: vypnout služby MSI Center",
                Description = "Zakáže služby na pozadí od MSI Center. Šetří paměť a procesor.",
                Note = "Experimentální: přestane fungovat ovládání ventilátorů, osvětlení a profilů přes MSI Center. Nenalezené služby se přeskočí.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("MSI Foundation Service", "auto"),
                    Catalog.Service("MSI_Central_Service", "auto")
                }
            },
            new()
            {
                Id = "win10-news", Category = Catalog.CatUi, IsNew = true,
                Requires = Needs.Windows10, RecommendWhen = p => !p.IsWindows11,
                RequiresRestart = false,
                Name = "Windows 10: vypnout Zpravodajství na hlavním panelu",
                Description = "Z hlavního panelu zmizí widget s počasím a zprávami, který běží na pozadí.",
                Ops = new TweakOp[]
                {
                    Catalog.Hkcu(@"Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 2, 0)
                }
            }
        };
    }
}
