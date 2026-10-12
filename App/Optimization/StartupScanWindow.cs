using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GasbynOptimizer.Optimization;

public sealed class StartupScanWindow : Window
{
    private readonly StackPanel _items = new();
    private readonly TextBlock _status = new();
    private readonly Button _continue = new() { Content = "Pokračovat do optimizeru", IsEnabled = false, Padding = new Thickness(16, 10, 16, 10), Background = new SolidColorBrush(Color.FromRgb(255, 106, 0)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };

    public StartupScanWindow()
    {
        Title = "Gasbyn Optimizer — kontrola stavu";
        Width = 640;
        Height = 480;
        MinWidth = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(8, 9, 11));
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI");

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(new TextBlock { Text = "KONTROLA AKTUÁLNÍHO STAVU", FontSize = 21, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(255, 106, 0)) });
        root.Children.Add(new TextBlock
        {
            Text = "Chvilku počkej. Před zobrazením hlavního okna zjišťuji aktuální nastavení, abych nerozhodoval podle starého stavu.",
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 18), Foreground = new SolidColorBrush(Color.FromRgb(168, 173, 183))
        });
        _status.Text = "Kontroluji nastavení…";
        _status.Margin = new Thickness(0, 0, 0, 12);
        root.Children.Add(_status);
        root.Children.Add(_items);
        _continue.HorizontalAlignment = HorizontalAlignment.Right;
        _continue.Margin = new Thickness(0, 18, 0, 0);
        _continue.Click += (_, _) => DialogResult = true;
        root.Children.Add(_continue);
        Content = root;
        Loaded += async (_, _) => await ScanAsync();
    }

    private async Task ScanAsync()
    {
        _continue.IsEnabled = false;
        _status.Text = "Kontroluji Game Mode, vizuální efekty, aktivní power plan a lokální zálohy…";
        try
        {
            var results = await Task.Run(() =>
            {
                string gameMode;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar"))
                {
                    var value = key?.GetValue("AutoGameModeEnabled");
                    gameMode = value is null ? "Windows default / nenastaveno" : Convert.ToInt32(value) == 1 ? "ZAPNUTO" : "VYPNUTO";
                }

                string visuals;
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                {
                    var value = key?.GetValue("VisualFXSetting");
                    visuals = value is null ? "Windows default / nenastaveno" : Convert.ToInt32(value) == 2 ? "Best performance — ZAPNUTO" : $"Jiný režim ({value})";
                }

                string power;
                using (var process = Process.Start(new ProcessStartInfo("powercfg.exe", "/getactivescheme")
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                }) ?? throw new InvalidOperationException("powercfg.exe se nepodařilo spustit."))
                {
                    power = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0) throw new InvalidOperationException("Aktivní plán napájení se nepodařilo zjistit.");
                }
                var match = Regex.Match(power, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
                var isHigh = match.Success && match.Value.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase);
                var savedStatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer", "optimization-state.json");
                string backups = "nenalezen";
                try
                {
                    if (File.Exists(savedStatePath))
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(savedStatePath));
                        backups = doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.EnumerateObject().GetEnumerator().MoveNext()
                            ? "nalezeny — stav se dá vrátit z Full Optimizeru"
                            : "žádné aktivní zálohy";
                    }
                }
                catch { backups = "nelze přečíst"; }
                return new[]
                {
                    ("Windows Game Mode", gameMode),
                    ("Vizuální efekty", visuals),
                    ("Plán napájení", isHigh ? "High Performance" : power.Trim()),
                    ("Zálohy Gasbyn Optimizeru", backups)
                };
            });

            _items.Children.Clear();
            foreach (var item in results)
            {
                _items.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(36, 39, 46)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(12),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = item.Item1, FontWeight = FontWeights.SemiBold },
                            new TextBlock { Text = item.Item2, Foreground = new SolidColorBrush(Color.FromRgb(255, 154, 82)), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap }
                        }
                    }
                });
            }
            _status.Text = "Kontrola dokončena. Nic nebylo změněno.";
            _continue.IsEnabled = true;
        }
        catch (Exception ex)
        {
            _status.Text = "Kontrola skončila s upozorněním: " + ex.Message;
            _continue.IsEnabled = true;
        }
    }
}
