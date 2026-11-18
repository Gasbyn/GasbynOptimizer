using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Síť, USB a Bluetooth: úspora energie a nastavení sítě, která většinou jen zlepší odezvu.</summary>
internal static class CatalogNetwork
{
    /// <summary>Vypnuto, když je výběrové pozastavení USB v aktivním schématu nastavené na 0 (střídavé i baterie).</summary>
    internal static OpState UsbSuspendState()
    {
        var result = Shell.Run(Shell.System32("powercfg.exe"),
            "/query SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226", 15000);
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
                Id = "net-adapter-power", Category = Catalog.CatInternet, RequiresRestart = true, Recommended = true,
                Name = "Vypnout úsporu energie síťových karet",
                Description = "Síťová karta (Wi-Fi i kabel) se nebude vypínat kvůli šetření energie. Odstraní to výpadky a zpoždění po chvíli nečinnosti.",
                Note = "Na notebooku na baterii může mírně zvýšit spotřebu. Projeví se po restartu nebo po novém připojení k síti.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("netpower", MultiKeyOp.NetworkAdapters, "PnPCapabilities", RegistryValueKind.DWord, 24, null, false)
                }
            },
            new()
            {
                Id = "net-eee", Category = Catalog.CatInternet, Recommended = true,
                Name = "Vypnout Energy-Efficient Ethernet a úsporu karet",
                Description = "Vypne úsporné režimy síťových karet (Energy-Efficient Ethernet, Green Ethernet, úsporný režim). Stabilnější odezva.",
                Note = "Nastavení existuje jen u karet, které ho podporují. Ostatní se přeskočí.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("eee", MultiKeyOp.NetworkAdapters, "*EEE", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("eee", MultiKeyOp.NetworkAdapters, "EEELinkAdvertisement", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("eee", MultiKeyOp.NetworkAdapters, "AdvancedEEE", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("eee", MultiKeyOp.NetworkAdapters, "GreenEthernet", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("eee", MultiKeyOp.NetworkAdapters, "PowerSavingMode", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("eee", MultiKeyOp.NetworkAdapters, "ULPMode", RegistryValueKind.String, "0", "1", true)
                }
            },
            new()
            {
                Id = "net-tcp-tuning", Category = Catalog.CatInternet, RequiresRestart = true, Recommended = true,
                Name = "Optimalizace TCP/IP",
                Description = "Zkrátí čekání na uvolnění spojení, zvýší počet dostupných portů a zapne zjišťování MTU. Bezpečné nastavení bez nevýhod.",
                Note = "Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "TcpTimedWaitDelay", 30, null),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "MaxUserPort", 65534, null),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "EnablePMTUDiscovery", 1, null),
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "DefaultTTL", 64, null)
                }
            },
            new()
            {
                Id = "net-qos-reserve", Category = Catalog.CatInternet, Recommended = true,
                Name = "Uvolnit rezervovanou šířku pásma",
                Description = "Windows přestane rezervovat část rychlosti internetu pro vlastní účely (QoS).",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, null)
                }
            },
            new()
            {
                Id = "net-netbios-off", Category = Catalog.CatInternet,
                Name = "Vypnout NetBIOS přes TCP/IP",
                Description = "Vypne starý protokol NetBIOS, který se nepoužívá a prozrazuje jméno počítače v síti.",
                Note = "Ve staré domácí síti se mohou přestat zobrazovat počítače v okolí.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("netbios", MultiKeyOp.NetBtInterfaces, "NetbiosOptions", RegistryValueKind.DWord, 2, 0, false)
                }
            },
            new()
            {
                Id = "net-ipv6-off", Category = Catalog.CatInternet, Experimental = true, RequiresRestart = true,
                Name = "Vypnout IPv6",
                Description = "Vypne protokol IPv6 na všech adaptérech.",
                Note = "Experimentální: některé služby (Xbox, část webů, Microsoft Store) na IPv6 spoléhají a mohou zpomalit nebo přestat fungovat. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters", "DisabledComponents", 255, 0)
                }
            },
            new()
            {
                Id = "net-ncsi-off", Category = Catalog.CatInternet, Experimental = true,
                Name = "Vypnout kontrolu připojení k internetu",
                Description = "Windows přestane zkoušet spojení se servery Microsoftu (msftconnecttest), aby zjistil, jestli máš internet.",
                Note = "Experimentální: ikona sítě může ukazovat „bez internetu“, i když internet funguje.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(@"SYSTEM\CurrentControlSet\Services\NlaSvc\Parameters\Internet", "EnableActiveProbing", 0, 1)
                }
            },
            new()
            {
                Id = "net-offload-off", Category = Catalog.CatInternet, Experimental = true,
                Name = "Vypnout Large Send Offload a RSC",
                Description = "Vypne dělení a slučování paketů na síťové kartě. Může snížit skoky odezvy (ping) ve hrách.",
                Note = "Experimentální: u některých karet sníží rychlost stahování nebo zvýší zátěž procesoru.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("lso", MultiKeyOp.NetworkAdapters, "*LsoV2IPv4", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("lso", MultiKeyOp.NetworkAdapters, "*LsoV2IPv6", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("lso", MultiKeyOp.NetworkAdapters, "*RscIPv4", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("lso", MultiKeyOp.NetworkAdapters, "*RscIPv6", RegistryValueKind.String, "0", "1", true)
                }
            },
            new()
            {
                Id = "net-moderation-off", Category = Catalog.CatInternet, Experimental = true,
                Name = "Vypnout moderaci přerušení a řízení toku",
                Description = "Síťová karta zpracovává pakety okamžitě. Nižší zpoždění, vyšší zátěž procesoru.",
                Note = "Experimentální: vyšší zátěž procesoru, u slabších PC to může zpomalit.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("moderation", MultiKeyOp.NetworkAdapters, "*InterruptModeration", RegistryValueKind.String, "0", "1", true),
                    new MultiKeyOp("moderation", MultiKeyOp.NetworkAdapters, "*FlowControl", RegistryValueKind.String, "0", "1", true)
                }
            },
            new()
            {
                Id = "usb-power-saving", Category = CatalogHardware.CatHardware,
                RecommendWhen = p => Needs.Desktop(p) == null,
                Name = "USB: vypnout úsporu energie",
                Description = "Zařízení USB (myš, klávesnice, sluchátka, ovladače) se nebudou uspávat. Odstraní to výpadky a zpoždění po nečinnosti.",
                Note = "Na notebooku na baterii může zvýšit spotřebu. Některá nastavení se projeví po odpojení a připojení zařízení nebo po restartu.",
                Ops = new TweakOp[]
                {
                    new CommandOp("usb-suspend", CatalogNetwork.UsbSuspendState, Shell.System32("cmd.exe"), "/c powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 && powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 && powercfg /setactive SCHEME_CURRENT", Shell.System32("cmd.exe"), "/c powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1 && powercfg /setdcvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1 && powercfg /setactive SCHEME_CURRENT"),
                    new MultiKeyOp("usbdev", MultiKeyOp.UsbDeviceParameters, "EnhancedPowerManagementEnabled", RegistryValueKind.DWord, 0, 1, true),
                    new MultiKeyOp("usbsel", MultiKeyOp.UsbDeviceParameters, "SelectiveSuspendEnabled", RegistryValueKind.DWord, 0, 1, true)
                }
            },
            new()
            {
                Id = "bluetooth-power-saving", Category = CatalogHardware.CatHardware, RequiresRestart = true,
                RecommendWhen = p => Needs.Desktop(p) == null,
                Name = "Bluetooth: vypnout úsporu energie",
                Description = "Bluetooth adaptér se nebude vypínat kvůli šetření energie. Odstraní to výpadky sluchátek a ovladačů.",
                Note = "Dostupné jen s Bluetooth adaptérem. Když je Bluetooth úplně vypnutý nebo chybí, optimalizace se nezobrazí a stačí „USB: vypnout úsporu energie“. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    new MultiKeyOp("btpower", MultiKeyOp.BluetoothAdapters, "PnPCapabilities", RegistryValueKind.DWord, 24, null, false, MultiKeyOp.BluetoothUsable)
                }
            }
        };
    }
}
