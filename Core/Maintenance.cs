namespace GasbynOptimizer.Core;

public sealed record CleanResult(long Bytes, int Files);

public static class Maintenance
{
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    /// <summary>Smaže obsah složky (soubory i prázdné podsložky). Zamčené nebo chráněné soubory se přeskočí.</summary>
    internal static void DeleteContents(string folder, ref long bytes, ref int files)
    {
        if (!Directory.Exists(folder)) return;

        foreach (var path in Directory.EnumerateFiles(folder, "*", Recursive))
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
        foreach (var dir in Directory.EnumerateDirectories(folder, "*", Recursive).OrderByDescending(d => d.Length))
        {
            try { Directory.Delete(dir, false); } catch { }
        }
    }

    /// <summary>Smaže jeden soubor, pokud existuje.</summary>
    internal static void DeleteFile(string path, ref long bytes, ref int files)
    {
        try
        {
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            var length = info.Length;
            info.Delete();
            bytes += length;
            files++;
        }
        catch
        {
            // Zamčený soubor přeskočíme.
        }
    }

    private static string Env(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);

    /// <summary>Smaže dočasné soubory uživatele a systému.</summary>
    public static CleanResult CleanTemp()
    {
        long bytes = 0;
        var files = 0;

        DeleteContents(Path.GetTempPath(), ref bytes, ref files);
        DeleteContents(Path.Combine(Env(Environment.SpecialFolder.Windows), "Temp"), ref bytes, ref files);

        Log.Write("Vyčištěno dočasných souborů: " + files + " (" + bytes / 1024 / 1024 + " MB)");
        return new CleanResult(bytes, files);
    }

    public static bool FlushDns()
    {
        var result = Shell.Run(Shell.System32("ipconfig.exe"), "/flushdns", 20000);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Smaže mezipaměť (cache) oblíbených aplikací: Discord, Steam, Slack, Teams, Epic Games, Spotify a VS Code.
    /// Mažou se jen složky s mezipamětí. Přihlášení, nastavení a stažené hry ani hudba se nemění.
    /// </summary>
    public static CleanResult CleanAppCaches()
    {
        var roaming = Env(Environment.SpecialFolder.ApplicationData);
        var local = Env(Environment.SpecialFolder.LocalApplicationData);

        var folders = new List<string>();

        void Electron(string root)
        {
            foreach (var name in new[] { "Cache", "Code Cache", "GPUCache", "DawnCache" })
                folders.Add(Path.Combine(root, name));
        }

        Electron(Path.Combine(roaming, "discord"));
        Electron(Path.Combine(roaming, "Slack"));
        Electron(Path.Combine(roaming, "Code"));
        folders.Add(Path.Combine(roaming, "Code", "CachedData"));

        foreach (var name in new[] { "Cache", "blob_storage", "Code Cache", "GPUCache", "tmp" })
            folders.Add(Path.Combine(roaming, "Microsoft", "Teams", name));

        folders.Add(Path.Combine(local, "Steam", "htmlcache"));
        folders.Add(Path.Combine(local, "Spotify", "Browser"));

        var epicSaved = Path.Combine(local, "EpicGamesLauncher", "Saved");
        try
        {
            if (Directory.Exists(epicSaved))
                folders.AddRange(Directory.EnumerateDirectories(epicSaved, "webcache*"));
        }
        catch
        {
            // Bez přístupu přeskočíme.
        }

        long bytes = 0;
        var files = 0;
        foreach (var folder in folders)
            DeleteContents(folder, ref bytes, ref files);

        Log.Write("Vyčištěna mezipaměť aplikací: " + files + " souborů (" + bytes / 1024 / 1024 + " MB)");
        return new CleanResult(bytes, files);
    }
}
