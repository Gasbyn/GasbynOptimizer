namespace GasbynOptimizer.Core;

/// <summary>Optimalizace Wi-Fi. Dostupné jen na počítači s Wi-Fi adaptérem (jinak jsou nepodporované).</summary>
internal static class CatalogWifi
{
    internal const string CatWifi = "Wi-Fi";

    private const string PowerPolicy = @"SOFTWARE\Policies\Microsoft\Power\PowerSettings\";
    private const string WcmConfig = @"SOFTWARE\Microsoft\WcmSvc\wifinetworksmanager\config";
    private const string WcmPolicy = @"SOFTWARE\Policies\Microsoft\Windows\WcmSvc\GroupPolicy";

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "wifi-power-saving", Category = CatWifi, IsNew = true, Requires = Needs.Wifi, Recommended = true,
                Name = "Wi-Fi: vypnout úsporný režim adaptéru",
                Description = "Wi-Fi adaptér poběží na síťovém napájení v režimu maximálního výkonu. Méně výpadků, nižší odezva a stabilnější signál.",
                Note = "Platí jen na síťovém napájení, na baterii notebooku se nic nemění. Doplňuje vypnutí šetření energie síťové karty v Síti.",
                Ops = new TweakOp[]
                {
                    // Plán napájení: Nastavení bezdrátového adaptéru, Úsporný režim, 0 = maximální výkon
                    Catalog.Hklm(PowerPolicy + @"19cbb8fa-5279-450e-9fac-8a3d5fedd0c1\12bbebe6-58d6-4636-95bb-3217ef867c1a", "ACSettingIndex", 0, null)
                }
            },
            new()
            {
                Id = "wifi-suggested-hotspots", Category = CatWifi, IsNew = true, Requires = Needs.Wifi, Recommended = true,
                Name = "Wi-Fi: nepřipojovat se k doporučeným otevřeným sítím",
                Description = "Windows se přestane samo připojovat k otevřeným Wi-Fi hotspotům, které sám doporučí. Bezpečnější a soukromější.",
                Ops = new TweakOp[]
                {
                    Catalog.Hklm(WcmConfig, "AutoConnectAllowedOEM", 0, null)
                }
            },
            new()
            {
                Id = "wifi-prefer-cable", Category = CatWifi, IsNew = true, Requires = Needs.Wifi,
                Name = "Wi-Fi: vypnout Wi-Fi, když je připojený kabel",
                Description = "Když je připojený síťový kabel, Windows odpojí Wi-Fi a používá jen kabel. Internet je stabilnější a nepřepíná se mezi dvěma sítěmi.",
                Note = "Hodí se na počítač nebo notebook, který používáš s kabelem i bez něj. Po odpojení kabelu se Wi-Fi připojí samo. Zásada Windows, může ji přepsat správce sítě.",
                Ops = new TweakOp[]
                {
                    // 3 = při připojení kabelu zabránit připojení k Wi-Fi
                    Catalog.Hklm(WcmPolicy, "fMinimizeConnections", 3, null)
                }
            },
            new()
            {
                Id = "wifi-hotspot-services", Category = CatWifi, IsNew = true, Requires = Needs.Wifi,
                Name = "Wi-Fi: vypnout mobilní hotspot a Wi-Fi Direct",
                Description = "Vypne služby sdílení internetu přes Wi-Fi a Wi-Fi Direct. Méně procesů na pozadí, samotné Wi-Fi zůstane funkční.",
                Note = "Nepůjde sdílet internet z počítače jako hotspot ani posílat soubory přes Wi-Fi Direct. Úplné vypnutí Wi-Fi je v Pokročilých.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("icssvc", "demand"),
                    Catalog.Service("WFDSConMgrSvc", "demand")
                }
            }
        };
    }
}
