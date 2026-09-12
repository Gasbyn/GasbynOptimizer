using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private readonly Dictionary<string, long> _lastScanSizes = new(StringComparer.OrdinalIgnoreCase);

    private void DynamicAction_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var button = FindParent<Button>(source);
        if (button is null || button.Tag is not string title) return;
        if (!button.IsDescendantOf(SecondaryContent)) return;
        e.Handled = true;
        RunDynamicAction(title);
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match) return match;
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private void RunDynamicAction(string title)
    {
        try
        {
            switch (title)
            {
                case "User TEMP": ScanAndOfferCleanup("User TEMP", new[] { Path.GetTempPath() }); break;
                case "Windows Update Cache": ScanAndOfferCleanup("Windows Update Cache", new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download") }); break;
                case "Delivery Optimization Cache": ScanAndOfferCleanup("Delivery Optimization Cache", new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "DeliveryOptimization", "Cache") }); break;
                case "Shader Cache": ScanAndOfferCleanup("Shader Cache", GetShaderCachePaths()); break;
                case "Windows + User TEMP": ScanAndOfferCleanup("Windows + User TEMP", GetTempPaths()); break;
                case "Browser Cache": ScanAndOfferCleanup("Browser Cache", GetBrowserCachePaths()); break;
                case "Crash Dumps & Logs": ScanAndOfferCleanup("Crash Dumps & Logs", GetCrashDumpPaths()); break;
                case "GPU / Shader Cache": ScanAndOfferCleanup("GPU / Shader Cache", GetShaderCachePaths()); break;
                case "Windows Update / Delivery Cache": ScanAndOfferCleanup("Windows Update / Delivery Cache", new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "DeliveryOptimization", "Cache")
                }); break;
                case "Recycle Bin": ShowRecycleBinInfo(); break;
                case "Large Files": ShowLargeFiles(); break;
                case "Deep Scan Summary": RunDeepScan(); break;
                case "Startup Registry Entries": ShowStartupRegistry(); break;
                case "Startup Folder": ShowStartupFolder(); break;
                case "Scheduled Startup Tasks": ShowScheduledTasks(); break;
                case "Installed Applications": OpenAppsManager(); break;
                case "App Checker": OpenAppsManager(); break;
                case "Deep Uninstall": OpenAppsManager(); break;
                case "Connection Info": ShowConnectionInfo(); break;
                case "DNS Check": ShowDnsInfo(); break;
                case "TCP/IP Health": RunNetworkHealth(); break;
                case "Network Reset Tools": ShowNetworkResetWarning(); break;
                case "System Drive": ShowDriveInfo(); break;
                case "Windows Version": MessageBox.Show(Environment.OSVersion.VersionString, "Windows", MessageBoxButton.OK, MessageBoxImage.Information); break;
                case ".NET Runtime": MessageBox.Show($".NET runtime: {Environment.Version}", ".NET", MessageBoxButton.OK, MessageBoxImage.Information); break;
                case "Hardware Detection": LoadSystemInfo(); ShowPage("PC Info"); break;
                case "CPU Quick Test": RunCpuBenchmark(); break;
                case "Memory Snapshot": MessageBox.Show($"Installed memory: {RamValue.Text}", "Memory", MessageBoxButton.OK, MessageBoxImage.Information); break;
                case "Disk Space": ShowDriveInfo(); break;
                case "Before / After": MessageBox.Show("Before/After historie bude dostupná po uložení prvního benchmarku. Aktuální benchmark lze spustit z této sekce.", "Benchmark", MessageBoxButton.OK, MessageBoxImage.Information); break;
                case "Tweak Backups": ShowBackupFolder(); break;
                case "Restore All": ShowRestoreInfo(); break;
                case "Change History": ShowHistory(); break;
                case "Refresh": LoadSystemInfo(); ShowPage("PC Info"); break;
                case "Manage Game Mode": GameModeAction_Click(this, new RoutedEventArgs()); break;
                case "Quick Optimize": QuickOptimizeSafe(); break;
                default: HandleGenericAction(title); break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Akce selhala bezpečně bez provedení změny:\n\n{ex.Message}", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void HandleGenericAction(string title)
    {
        if (title.Equals("Open", StringComparison.OrdinalIgnoreCase) || title.Equals("Open manager", StringComparison.OrdinalIgnoreCase))
        {
            OpenAppsManager();
            return;
        }
        if (title.Contains("Check", StringComparison.OrdinalIgnoreCase) || title.Contains("Scan", StringComparison.OrdinalIgnoreCase) || title.Contains("Run", StringComparison.OrdinalIgnoreCase))
        {
            RunDeepScan();
            return;
        }
        MessageBox.Show($"Modul „{title}“ je připravený a tato akce zatím neprovádí nevratné systémové změny.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenAppsManager()
    {
        var window = new Apps.AppsManagerWindow { Owner = this };
        window.ShowDialog();
    }

    private string[] GetTempPaths() => new[]
    {
        Path.GetTempPath(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
    };

    private string[] GetBrowserCachePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new List<string>();
        AddExistingParent(paths, Path.Combine(local, "Google", "Chrome", "User Data"), "Cache");
        AddExistingParent(paths, Path.Combine(local, "Microsoft", "Edge", "User Data"), "Cache");
        AddExistingParent(paths, Path.Combine(local, "Mozilla", "Firefox", "Profiles"), "cache2");
        return paths.ToArray();
    }

    private static void AddExistingParent(List<string> list, string parent, string childName)
    {
        if (!Directory.Exists(parent)) return;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                var candidate = Path.Combine(directory, childName);
                if (Directory.Exists(candidate)) list.Add(candidate);
            }
        }
        catch { }
    }

    private string[] GetCrashDumpPaths() => new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump")
    };

    private string[] GetShaderCachePaths() => new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "DXCache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA", "GLCache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache")
    };

    private void ScanAndOfferCleanup(string name, IEnumerable<string> paths)
    {
        var unique = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        long total = 0;
        var details = new StringBuilder();
        foreach (var path in unique)
        {
            if (!Directory.Exists(path)) continue;
            var size = GetDirectorySize(path);
            total += size;
            _lastScanSizes[name] = total;
            details.AppendLine($"{path}\n  {FormatBytes(size)}");
        }

        if (total == 0)
        {
            MessageBox.Show($"{name}: nebyla nalezena data k bezpečnému úklidu.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var question = $"{name}\n\nNalezeno přibližně {FormatBytes(total)}.\n\n{details}\nChceš odstranit pouze soubory, které lze bezpečně odstranit? Zamčené položky budou přeskočeny.";
        if (MessageBox.Show(question, "Potvrdit cleanup", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var deleted = 0L;
        foreach (var path in unique) deleted += CleanDirectoryContents(path);
        MessageBox.Show($"Cleanup dokončen. Uvolněno přibližně {FormatBytes(deleted)}. Některé zamčené položky mohou zůstat.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static long GetDirectorySize(string path)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static long CleanDirectoryContents(string path)
    {
        long deleted = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { var len = new FileInfo(file).Length; File.Delete(file); deleted += len; } catch { }
            }
            foreach (var directory in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length))
            {
                try { Directory.Delete(directory, false); } catch { }
            }
        }
        catch { }
        return deleted;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.00} GB";
    }

    private void ShowRecycleBinInfo()
    {
        MessageBox.Show("Koš je oddělený od běžných temp/cache složek. V této verzi zobrazujeme jeho stav přes Windows UI; automatické vyprazdňování bez dalšího potvrzení se nepouští.", "Recycle Bin", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunDeepScan()
    {
        var groups = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["TEMP"] = GetTempPaths(),
            ["Browser cache"] = GetBrowserCachePaths(),
            ["Crash dumps"] = GetCrashDumpPaths(),
            ["GPU cache"] = GetShaderCachePaths(),
            ["Windows Update"] = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution", "Download") }
        };
        var sb = new StringBuilder("GASBYN DEEP SCAN\n\n");
        long total = 0;
        foreach (var pair in groups)
        {
            long size = pair.Value.Where(Directory.Exists).Sum(GetDirectorySize);
            total += size;
            sb.AppendLine($"{pair.Key}: {FormatBytes(size)}");
        }
        sb.AppendLine($"\nCelkem nalezeno: {FormatBytes(total)}");
        sb.AppendLine("\nNic nebylo odstraněno.");
        MessageBox.Show(sb.ToString(), "Deep Scan", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowLargeFiles()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var cutoff = 1L * 1024 * 1024 * 1024;
        var results = new List<(string Path, long Size)>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length >= cutoff) results.Add((file, info.Length));
                    if (results.Count >= 20) break;
                }
                catch { }
            }
        }
        catch { }
        var text = results.Count == 0 ? "Nenalezen žádný soubor nad 1 GB v rychlém scanu." : string.Join("\n", results.OrderByDescending(x => x.Size).Select(x => $"{FormatBytes(x.Size)}  {x.Path}"));
        MessageBox.Show(text, "Large Files", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowStartupRegistry()
    {
        var lines = new List<string>();
        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (key is null) continue;
            foreach (var name in key.GetValueNames()) lines.Add($"{root.Name}\\...\\Run\\{name} = {key.GetValue(name)}");
        }
        MessageBox.Show(lines.Count == 0 ? "Žádné běžné Run položky." : string.Join("\n", lines), "Startup Registry", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowStartupFolder()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var files = Directory.Exists(folder) ? Directory.EnumerateFiles(folder).ToList() : new List<string>();
        MessageBox.Show(files.Count == 0 ? $"Startup folder je prázdný.\n{folder}" : string.Join("\n", files), "Startup Folder", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowScheduledTasks()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe", "/Query /FO LIST /V") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using var process = Process.Start(psi);
            if (process is null) return;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            var lines = output.Split(Environment.NewLine).Where(x => x.Contains("TaskName:", StringComparison.OrdinalIgnoreCase)).Take(80);
            MessageBox.Show(string.Join("\n", lines), "Scheduled Tasks", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Scheduled Tasks", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ShowConnectionInfo()
    {
        var sb = new StringBuilder();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a => a.OperationalStatus == OperationalStatus.Up))
        {
            var props = adapter.GetIPProperties();
            sb.AppendLine($"{adapter.Name} — {adapter.NetworkInterfaceType}");
            sb.AppendLine($"  Speed: {adapter.Speed / 1_000_000} Mbps");
            sb.AppendLine($"  IPv4: {string.Join(", ", props.UnicastAddresses.Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(x => x.Address))}");
            sb.AppendLine();
        }
        MessageBox.Show(sb.Length == 0 ? "Není aktivní síťový adaptér." : sb.ToString(), "Connection Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowDnsInfo()
    {
        var sb = new StringBuilder();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a => a.OperationalStatus == OperationalStatus.Up))
        {
            var dns = adapter.GetIPProperties().DnsAddresses;
            if (dns.Count > 0) sb.AppendLine($"{adapter.Name}: {string.Join(", ", dns)}");
        }
        MessageBox.Show(sb.Length == 0 ? "DNS servery nebyly nalezeny." : sb.ToString(), "DNS Check", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunNetworkHealth()
    {
        var sb = new StringBuilder();
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("1.1.1.1", 2500);
            sb.AppendLine($"Internet ping: {reply.Status} {(reply.Status == IPStatus.Success ? $"({reply.RoundtripTime} ms)" : "")}");
        }
        catch (Exception ex) { sb.AppendLine($"Ping: {ex.Message}"); }
        try
        {
            var dns = System.Net.Dns.GetHostAddresses("example.com");
            sb.AppendLine($"DNS resolution: OK ({dns.Length} address(es))");
        }
        catch { sb.AppendLine("DNS resolution: FAILED"); }
        MessageBox.Show(sb.ToString(), "TCP/IP Health", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowNetworkResetWarning()
    {
        MessageBox.Show("Network Reset je připraven pouze jako diagnostický modul. Reset síťového stacku může odpojit internet a změnit konfiguraci, takže ho zatím nespouštíme automaticky.", "Network Reset", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void ShowDriveInfo()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        var drive = new DriveInfo(root);
        MessageBox.Show($"{root}\n\nFree: {FormatBytes(drive.AvailableFreeSpace)}\nTotal: {FormatBytes(drive.TotalSize)}\nUsed: {FormatBytes(drive.TotalSize - drive.AvailableFreeSpace)}", "System Drive", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunCpuBenchmark()
    {
        const int iterations = 20_000_000;
        var sw = Stopwatch.StartNew();
        double value = 0;
        for (var i = 1; i <= iterations; i++) value += Math.Sqrt(i) * Math.Sin(i * 0.00001);
        sw.Stop();
        MessageBox.Show($"CPU Quick Test\n\nTime: {sw.Elapsed.TotalMilliseconds:0} ms\nChecksum: {value:0.0000}\n\nPoužívej stejné podmínky pro Before/After porovnání.", "Benchmark", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowBackupFolder()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer", "Backups");
        Directory.CreateDirectory(folder);
        var files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList();
        MessageBox.Show(files.Count == 0 ? $"Žádné backupy.\n\nFolder: {folder}" : string.Join("\n", files), "Backup Center", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowRestoreInfo()
    {
        MessageBox.Show("Restore funguje pro tweak, který vytvořil vlastní backup. Globální Restore All záměrně nemažeme naslepo: nejdřív se musí zobrazit konkrétní backup a jeho rozsah.", "Backup Center", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowHistory()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer");
        var log = Path.Combine(folder, "history.log");
        if (!File.Exists(log))
        {
            MessageBox.Show("Historie je zatím prázdná.", "Change History", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        MessageBox.Show(File.ReadAllText(log), "Change History", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void QuickOptimizeSafe()
    {
        var before = GetDirectorySize(Path.GetTempPath());
        var result = MessageBox.Show($"Quick Optimize provede pouze bezpečný cleanup aktuálního TEMP.\n\nNalezeno: {FormatBytes(before)}\n\nPokračovat?", "Quick Optimize", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;
        var deleted = CleanDirectoryContents(Path.GetTempPath());
        MessageBox.Show($"Hotovo. Uvolněno přibližně {FormatBytes(deleted)}. Windows soubory, registry ani ovladače nebyly měněny.", "Quick Optimize", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
