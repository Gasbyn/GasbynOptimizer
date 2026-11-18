using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Optimalizace grafiky: NVIDIA, AMD a obecné nastavení grafických ovladačů a sběrnice PCIe.</summary>
internal static class CatalogGpu
{
    /// <summary>Hodnota je nastavená na 0 pro síť i baterii v aktivním plánu napájení.</summary>
    internal static OpState PowerCfgIsZero(string subgroup, string setting)
    {
        var result = Shell.Run(Shell.System32("powercfg.exe"), "/query SCHEME_CURRENT " + subgroup + " " + setting, 15000);
        if (result.ExitCode != 0) return OpState.Missing;

        // Výstup je lokalizovaný, proto se čtou jen šestnáctkové hodnoty. Poslední dvě jsou index pro síť a pro baterii.
        var values = System.Text.RegularExpressions.Regex.Matches(result.Output, "0x[0-9a-fA-F]{8}")
            .Select(m => Convert.ToUInt32(m.Value, 16)).ToList();
        if (values.Count < 2) return OpState.Off;
        return values[^1] == 0 && values[^2] == 0 ? OpState.On : OpState.Off;
    }

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "nv-max-performance", Category = CatalogHardware.CatHardware, Experimental = true, RequiresRestart = true,
                Requires = Needs.Nvidia, RecommendWhen = p => false,
                Name = "NVIDIA: maximální výkon grafiky (PowerMizer)",
                Description = "Nastaví ovladač NVIDIA na trvalý maximální výkon. Grafika se při hraní nepřepíná do úsporných stavů, takže jsou menší výkyvy FPS.",
                Note = "Experimentální: vyšší spotřeba, teplota a hluk i na ploše Windows. Na notebooku na baterii nepoužívej. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "PerfLevelSrc", RegistryValueKind.DWord, 8738, null, false),
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "PowerMizerEnable", RegistryValueKind.DWord, 1, null, false),
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "PowerMizerLevel", RegistryValueKind.DWord, 1, null, false),
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "PowerMizerLevelAC", RegistryValueKind.DWord, 1, null, false)
                }
            },
            new()
            {
                Id = "nv-no-dynamic-pstate", Category = CatalogHardware.CatHardware, Experimental = true, RequiresRestart = true,
                Requires = Needs.Nvidia, RecommendWhen = p => false,
                Name = "NVIDIA: vypnout dynamické přepínání stavů (P-state)",
                Description = "Grafika NVIDIA zůstane v nejvyšším výkonovém stavu a nepřepíná se podle zátěže.",
                Note = "Experimentální: grafika stále běží na vysokých taktech, tedy vyšší spotřeba a teplota. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "DisableDynamicPstate", RegistryValueKind.DWord, 1, null, false)
                }
            },
            new()
            {
                Id = "nv-deep-l1-off", Category = CatalogHardware.CatHardware, Experimental = true, RequiresRestart = true,
                Requires = Needs.Nvidia, RecommendWhen = p => false,
                Name = "NVIDIA: vypnout úsporu PCIe (Deep L1)",
                Description = "Vypne hluboký úsporný stav sběrnice PCIe u grafiky NVIDIA. Menší zpoždění při probouzení grafiky ze spánku.",
                Note = "Experimentální: mírně vyšší spotřeba v nečinnosti. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "RMDeepL1EntryLatencyUsec", RegistryValueKind.DWord, 1, null, false)
                }
            },
            new()
            {
                Id = "nv-hdcp-off", Category = CatalogHardware.CatHardware, Experimental = true, RequiresRestart = true,
                Requires = Needs.Nvidia, RecommendWhen = p => false,
                Name = "NVIDIA: vypnout HDCP",
                Description = "Vypne ochranu obsahu HDCP v ovladači NVIDIA. Odstraní drobné zpoždění a problémy s některými monitory a záznamovými kartami.",
                Note = "Experimentální: přestane fungovat přehrávání chráněného obsahu ve vysokém rozlišení (například Netflix 4K) a některé výstupy. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("gpu-nvidia", () => MultiKeyOp.GpuAdapters("NVIDIA"), "RMHdcpKeyglobZero", RegistryValueKind.DWord, 1, null, false)
                }
            },
            new()
            {
                Id = "nv-gfe-services", Category = CatalogHardware.CatHardware, Experimental = true,
                Requires = Needs.Nvidia, RecommendWhen = p => false,
                Name = "NVIDIA: vypnout služby GeForce Experience a Shield",
                Description = "Zakáže služby GeForce Experience (GfExperienceService, NvContainerLocalSystem, NvBroadcast) a GameStream. Šetří paměť a procesor.",
                Note = "Experimentální: přestanou fungovat ShadowPlay, nahrávání, overlay a Instant Replay. Ovladač a Ovládací panel NVIDIA fungují dál. Nenalezené služby se přeskočí.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("GfExperienceService", "auto"),
                    Catalog.Service("NvContainerLocalSystem", "auto"),
                    Catalog.Service("NvBroadcast.ContainerLocalSystem", "demand"),
                    Catalog.Service("NvStreamSvc", "demand"),
                    Catalog.Service("NvStreamNetworkSvc", "demand")
                }
            },
            new()
            {
                Id = "nv-gfe-startup", Category = CatalogHardware.CatHardware,
                Requires = Needs.Nvidia, RecommendWhen = p => false,
                Name = "NVIDIA: nespouštět GeForce Experience se systémem",
                Description = "Program NVIDIA Backend (GeForce Experience) se přestane spouštět po startu Windows.",
                Note = "Dostupné, jen když GeForce Experience má položku automatického spuštění.",
                Ops = new TweakOp[]
                {
                    new StartupEntryOp("NvBackend")
                }
            },
            new()
            {
                Id = "gpu-tdr-delay", Category = CatalogHardware.CatHardware, RequiresRestart = true, Recommended = true,
                Name = "GPU: prodloužit časový limit ovladače (TDR)",
                Description = "Prodlouží čas, po kterém Windows restartuje ovladač grafiky při velké zátěži. Odstraní hlášení „Ovladač přestal reagovat“ při těžších hrách a renderování.",
                Note = "Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay", 10, null),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDdiDelay", 10, null)
                }
            },
            new()
            {
                Id = "gpu-mpo-off", Category = CatalogHardware.CatHardware, Experimental = true, RequiresRestart = true,
                Name = "GPU: vypnout Multiplane Overlay (MPO)",
                Description = "Vypne vykreslování po vrstvách (MPO) v okně Windows. Řeší záseky, blikání a černé obrazovky u grafik NVIDIA a AMD na některých ovladačích.",
                Note = "Experimentální: u některých her a monitorů se nemusí nic změnit. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5, null)
                }
            },
            new()
            {
                Id = "gpu-pcie-link", Category = CatalogHardware.CatHardware,
                RecommendWhen = p => Needs.Desktop(p) == null,
                Name = "GPU: vypnout úsporu energie sběrnice PCIe",
                Description = "Vypne úsporný stav sběrnice PCIe (Link State Power Management) v plánu napájení. Grafika a disky NVMe se nepřepínají do spánku, takže jsou rychlejší odezvy.",
                Note = "Na notebooku na baterii zvyšuje spotřebu. Vypnutí nastaví střední úsporný režim.",
                Ops = new TweakOp[]
                {
                    new CommandOp("pcie-aspm", () => CatalogGpu.PowerCfgIsZero("501a4d13-42af-4429-9fd1-a8218c268e20", "ee12f906-d277-404b-b6da-e5fa1a576df5"), Shell.System32("cmd.exe"), "/c powercfg /setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0 && powercfg /setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0 && powercfg /setactive SCHEME_CURRENT", Shell.System32("cmd.exe"), "/c powercfg /setacvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1 && powercfg /setdcvalueindex SCHEME_CURRENT 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1 && powercfg /setactive SCHEME_CURRENT")
                }
            },
            new()
            {
                Id = "amd-ulps-off", Category = CatalogHardware.CatHardware, Experimental = true, RequiresRestart = true,
                Requires = Needs.AmdGpu, RecommendWhen = p => false,
                Name = "AMD: vypnout úsporný stav ULPS",
                Description = "Vypne Ultra Low Power State u grafiky AMD. Odstraní černé obrazovky a výpadky obrazu po probuzení a u více monitorů.",
                Note = "Experimentální: mírně vyšší spotřeba v nečinnosti. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("gpu-amd", () => MultiKeyOp.GpuAdapters("AMD"), "EnableUlps", RegistryValueKind.DWord, 0, 1, false)
                }
            },
            new()
            {
                Id = "amd-external-events", Category = CatalogHardware.CatHardware, Experimental = true,
                Requires = Needs.AmdGpu, RecommendWhen = p => false,
                Name = "AMD: vypnout službu External Events Utility",
                Description = "Zakáže službu AMD External Events Utility, která na pozadí hlídá události ovladače. Šetří paměť.",
                Note = "Experimentální: může se rozbít přepínání grafiky na některých noteboocích. Nenalezená služba se přeskočí.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("AMD External Events Utility", "auto")
                }
            }
        };
    }
}
