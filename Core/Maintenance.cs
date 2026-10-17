namespace GasbynOptimizer.Core;

public static class Maintenance
{
    /// <summary>Smaže dočasné soubory uživatele a systému. Soubory, které se právě používají, se přeskočí.</summary>
    public static (long Bytes, int Files) CleanTemp()
    {
        long bytes = 0;
        var files = 0;

        var folders = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
        };

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;

            foreach (var path in Directory.EnumerateFiles(folder, "*", options))
            {
                try
                {
                    var info = new FileInfo(path);
                    var length = info.Length;
                    info.Delete();
                    bytes += length;
                    files++;
                }
                catch
                {
                    // Soubor je zamčený nebo chráněný, přeskočíme ho.
                }
            }

            // Prázdné složky mažeme od nejhlubších.
            foreach (var dir in Directory.EnumerateDirectories(folder, "*", options).OrderByDescending(d => d.Length))
            {
                try { Directory.Delete(dir, false); } catch { }
            }
        }

        Log.Write("Vyčištěno dočasných souborů: " + files + " (" + bytes / 1024 / 1024 + " MB)");
        return (bytes, files);
    }

    public static bool FlushDns()
    {
        var result = Shell.Run(Shell.System32("ipconfig.exe"), "/flushdns", 20000);
        return result.ExitCode == 0;
    }
}
