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

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 4)]
    private struct ShQueryRbInfo
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? rootPath, ref ShQueryRbInfo info);

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? rootPath, uint flags);

    /// <summary>Vysype koš na všech discích bez potvrzení. Vrací velikost a počet smazaných položek.</summary>
    private static void EmptyRecycleBin(ref long bytes, ref int files)
    {
        try
        {
            var info = new ShQueryRbInfo { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<ShQueryRbInfo>() };
            if (SHQueryRecycleBin(null, ref info) != 0 || info.i64NumItems == 0) return;

            // 0x7 = bez potvrzení, bez okna s průběhem, bez zvuku.
            if (SHEmptyRecycleBin(IntPtr.Zero, null, 0x7) == 0)
            {
                bytes += info.i64Size;
                files += (int)Math.Min(info.i64NumItems, int.MaxValue);
            }
        }
        catch
        {
            // Koš se nepodařilo vysypat, pokračuje se dál.
        }
    }

    private static string Env(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);

    /// <summary>Smaže dočasné soubory uživatele a systému.</summary>
    public static CleanResult CleanTemp()
    {
        long bytes = 0;
        var files = 0;

        DeleteContents(Path.GetTempPath(), ref bytes, ref files);
        var windows = Env(Environment.SpecialFolder.Windows);
        var local = Env(Environment.SpecialFolder.LocalApplicationData);
        var programData = Env(Environment.SpecialFolder.CommonApplicationData);

        DeleteContents(Path.Combine(windows, "Temp"), ref bytes, ref files);

        // Hlášení o chybách, výpisy pádů, protokoly a stažené soubory Windows Update.
        DeleteContents(Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"), ref bytes, ref files);
        DeleteContents(Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"), ref bytes, ref files);
        DeleteContents(Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"), ref bytes, ref files);
        DeleteContents(Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue"), ref bytes, ref files);
        DeleteContents(Path.Combine(local, "CrashDumps"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "Minidump"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "Logs"), ref bytes, ref files);
        DeleteFile(Path.Combine(windows, "MEMORY.DMP"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "SoftwareDistribution", "Download"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache"), ref bytes, ref files);

        // Windows Update: protokoly, zbytky instalací a dávky odinstalace.
        DeleteContents(Path.Combine(windows, "Logs", "CBS"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "Logs", "WindowsUpdate"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "SoftwareDistribution", "DeliveryOptimization"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "SoftwareDistribution", "DataStore", "Logs"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "Temp", "CBS"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "$WINDOWS.~BT", "Sources", "Panther"), ref bytes, ref files);

        // Optimalizace doručování (Delivery Optimization).
        DeleteContents(Path.Combine(programData, "Microsoft", "Windows", "DeliveryOptimization", "Cache"), ref bytes, ref files);
        DeleteContents(Path.Combine(programData, "Microsoft", "Windows", "DeliveryOptimization", "Logs"), ref bytes, ref files);

        // Microsoft Defender Antivirus: dočasné soubory kontrol, protokoly a místní kopie.
        var defender = Path.Combine(programData, "Microsoft", "Windows Defender");
        DeleteContents(Path.Combine(defender, "Scans", "History", "Results"), ref bytes, ref files);
        DeleteContents(Path.Combine(defender, "Scans", "History", "CacheManager"), ref bytes, ref files);
        DeleteContents(Path.Combine(defender, "Scans", "History", "ReportLatency"), ref bytes, ref files);
        DeleteContents(Path.Combine(defender, "Support"), ref bytes, ref files);
        DeleteContents(Path.Combine(defender, "LocalCopy"), ref bytes, ref files);

        // Dočasné internetové soubory.
        DeleteContents(Path.Combine(local, "Microsoft", "Windows", "INetCache"), ref bytes, ref files);
        DeleteContents(Path.Combine(local, "Microsoft", "Windows", "Temporary Internet Files"), ref bytes, ref files);
        DeleteContents(Path.Combine(windows, "ServiceProfiles", "LocalService", "AppData", "Local", "Microsoft", "Windows", "INetCache"), ref bytes, ref files);

        // Mezipaměť shaderů DirectX (hry ji znovu vytvoří, první spuštění může chvíli trvat).
        DeleteContents(Path.Combine(local, "D3DSCache"), ref bytes, ref files);
        DeleteContents(Path.Combine(local, "Microsoft", "DirectX Shader Cache"), ref bytes, ref files);

        // Koš na všech discích.
        EmptyRecycleBin(ref bytes, ref files);

        // Náhledy obrázků v Průzkumníku (vytvoří se znovu).
        var explorer = Path.Combine(local, "Microsoft", "Windows", "Explorer");
        try
        {
            if (Directory.Exists(explorer))
                foreach (var file in Directory.EnumerateFiles(explorer, "thumbcache_*.db").Concat(Directory.EnumerateFiles(explorer, "iconcache_*.db")))
                    DeleteFile(file, ref bytes, ref files);
        }
        catch { /* bez přístupu přeskočíme */ }

        Log.Write("Vyčištěno dočasných souborů: " + files + " (" + bytes / 1024 / 1024 + " MB)");
        return new CleanResult(bytes, files);
    }

    public sealed record DeepCleanResult(long Bytes, string Summary);

    private static long SystemFreeBytes()
    {
        try { return new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!).AvailableFreeSpace; }
        catch { return 0; }
    }

    private static string Encode(string script) => Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

    /// <summary>
    /// Hloubkové čištění jako ve Vyčištění disku: staré soubory Windows Update (úklid součástí systému), staré balíčky ovladačů a nepoužívané jazykové balíčky.
    /// Uvolněné místo se zjišťuje z volného místa na systémovém disku.
    /// </summary>
    public static DeepCleanResult DeepClean(bool components, bool drivers, bool languages)
    {
        var start = SystemFreeBytes();
        var notes = new List<string>();

        if (components)
        {
            // Úklid úložiště součástí (WinSxS): nahrazené aktualizace Windows Update. Bez ResetBase, aby šlo aktualizace dál odinstalovat.
            var r = Shell.Run(Shell.System32("dism.exe"), "/Online /Cleanup-Image /StartComponentCleanup", 1800000);
            notes.Add(r.ExitCode == 0 ? "úklid Windows Update hotov" : "úklid Windows Update skončil s kódem " + r.ExitCode);
        }

        if (drivers)
        {
            // Smaže jen starší verze stejného ovladače. Ovladač, který se používá, odinstalace odmítne (bez přepínače force).
            const string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$all = Get-WindowsDriver -Online -All | Where-Object { $_.OriginalFileName }
$removed = 0
foreach ($g in ($all | Group-Object { (Split-Path $_.OriginalFileName -Leaf).ToLower() + '|' + $_.ProviderName + '|' + $_.ClassName })) {
  if ($g.Count -lt 2) { continue }
  $sorted = $g.Group | Sort-Object @{ Expression = { [version]$_.Version }; Descending = $true }, Date -Descending
  foreach ($old in ($sorted | Select-Object -Skip 1)) {
    pnputil.exe /delete-driver $old.Driver | Out-Null
    if ($LASTEXITCODE -eq 0) { $removed++ }
  }
}
Write-Output $removed
";
            var r = Shell.Run(Shell.PowerShellExe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Encode(script), 900000);
            var last = r.Output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
            notes.Add(int.TryParse(last, out var n) ? "odstraněno starých ovladačů: " + n : "ovladače se nepodařilo zkontrolovat");
        }

        if (languages)
        {
            // Odebere jen jazyky, které nejsou v seznamu jazyků uživatele, ani jazykem systému a rozhraní.
            const string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$keep = @()
$keep += (Get-WinUserLanguageList).LanguageTag
$keep += (Get-WinSystemLocale).Name
$keep += (Get-UICulture).Name
$keep += 'en-US'
$keep = $keep | ForEach-Object { $_.ToLower() }
$removed = 0
foreach ($l in (Get-InstalledLanguage)) {
  $id = $l.LanguageId.ToLower()
  $base = $id.Split('-')[0]
  if ($keep -contains $id -or ($keep | Where-Object { $_.Split('-')[0] -eq $base })) { continue }
  Uninstall-Language -Language $l.LanguageId | Out-Null
  $removed++
}
Write-Output $removed
";
            var r = Shell.Run(Shell.PowerShellExe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Encode(script), 900000);
            var last = r.Output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
            notes.Add(int.TryParse(last, out var n) ? "odebráno nepoužívaných jazyků: " + n : "jazyky se nepodařilo zkontrolovat");
        }

        var freed = Math.Max(0, SystemFreeBytes() - start);
        Log.Write("Hloubkové čištění: " + string.Join(", ", notes) + " (" + freed / 1024 / 1024 + " MB)");
        return new DeepCleanResult(freed, string.Join(", ", notes));
    }

    public static bool FlushDns()
    {
        var result = Shell.Run(Shell.System32("ipconfig.exe"), "/flushdns", 20000);
        return result.ExitCode == 0;
    }

    /// <summary>
    /// Smaže mezipaměť (cache) oblíbených aplikací a her: Discord, Steam, Slack, Teams, Epic Games, Fortnite, Ubisoft, Battle.net, EA, GOG, Spotify, VS Code, OBS a shadery grafických karet.
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

        // Mezipaměť shaderů grafických karet a DirectX (hry ji při dalším spuštění znovu vytvoří, první start může být chvíli pomalejší).
        folders.Add(Path.Combine(local, "D3DSCache"));
        folders.Add(Path.Combine(local, "NVIDIA", "DXCache"));
        folders.Add(Path.Combine(local, "NVIDIA", "GLCache"));
        folders.Add(Path.Combine(local, "NVIDIA Corporation", "NV_Cache"));
        folders.Add(Path.Combine(local, "AMD", "DxCache"));
        folders.Add(Path.Combine(local, "AMD", "GLCache"));
        folders.Add(Path.Combine(local, "Intel", "ShaderCache"));

        // Hry a herní launchery.
        folders.Add(Path.Combine(local, "FortniteGame", "Saved", "ShaderCache"));
        folders.Add(Path.Combine(local, "FortniteGame", "Saved", "Logs"));
        folders.Add(Path.Combine(local, "EpicGamesLauncher", "Saved", "Logs"));
        folders.Add(Path.Combine(local, "Ubisoft Game Launcher", "cache"));
        folders.Add(Path.Combine(local, "Battle.net", "Cache"));
        folders.Add(Path.Combine(local, "Electronic Arts", "EA Desktop", "Cache"));
        folders.Add(Path.Combine(local, "GOG.com", "Galaxy", "webcache"));
        folders.Add(Path.Combine(local, "Riot Games", "Riot Client", "Logs"));
        folders.Add(Path.Combine(roaming, ".minecraft", "logs"));

        // OBS Studio: protokoly, hlášení o pádech a stažené aktualizace.
        folders.Add(Path.Combine(roaming, "obs-studio", "logs"));
        folders.Add(Path.Combine(roaming, "obs-studio", "crashes"));
        folders.Add(Path.Combine(roaming, "obs-studio", "updates"));

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

    public sealed record LeftoverResult(int UninstallEntries, int PackageFolders, long Bytes, string? BackupFile);

    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallPath32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    /// <summary>Vytáhne cestu k programu z řetězce odinstalace (v uvozovkách, nebo do přípony .exe).</summary>
    internal static string? ExecutableFrom(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();

        string path;
        if (command[0] == '"')
        {
            var end = command.IndexOf('"', 1);
            if (end < 0) return null;
            path = command.Substring(1, end - 1);
        }
        else
        {
            var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe < 0) return null;
            path = command.Substring(0, exe + 4);
        }

        path = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(path) ? path : null;
    }

    private static bool PathExistsOrUnknown(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            var comma = path.LastIndexOf(',');
            if (comma > 2) path = path.Substring(0, comma);
            if (!Path.IsPathRooted(path)) return true;           // neznámý tvar, raději nemazat
            var root = Path.GetPathRoot(path);
            if (root == null || !Directory.Exists(root)) return true; // disk není připojený, nelze rozhodnout
            return File.Exists(path) || Directory.Exists(path);
        }
        catch { return true; }
    }

    /// <summary>Záznamy v „Programy a funkce“, jejichž program už neexistuje. Před smazáním se zálohují do souboru .reg.</summary>
    private static int CleanStaleUninstallEntries(out string? backupFile)
    {
        backupFile = null;
        var removed = 0;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var sources = new[] { (true, UninstallPath), (true, UninstallPath32), (false, UninstallPath) };

        foreach (var (hklm, path) in sources)
        {
            var stale = new List<string>();
            try
            {
                using var root = Reg.Base(hklm);
                using var parent = root.OpenSubKey(path, false);
                if (parent == null) continue;

                foreach (var name in parent.GetSubKeyNames())
                {
                    using var key = parent.OpenSubKey(name, false);
                    if (key == null) continue;

                    if (key.GetValue("DisplayName") is not string { Length: > 0 }) continue;
                    if (key.GetValue("SystemComponent") is int sc && sc == 1) continue;
                    if (key.GetValue("WindowsInstaller") is int wi && wi == 1) continue;
                    if (key.GetValue("ParentKeyName") != null) continue;

                    var uninstall = key.GetValue("UninstallString") as string ?? key.GetValue("QuietUninstallString") as string;
                    var exe = ExecutableFrom(uninstall);
                    if (exe == null) continue;
                    var driveRoot = Path.GetPathRoot(exe);
                    if (driveRoot == null || !Directory.Exists(driveRoot)) continue;
                    if (File.Exists(exe)) continue;

                    if (PathExistsOrUnknown(key.GetValue("InstallLocation") as string)) continue;
                    if (PathExistsOrUnknown(key.GetValue("DisplayIcon") as string)) continue;

                    stale.Add(name);
                }
            }
            catch (Exception ex)
            {
                Log.Write("Hledání zbytků v seznamu programů selhalo: " + ex.Message);
                continue;
            }

            if (stale.Count == 0) continue;

            // Záloha celé větve před mazáním.
            AppPaths.EnsureDataDir();
            var hive = hklm ? "HKLM" : "HKCU";
            var file = Path.Combine(AppPaths.DataDir, "zaloha-programy-" + hive + (path == UninstallPath32 ? "-32" : "") + "-" + stamp + ".reg");
            var export = Shell.Run(Shell.System32("reg.exe"), "export \"" + hive + "\\" + path + "\" \"" + file + "\" /y", 30000);
            if (export.ExitCode != 0)
            {
                Log.Write("Záloha seznamu programů selhala, zbytky se nemažou: " + export.Output);
                continue;
            }
            backupFile = file;

            using var root2 = Reg.Base(hklm);
            using var parent2 = root2.OpenSubKey(path, true);
            if (parent2 == null) continue;
            foreach (var name in stale)
            {
                try { parent2.DeleteSubKeyTree(name, false); removed++; }
                catch (Exception ex) { Log.Write("Záznam „" + name + "“ nejde smazat: " + ex.Message); }
            }
        }

        return removed;
    }

    /// <summary>Složky s daty odinstalovaných aplikací z Obchodu (v %LocalAppData%\Packages).</summary>
    private static int CleanOrphanPackageFolders(ref long bytes)
    {
        var packages = Path.Combine(Env(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (!Directory.Exists(packages)) return 0;

        var result = Shell.Run(Shell.PowerShellExe,
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Get-AppxPackage -AllUsers | ForEach-Object { $_.PackageFamilyName }\"", 90000);
        if (result.ExitCode != 0) return 0;

        var known = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).Where(l => l.Contains('_')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (known.Count < 5) return 0; // seznam je podezřele krátký, raději nemazat

        var removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(packages))
        {
            var name = Path.GetFileName(dir);
            if (known.Contains(name)) continue;
            if (name.StartsWith("Microsoft.Windows.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("MicrosoftWindows.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("windows.", StringComparison.OrdinalIgnoreCase)) continue;

            long folderBytes = 0;
            var files = 0;
            DeleteContents(dir, ref folderBytes, ref files);
            try
            {
                Directory.Delete(dir, false);
                removed++;
                bytes += folderBytes;
            }
            catch
            {
                bytes += folderBytes; // složka zůstala, ale část obsahu se smazala
            }
        }
        return removed;
    }

    /// <summary>Uklidí zbytky odinstalovaných aplikací: záznamy v seznamu programů a složky aplikací z Obchodu.</summary>
    public static LeftoverResult CleanLeftovers()
    {
        long bytes = 0;
        var entries = 0;
        string? backup = null;
        var folders = 0;

        try { entries = CleanStaleUninstallEntries(out backup); }
        catch (Exception ex) { Log.Write("Čištění seznamu programů selhalo: " + ex.Message); }

        try { folders = CleanOrphanPackageFolders(ref bytes); }
        catch (Exception ex) { Log.Write("Čištění složek aplikací selhalo: " + ex.Message); }

        Log.Write("Zbytky po aplikacích: záznamů " + entries + ", složek " + folders + " (" + bytes / 1024 / 1024 + " MB)");
        return new LeftoverResult(entries, folders, bytes, backup);
    }
}
