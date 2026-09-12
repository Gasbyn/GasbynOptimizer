using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        AddTarget("User TEMP", Path.GetTempPath());
        AddTarget("Windows TEMP", Path.Combine(windows, "Temp"));
        AddTarget("Windows Update Download", Path.Combine(windows, "SoftwareDistribution", "Download"));
        AddTarget("Delivery Optimization Cache", Path.Combine(windows, "SoftwareDistribution", "DeliveryOptimization", "Cache"));
        AddTarget("Direct3D Shader Cache", Path.Combine(local, "D3DSCache"));
        AddTarget("NVIDIA DX Cache", Path.Combine(local, "NVIDIA", "DXCache"));
        AddTarget("NVIDIA GL Cache", Path.Combine(local, "NVIDIA", "GLCache"));
        AddTarget("Crash Dumps", Path.Combine(local, "CrashDumps"));

        AddBrowserCaches(local);
    }

    private void AddBrowserCaches(string local)
    {
        var chrome = Path.Combine(local, "Google", "Chrome", "User Data");
        var edge = Path.Combine(local, "Microsoft", "Edge", "User Data");
        var firefox = Path.Combine(local, "Mozilla", "Firefox", "Profiles");

        AddProfileCaches("Chrome Cache", chrome, "Cache");
        AddProfileCaches("Edge Cache", edge, "Cache");
        AddProfileCaches("Firefox Cache", firefox, "cache2");
    }

    private void AddProfileCaches(string name, string parent, string child)
    {
        if (!Directory.Exists(parent)) return;
        try
        {
            foreach (var profile in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
                AddTarget(name + " — " + Path.GetFileName(profile), Path.Combine(profile, child));
        }
        catch { }
    }

    private void AddTarget(string name, string path)
    {
        if (!Directory.Exists(path)) return;
        _targets.Add(new CleanTarget { Name = name, Path = path });
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => Scan();

    private void Scan()
    {
        ItemsPanel.Children.Clear();
        long total = 0;

        foreach (var target in _targets)
        {
            target.Size = GetDirectorySize(target.Path);
            total += target.Size;

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
                IsChecked = target.Size > 0,
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
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
        StatusText.Text = $"Scan dokončen • {_targets.Count} bezpečných umístění";
    }

    private void Clean_Click(object sender, RoutedEventArgs e)
    {
        var selected = _targets.Where(x => x.CheckBox.IsChecked == true && x.Size > 0).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Není vybraná žádná položka s daty.", "Deep Clean", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var expected = selected.Sum(x => x.Size);
        var result = MessageBox.Show(
            $"Vybráno: {selected.Count} položek\nOdhad: {FormatBytes(expected)}\n\nBudou mazány pouze soubory v těchto cache/temp umístěních. Zamčené soubory budou přeskočeny.\n\nPokračovat?",
            "Potvrdit Deep Clean", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        long deleted = 0;
        foreach (var target in selected)
            deleted += CleanDirectoryContents(target.Path);

        StatusText.Text = $"Hotovo • uvolněno přibližně {FormatBytes(deleted)}";
        Scan();
        StatusText.Text = $"Hotovo • uvolněno přibližně {FormatBytes(deleted)}";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

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
                try
                {
                    var size = new FileInfo(file).Length;
                    File.Delete(file);
                    deleted += size;
                }
                catch { }
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
}
