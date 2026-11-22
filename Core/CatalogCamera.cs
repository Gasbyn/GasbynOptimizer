namespace GasbynOptimizer.Core;

/// <summary>Kamera: lehčí varianta (zakázat přístup aplikacím) a úplné vypnutí podpory kamer.</summary>
internal static class CatalogCamera
{
    private const string Consent = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    public static IReadOnlyList<Tweak> Create()
    {
        return new List<Tweak>
        {
            new()
            {
                Id = "camera-access-deny", Category = Catalog.CatPrivacy, IsNew = true,
                Name = "Kamera: zakázat aplikacím přístup",
                Description = "Aplikace a weby nedostanou přístup ke kameře, ale kamera zůstane ve Windows a jde kdykoli povolit zpět. Vhodné, když kameru nepoužíváš nebo chceš jen soukromí.",
                Note = "Lehčí varianta. Kamera nepůjde použít v Discordu, Zoomu ani Teams, dokud tuhle úpravu nevypneš.",
                Ops = new TweakOp[]
                {
                    Catalog.HklmStr(Consent, "Value", "Deny", "Allow"),
                    Catalog.HkcuStr(Consent, "Value", "Deny", "Allow")
                }
            },
            new()
            {
                Id = "camera-support-off", Category = Catalog.CatServices, IsNew = true, Experimental = true, RequiresRestart = true,
                Name = "Kamera: vypnout podporu kamer",
                Description = "Vypne službu sdílení kamery (Windows Camera Frame Server) a zakáže kamery ve Windows. Méně procesů na pozadí a žádný přístup ke kameře.",
                Note = "Experimentální: kamera přestane fungovat ve všech programech (Discord, Zoom, OBS, Teams). Pokud ji chceš zachovat a jen omezit aplikace, zvol „Zakázat aplikacím přístup“. Vyžaduje restart.",
                Ops = new TweakOp[]
                {
                    Catalog.Service("FrameServer", "demand"),
                    Catalog.Service("FrameServerMonitor", "demand"),
                    Catalog.Hklm(@"SOFTWARE\Policies\Microsoft\Camera", "AllowCamera", 0, null)
                }
            }
        };
    }
}
