using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Nainstalovaná klasická aplikace (z registru Windows).</summary>
public sealed record InstalledApp(string Name, string Publisher, string Version, long SizeKb, string UninstallString)
{
    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (Publisher.Length > 0) parts.Add(Publisher);
            if (Version.Length > 0) parts.Add("verze " + Version);
            if (SizeKb > 0) parts.Add(SizeKb >= 1024 * 1024 ? (SizeKb / 1024.0 / 1024.0).ToString("0.0") + " GB" : SizeKb >= 1024 ? (SizeKb / 1024) + " MB" : SizeKb + " kB");
            return string.Join("  ·  ", parts);
        }
    }
}

public static class InstalledApps
{
    private static readonly (RegistryHive Hive, RegistryView View, string Path)[] Sources =
    {
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
    };

    private static string Str(RegistryKey key, string name) => (key.GetValue(name) as string)?.Trim() ?? "";

    public static List<InstalledApp> List()
    {
        var result = new List<InstalledApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (hive, view, path) in Sources)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = root.OpenSubKey(path, false);
                if (uninstall == null) continue;

                foreach (var name in uninstall.GetSubKeyNames())
                {
                    try
                    {
                        using var key = uninstall.OpenSubKey(name, false);
                        if (key == null) continue;

                        var display = Str(key, "DisplayName");
                        var command = Str(key, "UninstallString");
                        if (display.Length == 0 || command.Length == 0) continue;

                        // Systémové součásti, aktualizace a opravy do seznamu nepatří.
                        if (key.GetValue("SystemComponent") is int sc && sc == 1) continue;
                        if (key.GetValue("NoRemove") is int nr && nr == 1) continue;
                        if (Str(key, "ParentKeyName").Length > 0) continue;
                        var release = Str(key, "ReleaseType");
                        if (release.Equals("Update", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Hotfix", StringComparison.OrdinalIgnoreCase) ||
                            release.Equals("Security Update", StringComparison.OrdinalIgnoreCase)) continue;
                        if (display.StartsWith("Gasbyn Optimizer", StringComparison.OrdinalIgnoreCase)) continue;
                        if (Regex.IsMatch(display, @"^(Security )?Update for ", RegexOptions.IgnoreCase)) continue;

                        var version = Str(key, "DisplayVersion");
                        var uniq = display + "|" + version;
                        if (!seen.Add(uniq)) continue;

                        var size = key.GetValue("EstimatedSize") is int kb && kb > 0 ? kb : 0;
                        result.Add(new InstalledApp(display, Str(key, "Publisher"), version, size, command));
                    }
                    catch
                    {
                        // Poškozený záznam přeskočíme.
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Seznam aplikací: čtení registru selhalo (" + path + "): " + ex.Message);
            }
        }

        return result.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Spustí odinstalátor aplikace a počká na jeho dokončení. Odinstalátor zobrazí vlastní okno.</summary>
    public static (bool Ok, string Message) Uninstall(InstalledApp app)
    {
        try
        {
            var command = app.UninstallString.Trim();

            // MSI: příkaz „/I{GUID}“ aplikaci opravuje, k odinstalaci je potřeba „/X{GUID}“.
            if (command.StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
                command = Regex.Replace(command, @"/I\s*(\{)", "/X$1", RegexOptions.IgnoreCase);

            Log.Write("Odinstalace: " + app.Name + " -> " + command);

            var psi = new ProcessStartInfo
            {
                FileName = Shell.System32("cmd.exe"),
                // Vnější uvozovky jsou nutné, aby cmd správně zpracoval příkaz s cestou v uvozovkách.
                Arguments = "/c \"" + command + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return (false, "Odinstalátor se nepodařilo spustit.");
            process.WaitForExit();

            return (true, "Odinstalátor skončil (kód " + process.ExitCode + ").");
        }
        catch (Exception ex)
        {
            Log.Write("Odinstalace selhala: " + ex.Message);
            return (false, ex.Message);
        }
    }
}

public sealed record UpgradableApp(string Name, string Id, string Version, string Available)
{
    public string VersionText => (Version.Length > 0 ? Version : "?") + "  →  " + Available;

    public string Subtitle => Id + "    " + VersionText;
}

public sealed record WingetResult(bool Available, int ExitCode, string Output);

/// <summary>Aktualizace aplikací přes Windows Package Manager (winget), pokud je ve Windows k dispozici.</summary>
public static class Winget
{
    private const string Common = " --accept-source-agreements --disable-interactivity";
    private const string InstallCommon = " --silent --accept-package-agreements --accept-source-agreements --disable-interactivity";

    public static async Task<WingetResult> RunAsync(string arguments, int timeoutMs)
    {
        return await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget.exe",
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                using var process = new Process { StartInfo = psi };
                process.Start();

                var outTask = process.StandardOutput.ReadToEndAsync();
                var errTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(true); } catch { }
                    return new WingetResult(true, -1, "Vypršel časový limit.");
                }

                process.WaitForExit();
                return new WingetResult(true, process.ExitCode, outTask.Result + Environment.NewLine + errTask.Result);
            }
            catch (Win32Exception)
            {
                return new WingetResult(false, -2, "");
            }
        });
    }

    /// <summary>Vrátí seznam aplikací s dostupnou aktualizací. Při chybě výstupu vrací null a v raw je celý výstup.</summary>
    public static async Task<(List<UpgradableApp>? Apps, WingetResult Raw)> ListUpgradesAsync()
    {
        var raw = await RunAsync("upgrade" + Common, 180000);
        if (!raw.Available) return (null, raw);
        return (Parse(raw.Output), raw);
    }

    public static Task<WingetResult> UpgradeOneAsync(string id) =>
        RunAsync("upgrade --id \"" + id + "\" --exact" + InstallCommon, 1800000);

    public static Task<WingetResult> UpgradeAllAsync() =>
        RunAsync("upgrade --all" + InstallCommon, 3600000);

    /// <summary>Tabulku z winget upgrade čteme podle pozic sloupců v hlavičce (funguje i v češtině).</summary>
    internal static List<UpgradableApp>? Parse(string output)
    {
        var lines = output.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        var dashIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i].Trim(), "^-{10,}$"))
            {
                dashIndex = i;
                break;
            }
        }

        if (dashIndex < 1) return new List<UpgradableApp>(); // žádná tabulka = žádné aktualizace

        var header = lines[dashIndex - 1];
        var columns = Regex.Matches(header, @"\S+(?: \S+)*").Select(m => m.Index).ToList();
        if (columns.Count < 4) return null;

        string Cell(string line, int column)
        {
            var start = columns[column];
            if (start >= line.Length) return "";
            var end = column + 1 < columns.Count ? Math.Min(columns[column + 1], line.Length) : line.Length;
            return line.Substring(start, end - start).Trim();
        }

        var apps = new List<UpgradableApp>();
        for (var i = dashIndex + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) break;

            var id = Cell(line, 1);
            var available = Cell(line, 3);
            if (id.Length == 0 || available.Length == 0) break; // zápatí typu „3 aktualizací k dispozici“

            apps.Add(new UpgradableApp(Cell(line, 0), id, Cell(line, 2), available));
        }

        return apps;
    }
}

public sealed record BrowserCleanResult(bool Done, long Bytes, int Files, List<string> RunningBrowsers, List<string> Cleaned);

/// <summary>Čištění prohlížečů: smaže mezipaměť a cookies (po vyčištění je potřeba se na webech znovu přihlásit).</summary>
public static class BrowserCleaner
{
    private static readonly (string Name, string Process)[] Known =
    {
        ("Chrome", "chrome"), ("Edge", "msedge"), ("Brave", "brave"), ("Opera", "opera"),
        ("Vivaldi", "vivaldi"), ("Firefox", "firefox")
    };

    public static List<string> RunningBrowsers()
    {
        var running = new List<string>();
        foreach (var (name, process) in Known)
        {
            try
            {
                var found = Process.GetProcessesByName(process);
                if (found.Length > 0) running.Add(name);
                foreach (var p in found) p.Dispose();
            }
            catch
            {
                // Bez přístupu k procesům nic nehlásíme.
            }
        }
        return running;
    }

    private static readonly string[] ChromiumCacheFolders =
    {
        "Cache", "Code Cache", "GPUCache", "DawnCache", @"Service Worker\CacheStorage", @"Service Worker\ScriptCache"
    };

    private static readonly string[] ChromiumCookieFiles =
    {
        @"Network\Cookies", @"Network\Cookies-journal", "Cookies", "Cookies-journal"
    };

    private static void CleanChromiumProfile(string profile, ref long bytes, ref int files)
    {
        foreach (var folder in ChromiumCacheFolders)
            Maintenance.DeleteContents(Path.Combine(profile, folder), ref bytes, ref files);

        foreach (var file in ChromiumCookieFiles)
            Maintenance.DeleteFile(Path.Combine(profile, file), ref bytes, ref files);
    }

    public static BrowserCleanResult Clean()
    {
        var running = RunningBrowsers();
        if (running.Count > 0)
            return new BrowserCleanResult(false, 0, 0, running, new List<string>());

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        long bytes = 0;
        var files = 0;
        var cleaned = new List<string>();

        // Chromium prohlížeče s profily Default a Profile N.
        var chromium = new (string Name, string Root)[]
        {
            ("Chrome", Path.Combine(local, "Google", "Chrome", "User Data")),
            ("Edge", Path.Combine(local, "Microsoft", "Edge", "User Data")),
            ("Brave", Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")),
            ("Vivaldi", Path.Combine(local, "Vivaldi", "User Data"))
        };

        foreach (var (name, root) in chromium)
        {
            if (!Directory.Exists(root)) continue;

            try
            {
                var profiles = Directory.EnumerateDirectories(root)
                    .Where(d =>
                    {
                        var n = Path.GetFileName(d);
                        return n.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                               n.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);
                    })
                    .ToList();

                foreach (var profile in profiles)
                    CleanChromiumProfile(profile, ref bytes, ref files);

                if (profiles.Count > 0) cleaned.Add(name);
            }
            catch (Exception ex)
            {
                Log.Write("Čištění prohlížeče " + name + " selhalo: " + ex.Message);
            }
        }

        // Opera ukládá profil přímo do složky prohlížeče.
        foreach (var (name, root) in new[]
                 {
                     ("Opera", Path.Combine(roaming, "Opera Software", "Opera Stable")),
                     ("Opera GX", Path.Combine(roaming, "Opera Software", "Opera GX Stable"))
                 })
        {
            if (!Directory.Exists(root)) continue;
            CleanChromiumProfile(root, ref bytes, ref files);
            cleaned.Add(name);
        }

        // Firefox: mezipaměť je v Local, cookies v Roaming profilu.
        try
        {
            var firefoxCache = Path.Combine(local, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxCache))
            {
                foreach (var profile in Directory.EnumerateDirectories(firefoxCache))
                    Maintenance.DeleteContents(Path.Combine(profile, "cache2"), ref bytes, ref files);
            }

            var firefoxRoaming = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            var any = false;
            if (Directory.Exists(firefoxRoaming))
            {
                foreach (var profile in Directory.EnumerateDirectories(firefoxRoaming))
                {
                    foreach (var file in new[] { "cookies.sqlite", "cookies.sqlite-wal", "cookies.sqlite-shm" })
                        Maintenance.DeleteFile(Path.Combine(profile, file), ref bytes, ref files);
                    any = true;
                }
            }
            if (any) cleaned.Add("Firefox");
        }
        catch (Exception ex)
        {
            Log.Write("Čištění Firefoxu selhalo: " + ex.Message);
        }

        Log.Write("Vyčištěny prohlížeče (" + string.Join(", ", cleaned) + "): " + files + " souborů, " + bytes / 1024 / 1024 + " MB");
        return new BrowserCleanResult(true, bytes, files, running, cleaned);
    }
}
