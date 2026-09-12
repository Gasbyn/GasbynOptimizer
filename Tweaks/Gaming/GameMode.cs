using GasbynOptimizer.Models;
using Microsoft.Win32;

namespace GasbynOptimizer.Tweaks.Gaming;

public static class GameMode
{
    public static Tweak Create() => new()
    {
        Id = "game-mode",
        Name = "Windows Game Mode",
        Description = "Zjistí aktuální stav Game Mode bez provedení systémové změny.",
        Category = "Gaming",
        Tier = Tier.Free,
        Apply = _ => TweakResult.Fail("Změna Game Mode zatím není povolena. Tato první verze je pouze scan-only."),
        Restore = _ => TweakResult.Fail("Obnova Game Mode zatím není povolena. Tato první verze je pouze scan-only."),
        GetStatus = _ => GetCurrentStatus()
    };

    private static TweakStatus GetCurrentStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar", false);
            var value = key?.GetValue("AllowAutoGameMode");

            if (value is int number)
                return number == 1 ? TweakStatus.Enabled : TweakStatus.Disabled;

            return TweakStatus.NotConfigured;
        }
        catch
        {
            return TweakStatus.Unknown;
        }
    }
}
