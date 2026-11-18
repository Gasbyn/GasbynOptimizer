using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>
/// Souhlas s podmínkami se pamatuje na dvou místech: v settings.json a v registru (HKLM, při potížích HKCU).
/// Za odsouhlasené se podmínky berou, jen když sedí obě místa. Smazání jedné z nich (nová instalace, odinstalace,
/// smazání nastavení) tak vede k tomu, že se potvrzovací okno ukáže znovu.
/// </summary>
public static class TermsAcceptance
{
    private const string KeyPath = @"Software\GasbynOptimizer";
    private const string ValueName = "AcceptedTermsVersion";

    private static int ReadRegistry()
    {
        var best = 0;
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var key = root.OpenSubKey(KeyPath, false);
                if (key?.GetValue(ValueName) is int value && value > best) best = value;
            }
            catch { /* bez práv se bere jako nezapsáno */ }
        }
        return best;
    }

    public static bool IsAccepted(AppSettings settings, int requiredVersion) =>
        settings.AcceptedTermsVersion >= requiredVersion && ReadRegistry() >= requiredVersion;

    public static void Save(AppSettings settings, int version)
    {
        settings.AcceptedTermsVersion = version;
        settings.Save();

        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(KeyPath, true);
            key.SetValue(ValueName, version, RegistryValueKind.DWord);
        }
        catch
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(KeyPath, true);
                key.SetValue(ValueName, version, RegistryValueKind.DWord);
            }
            catch (Exception ex) { Log.Write("Souhlas s podmínkami se nepodařilo zapsat do registru: " + ex.Message); }
        }
    }
}
