using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Zjištění, jestli je aplikace na počítači nainstalovaná (před optimalizacemi v kategorii Debloat).</summary>
internal static class AppPresence
{
    private static bool AppPath(string exe)
    {
        foreach (var hklm in new[] { true, false })
        foreach (var sub in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths\" })
        {
            try
            {
                using var root = Reg.Base(hklm);
                using var key = root.OpenSubKey(sub + exe, false);
                if (key != null) return true;
            }
            catch { /* klíč nejde číst, bere se jako nenalezeno */ }
        }
        return false;
    }

    /// <summary>Nainstalováno, když existuje položka App Paths pro daný program nebo některý ze souborů.</summary>
    public static bool ByExe(string exe, params string[] files)
    {
        if (AppPath(exe)) return true;
        foreach (var file in files)
        {
            try { if (File.Exists(Environment.ExpandEnvironmentVariables(file))) return true; }
            catch { }
        }
        return false;
    }
}
