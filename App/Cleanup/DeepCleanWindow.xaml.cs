using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GasbynOptimizer.Cleanup;

public partial class DeepCleanWindow : Window
{
    private sealed class CleanTarget
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public int MinAgeDays { get; init; } = 7;
        public bool DefaultSelected { get; init; }
        public string? StartsWith { get; init; }
        public string? EndsWith { get; init; }
        public string Note { get; init; } = "";
        public long Size { get; set; }
        public CheckBox CheckBox { get; set; } = null!;
    }

    private readonly List<CleanTarget> _targets = new();

    public DeepCleanWindow()
    {
        InitializeComponent();
        BuildTargets();
        Scan();
    }

    private void BuildTargets()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        AddTarget("User TEMP", Path.GetTempPath(), 7, true,
            "Dočasné soubory starší než 7 dní. Právě používané soubory se přeskočí.");
        AddTarget("Windows TEMP", Path.Combine(windows, "Temp"), 7, true,
            "Starší dočasné soubory Windows. Některé položky mohou vyžadovat správce.");

        AddTarget("Windows Update Download", Path.Combine(windows, "SoftwareDistribution", "Download"), 14, false,
            "Starší stažené aktualizační soubory. Windows je může v případě potřeby znovu stáhnout.");
        AddTarget("Delivery Optimization Cache", Path.Combine(windows, "SoftwareDistribution", "DeliveryOptimization", "Cache"), 14, false,
            "Mezipaměť doručování aktualizací. Windows ji může znovu vytvořit.");

        AddTarget("Direct3D Shader Cache", Path.Combine(local, "D3DSCache"), 7, false,
            "Shader cache se znovu vytvoří; první spuštění hry může být dočasně pomalejší.");
        AddTarget("NVIDIA DX Cache", Path.Combine(local, "NVIDIA", "DXCache"), 7, false,
            "Grafická cache NVIDIA. Po vyčištění se může znovu vytvořit.");
        AddTarget("NVIDIA GL Cache", Path.Combine(local, "NVIDIA", "GLCache"), 7, false,
            "Grafická cache NVIDIA. Po vyčištění se může znovu vytvořit.");

        AddTarget("Crash Dumps", Path.Combine(local, "CrashDumps"), 30, false,
            "Staré výpisy pádů aplikací. Mohou být užitečné při diagnostice.");
        AddTarget("Windows Error Reporting — Queue", Path.Combine(common, "Microsoft", "Windows", "WER", "ReportQueue"), 30, false,
            "Starší čekající diagnostické reporty Windows.");
        AddTarget("Windows Error Reporting — Archive", Path.Combine(common, "Microsoft", "Windows", "WER", "ReportArchive"), 30, false,
            "Archivované diagnostické reporty Windows.");
        AddTarget("User Error Reporting — Queue", Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue"), 30, false,
            "Starší diagnostické reporty aktuálního uživatele.");
        AddTarget("User Error Reporting — Archive", Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"), 30, false,
            "Archivované diagnostické reporty aktuálního uživatele.");

        AddBrowserCaches(local);
        AddBrowserCaches(roaming);

        AddTarget("Windows Thumbnail Cache", Path.Combine(local, "Microsoft", "Windows", "Explorer"), 30, true,
            "Pouze soubory thumbcache_*.db. Náhledy se znovu vytvoří.",
            startsWith: "thumbcache_", endsWith: ".db");
    }

    private void AddBrowserCaches(string basePath)
    {
        AddChromiumCaches("Chrome", Path.Combine(basePath, "Google", "Chrome", "User Data"));
        AddChromiumCaches("Edge", Path.Combine(basePath, "Microsoft", "Edge", "User Data"));
        AddChromiumCaches("Brave", Path.Combine(basePath, "BraveSoftware", "Brave-Browser", "User Data"));
        AddChromiumCaches("Vivaldi", Path.Combine(basePath, "Vivaldi", "User Data"));
        AddChromiumCaches("Opera", Path.Combine(basePath, "Opera Software", "Opera Stable"));

        var firefox = Path.Combine(basePath, "Mozilla", "Firefox", "Profiles");
        AddProfileCaches("Firefox", firefox, new[] { "cache2" });
    }

    private void AddChromiumCaches(string browser, string userData)
    {
        AddProfileCaches(browser, userData, new[] { "Cache", "Code Cache", "GPUCache" });
    }

    private void AddProfileCaches(string browser, string parent, IEnumerable<string> cacheNames)
    {
        if (!Directory.Exists(parent)) return;

        try
        {
            foreach (var profile in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
            {
                var profileName = Path.GetFileName(profile);
                foreach (var cacheName in cacheNames)
                {
                    var path = Path.Combine(profile, cacheName);
                    AddTarget($"{browser} — {profileName} — {cacheName}", path, 7, true,
                        "Pouze cache prohlížeče; historie, cookies, hesla a stažené soubory se nemažou.");
                }
            }
        }
        catch { }
    }

    private void AddTarget(string name, string path, int minAgeDays, bool defaultSelected, string note,
        string? startsWith = null, string? endsWith = null)
    {
        if (!Directory.Exists(path)) return;

        var fullPath = Path.GetFullPath(path);
        if (_targets.Any(x => string.Equals(x.Path, fullPath, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(x.StartsWith, startsWith, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(x.EndsWith, endsWith, StringComparison.OrdinalIgnoreCase)))
            return;

        _targets.Add(new CleanTarget
        {
            Name = name,
            Path = fullPath,
            MinAgeDays = Math.Max(0, minAgeDays),
            DefaultSelected = defaultSelected,
            StartsWith = startsWith,
            EndsWith = endsWith,
            Note = note
        });
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => Scan();

    private void Scan()
    {
        ItemsPanel.Children.Clear();
        long total = 0;
        var populated = 0;

        foreach (var target in _targets)
        {
            target.Size = GetDirectorySize(target);
            total += target.Size;
            if (target.Size > 0) populated++;

            var row = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(36, 39, 46)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 7)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var check = new CheckBox
            {
                Content = target.Name,
                IsChecked = target.DefaultSelected && target.Size > 0,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"{target.Note}\n\n{target.Path}\nČistí soubory starší než {target.MinAgeDays} dní."
            };
            target.CheckBox = check;
            Grid.SetColumn(check, 0);

            var size = new TextBlock
            {
                Text = FormatBytes(target.Size),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 154, 82)),
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(size, 1);

            grid.Children.Add(check);
            grid.Children.Add(size);
            row.Child = grid;
            ItemsPanel.Children.Add(row);
        }

        TotalSizeText.Text = FormatBytes(total);
        StatusText.Text = $"Scan dokončen • {populated} umístění obsahuje čistitelná data • nic nebylo odstraněno";
    }

    private void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selected = _targets.Where(x => x.CheckBox?.IsChecked == true && x.Size > 0).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Není vybraná žádná položka s daty.", "Deep Clean", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var expected = selected.Sum(x => x.Size);
        var result = MessageBox.Show(
            $"Vybráno: {selected.Count} položek\nOdhad: {FormatBytes(expected)}\n\nBudou odstraněny pouze starší soubory z uvedených TEMP/cache/log umístění. Soubory mladší než stanovený limit, systémové soubory a odkazy na jiné složky se přeskočí. Zamčené soubory zůstanou.\n\nHistorie, cookies, hesla, stažené soubory, osobní dokumenty a nastavení aplikací se nemažou.\n\nPokračovat?",
            "Potvrdit Deep Clean", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        long deleted = 0;
        foreach (var target in selected)
            deleted += CleanDirectoryContents(target);

        Scan();
        StatusText.Text = $"Hotovo • uvolněno přibližně {FormatBytes(deleted)}";
        MessageBox.Show($"Úklid dokončen.\nUvolněno přibližně {FormatBytes(deleted)}.\n\nNěkteré zamčené nebo novější soubory mohly zůstat.", "Deep Clean", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ComponentCleanup_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Windows Component Cleanup odstraní pouze nahrazené systémové komponenty, které už nejsou potřeba.\n\nNepoužívá se /ResetBase, takže zůstává zachována možnost odinstalovat aktualizace, pokud to Windows dovolí.\n\nOperace může trvat několik minut a vyžádá si potvrzení správce (UAC). Pokračovat?",
            "Windows Component Cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        ComponentCleanupButton.IsEnabled = false;
        StatusText.Text = "Probíhá systémové čištění Windows (DISM)…";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = "/Online /Cleanup-Image /StartComponentCleanup",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                throw new InvalidOperationException("DISM se nepodařilo spustit.");

            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode == 0)
            {
                StatusText.Text = "Windows Component Cleanup dokončen.";
                MessageBox.Show("Systémové čištění Windows bylo dokončeno.", "Windows Component Cleanup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                StatusText.Text = $"DISM skončil s kódem {process.ExitCode}.";
                MessageBox.Show($"DISM skončil s kódem {process.ExitCode}. Zkontroluj případnou chybu v okně DISM.", "Windows Component Cleanup", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            StatusText.Text = "Systémové čištění bylo zrušeno.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Systémové čištění se nepodařilo spustit.";
            MessageBox.Show(ex.Message, "Windows Component Cleanup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            ComponentCleanupButton.IsEnabled = true;
        }
    }

    private void OpenStorage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:storage") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Windows Storage", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static long GetDirectorySize(CleanTarget target)
    {
        long total = 0;
        foreach (var file in GetEligibleFiles(target))
        {
            try { total += new FileInfo(file).Length; } catch { }
        }
        return total;
    }

    private static long CleanDirectoryContents(CleanTarget target)
    {
        long deleted = 0;
        foreach (var file in GetEligibleFiles(target))
        {
            try
            {
                var size = new FileInfo(file).Length;
                File.Delete(file);
                if (!File.Exists(file)) deleted += size;
            }
            catch { }
        }
        return deleted;
    }

    private static List<string> GetEligibleFiles(CleanTarget target)
    {
        var files = new List<string>();
        if (!Directory.Exists(target.Path)) return files;

        var cutoffUtc = DateTime.UtcNow.AddDays(-target.MinAgeDays);
        var pending = new Stack<string>();
        pending.Push(target.Path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            string[] entries;

            try
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if ((attributes & FileAttributes.Directory) == 0) continue;
                entries = Directory.GetFileSystemEntries(current);
            }
            catch { continue; }

            foreach (var entry in entries)
            {
                FileAttributes attributes;
                try { attributes = File.GetAttributes(entry); }
                catch { continue; }

                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                    continue;
                }

                if ((attributes & FileAttributes.System) != 0) continue;

                var fileName = Path.GetFileName(entry);
                if (!string.IsNullOrEmpty(target.StartsWith) &&
                    !fileName.StartsWith(target.StartsWith, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrEmpty(target.EndsWith) &&
                    !fileName.EndsWith(target.EndsWith, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    if (target.MinAgeDays > 0 && File.GetLastWriteTimeUtc(entry) > cutoffUtc) continue;
                    files.Add(entry);
                }
                catch { }
            }
        }

        return files;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.00} GB";
    }
}