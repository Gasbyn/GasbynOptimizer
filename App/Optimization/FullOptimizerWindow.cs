using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GasbynOptimizer.Optimization;

public sealed class FullOptimizerWindow : Window
{
    private const string AppFolderName = "GasbynOptimizer";
    private readonly StackPanel _root = new();
    private readonly TextBlock _scanStatus = new();
    private readonly TextBlock _results = new();
    private readonly TextBox _processName = new() { Text = "FortniteClient-Win64-Shipping.exe", MinWidth = 260 };
    private readonly TextBlock _benchmarkStatus = new();
    private bool _busy;
    private readonly Dictionary<string, TextBlock> _toggleStatuses = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName);
    private static readonly string StatePath = Path.Combine(DataFolder, "optimization-state.json");
    private static readonly string BenchmarkPath = Path.Combine(DataFolder, "fps-tests.json");

    public FullOptimizerWindow()
    {
        Title = "Gasbyn Optimizer — Full Optimizer";
        Width = 980;
        Height = 760;
        MinWidth = 780;
        MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#08090B");
        Foreground = Brush("#F4F4F5");
        FontFamily = new FontFamily("Segoe UI");

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.Content = _root;
        Content = scroll;
        _root.Margin = new Thickness(24);
        _root.Children.Add(new TextBlock { Text = "FULL OPTIMIZER", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brush("#FF6A00") });
        _root.Children.Add(new TextBlock
        {
            Text = "Reverzibilní nastavení Windows. Každý modul ukládá původní stav před změnou; žádné hromadné vypínání systémových služeb.",
            Margin = new Thickness(0, 8, 0, 18), TextWrapping = TextWrapping.Wrap, Foreground = Brush("#A8ADB7")
        });
        _scanStatus.Text = "Kontroluji aktuální stav nastavení…";
        _scanStatus.Margin = new Thickness(0, 0, 0, 16);
        _scanStatus.Foreground = Brush("#FF9A52");
        _root.Children.Add(_scanStatus);

        AddToggleCard("Windows Game Mode",
            "Zapne herní režim Windows. Nezaručuje vyšší FPS; výsledek závisí na PC a hrách.",
            "game-mode", "Zapnout", "Vrátit původní stav");
        AddToggleCard("Výkonové vizuální efekty",
            "Nastaví Windows na nejlepší výkon (může vypnout některé animace a vizuální efekty).",
            "visual-effects", "Zapnout", "Vrátit původní stav");
        AddToggleCard("High Performance power plan",
            "Přepne na vestavěný plán Vysoký výkon. Může zvýšit spotřebu a teploty; původní plán se uloží.",
            "power-plan", "Zapnout", "Vrátit původní stav");

        var restoreCard = Card();
        restoreCard.Children.Add(Heading("BOD OBNOVENÍ WINDOWS"));
        restoreCard.Children.Add(Body("Vytvoří systémový bod obnovení, pokud je ochrana systému zapnutá. Windows může povolit jen jeden bod za 24 hodin."));
        var restoreButton = ActionButton("Vytvořit bod obnovení");
        restoreButton.Click += async (_, _) => await CreateRestorePointAsync();
        restoreCard.Children.Add(restoreButton);
        _root.Children.Add(restoreCard);

        var fpsCard = Card();
        fpsCard.Children.Add(Heading("FPS TESTER — POSLEDNÍCH 15 TESTŮ"));
        fpsCard.Children.Add(Body("Vyber oficiální PresentMon.exe. Test sbírá frame times po dobu 60 sekund, pokud běží zadaný proces. Výsledek není sám o sobě důkazem, že tweak zvyšuje výkon; testuj ve stejných podmínkách."));
        var pathRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var presentMonPath = new TextBox { Name = "PresentMonPath", MinWidth = 300, Height = 32, VerticalContentAlignment = VerticalAlignment.Center, Background = Brush("#0D0F13"), Foreground = Brush("#F4F4F5"), BorderBrush = Brush("#343842"), Padding = new Thickness(8) };
        presentMonPath.Text = LoadPresentMonPath();
        var browse = ActionButton("Vybrat PresentMon.exe");
        browse.Margin = new Thickness(8, 0, 0, 0);
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog { Title = "Vyber PresentMon.exe", Filter = "PresentMon executable|PresentMon.exe;PresentMon-*.exe|EXE files|*.exe" };
            if (dialog.ShowDialog(this) == true) { presentMonPath.Text = dialog.FileName; SavePresentMonPath(dialog.FileName); }
        };
        pathRow.Children.Add(presentMonPath);
        pathRow.Children.Add(browse);
        fpsCard.Children.Add(pathRow);
        var processRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        processRow.Children.Add(new TextBlock { Text = "Proces:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _processName.Background = Brush("#0D0F13"); _processName.Foreground = Brush("#F4F4F5"); _processName.BorderBrush = Brush("#343842"); _processName.Padding = new Thickness(8); _processName.Height = 32;
        processRow.Children.Add(_processName);
        var runTest = ActionButton("Spustit 60s test");
        runTest.Margin = new Thickness(8, 0, 0, 0);
        runTest.Click += async (_, _) => await RunFpsTestAsync(presentMonPath.Text, runTest);
        processRow.Children.Add(runTest);
        fpsCard.Children.Add(processRow);
        _benchmarkStatus.Margin = new Thickness(0, 10, 0, 0);
        _benchmarkStatus.Foreground = Brush("#FF9A52");
        fpsCard.Children.Add(_benchmarkStatus);
        _results.Margin = new Thickness(0, 8, 0, 0);
        _results.TextWrapping = TextWrapping.Wrap;
        fpsCard.Children.Add(_results);
        var refreshTests = ActionButton("Obnovit historii");
        refreshTests.Margin = new Thickness(0, 10, 0, 0);
        refreshTests.Click += (_, _) => RefreshBenchmarkHistory();
        fpsCard.Children.Add(refreshTests);
        _root.Children.Add(fpsCard);

        Loaded += async (_, _) =>
        {
            await ScanCurrentStateAsync();
            RefreshBenchmarkHistory();
        };
    }

    private void AddToggleCard(string title, string description, string id, string enableText, string restoreText)
    {
        var card = Card();
        card.Children.Add(Heading(title));
        card.Children.Add(Body(description));
        var status = new TextBlock { Text = "Čekám na kontrolu…", Foreground = Brush("#FF9A52"), Margin = new Thickness(0, 10, 0, 0) };
        card.Children.Add(status);
        _toggleStatuses[id] = status;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var enable = ActionButton(enableText);
        enable.Click += async (_, _) => await ChangeSettingAsync(id, true, status);
        var restore = ActionButton(restoreText);
        restore.Background = Brush("#292D35");
        restore.Margin = new Thickness(8, 0, 0, 0);
        restore.Click += async (_, _) => await ChangeSettingAsync(id, false, status);
        row.Children.Add(enable);
        row.Children.Add(restore);
        card.Children.Add(row);
        _root.Children.Add(card);
    }

    private async Task ScanCurrentStateAsync()
    {
        SetBusy(true);
        _scanStatus.Text = "Kontroluji, co je aktuálně zapnuté/vypnuté…";
        try
        {
            var scan = await Task.Run(() => new
            {
                GameMode = GetGameModeState(),
                VisualEffects = GetVisualEffectsState(),
                PowerPlan = GetPowerPlanState(),
                Saved = LoadState()
            });
            if (_toggleStatuses.TryGetValue("game-mode", out var game))
                game.Text = $"Aktuální stav: {(scan.GameMode == true ? "ZAPNUTO" : scan.GameMode == false ? "VYPNUTO" : "Windows default / nenastaveno")} · backup: {(scan.Saved.ContainsKey("game-mode") ? "uložen" : "není")}";
            if (_toggleStatuses.TryGetValue("visual-effects", out var visual))
            {
                var v = scan.VisualEffects is null ? "Windows default" : Convert.ToInt32(scan.VisualEffects, CultureInfo.InvariantCulture) == 2 ? "ZAPNUTO (best performance)" : $"Jiný režim ({scan.VisualEffects})";
                visual.Text = $"Aktuální stav: {v} · backup: {(scan.Saved.ContainsKey("visual-effects") ? "uložen" : "není")}";
            }
            if (_toggleStatuses.TryGetValue("power-plan", out var power))
            {
                var active = System.Text.RegularExpressions.Regex.Match(scan.PowerPlan, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})").Groups[1].Value;
                var high = active.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase);
                power.Text = $"Aktuální plán: {(high ? "High Performance" : scan.PowerPlan.Trim())} · backup: {(scan.Saved.ContainsKey("power-plan") ? "uložen" : "není")}";
            }
            _scanStatus.Text = "Kontrola dokončena. Aktuální stav je načten; po změně proběhne další kontrola.";
        }
        catch (Exception ex) { _scanStatus.Text = "Kontrola dokončena s upozorněním: " + ex.Message; }
        finally { SetBusy(false); }
    }

    private async Task ChangeSettingAsync(string id, bool enable, TextBlock status)
    {
        if (_busy) return;
        var label = id switch { "game-mode" => "Windows Game Mode", "visual-effects" => "Výkonové vizuální efekty", _ => "High Performance power plan" };
        var prompt = enable
            ? $"Zapnout modul „{label}“? Původní stav se uloží, aby šel vrátit. Některé změny mohou vyžadovat restart Windows."
            : $"Vrátit modul „{label}“ do původního stavu uloženého při zapnutí?";
        if (MessageBox.Show(this, prompt, "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        SetBusy(true);
        status.Text = enable ? "Ukládám původní stav a zapínám…" : "Obnovuji uložený původní stav…";
        try
        {
            await Task.Run(() => ApplySetting(id, enable));
            status.Text = enable ? "Zapnuto. Pro plné použití změn doporučuji restart PC." : "Původní stav obnoven.";
            _scanStatus.Text = "Znovu ověřuji aktuální nastavení…";
            await ScanCurrentStateAsync();
        }
        catch (Exception ex)
        {
            status.Text = "Akce se nepovedla.";
            MessageBox.Show(this, ex.Message, "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private static void ApplySetting(string id, bool enable)
    {
        Directory.CreateDirectory(DataFolder);
        var state = LoadState();
        if (enable)
        {
            if (!state.ContainsKey(id)) state[id] = Capture(id);
            SaveState(state);
            switch (id)
            {
                case "game-mode":
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\GameBar"))
                    {
                        key!.SetValue("AutoGameModeEnabled", 1, RegistryValueKind.DWord);
                        key.SetValue("AllowAutoGameMode", 1, RegistryValueKind.DWord);
                    }
                    break;
                case "visual-effects":
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"))
                        key!.SetValue("VisualFXSetting", 2, RegistryValueKind.DWord);
                    break;
                case "power-plan":
                    RunPowerCfg("/setactive SCHEME_MIN");
                    break;
                default: throw new InvalidOperationException("Neznámý optimalizační modul.");
            }
        }
        else
        {
            if (!state.TryGetValue(id, out var snapshot))
                throw new InvalidOperationException("Pro tento modul není uložený původní stav. Nic nebylo změněno.");
            Restore(id, snapshot);
            state.Remove(id);
            SaveState(state);
        }
    }

    private static Snapshot Capture(string id)
    {
        var s = new Snapshot();
        if (id == "power-plan")
        {
            var output = RunPowerCfg("/getactivescheme");
            var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
            if (!match.Success) throw new InvalidOperationException("Nepodařilo se zjistit aktivní plán napájení.");
            s.PowerScheme = match.Value;
            return s;
        }

        var values = id == "game-mode"
            ? new[] { (Path: @"Software\Microsoft\GameBar", Name: "AutoGameModeEnabled"), (Path: @"Software\Microsoft\GameBar", Name: "AllowAutoGameMode") }
            : new[] { (Path: @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", Name: "VisualFXSetting") };
        foreach (var item in values)
        {
            using var key = Registry.CurrentUser.OpenSubKey(item.Path);
            var value = key?.GetValue(item.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            s.RegistryValues.Add(new RegistrySnapshot
            {
                Path = item.Path, Name = item.Name, Exists = value is not null,
                Kind = value is null ? RegistryValueKind.String.ToString() : key!.GetValueKind(item.Name).ToString(),
                Data = value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
            });
        }
        return s;
    }

    private static void Restore(string id, Snapshot snapshot)
    {
        if (id == "power-plan")
        {
            if (string.IsNullOrWhiteSpace(snapshot.PowerScheme)) throw new InvalidOperationException("Backup plánu napájení je neplatný.");
            RunPowerCfg("/setactive " + snapshot.PowerScheme);
            return;
        }
        foreach (var item in snapshot.RegistryValues)
        {
            if (item.Exists)
            {
                using var key = Registry.CurrentUser.CreateSubKey(item.Path);
                var kind = Enum.TryParse<RegistryValueKind>(item.Kind, out var parsed) ? parsed : RegistryValueKind.String;
                object value = kind switch
                {
                    RegistryValueKind.DWord => int.Parse(item.Data, CultureInfo.InvariantCulture),
                    RegistryValueKind.QWord => long.Parse(item.Data, CultureInfo.InvariantCulture),
                    RegistryValueKind.Binary => Convert.FromBase64String(item.Data),
                    RegistryValueKind.MultiString => item.Data.Split('|'),
                    _ => item.Data
                };
                key!.SetValue(item.Name, value, kind);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(item.Path, writable: true);
                key?.DeleteValue(item.Name, throwOnMissingValue: false);
            }
        }
    }

    private static bool? GetGameModeState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
        var value = key?.GetValue("AutoGameModeEnabled");
        return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
    }

    private static object? GetVisualEffectsState()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
        return key?.GetValue("VisualFXSetting");
    }

    private static string GetPowerPlanState() => RunPowerCfg("/getactivescheme");

    private static Dictionary<string, Snapshot> LoadState()
    {
        try
        {
            if (!File.Exists(StatePath)) return new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, Snapshot>>(File.ReadAllText(StatePath))
                   ?? new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase);
        }
        catch { return new Dictionary<string, Snapshot>(StringComparer.OrdinalIgnoreCase); }
    }

    private static void SaveState(Dictionary<string, Snapshot> state)
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(StatePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task CreateRestorePointAsync()
    {
        if (_busy) return;
        SetBusy(true);
        _scanStatus.Text = "Žádám Windows o vytvoření bodu obnovení…";
        try
        {
            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo("powershell.exe")
                {
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Arguments = "-NoProfile -Command \"$ErrorActionPreference='Stop'; Checkpoint-Computer -Description 'Gasbyn Optimizer' -RestorePointType MODIFY_SETTINGS\""
                };
                using var p = Process.Start(psi) ?? throw new InvalidOperationException("PowerShell se nepodařilo spustit.");
                p.WaitForExit();
                if (p.ExitCode != 0) throw new InvalidOperationException("Windows bod obnovení nevytvořil. Ověř Ochranu systému; Windows může povolit jen jeden bod za 24 hodin.");
            });
            _scanStatus.Text = "Příkaz pro bod obnovení doběhl. Ověř jeho existenci v „Vytvořit bod obnovení“ ve Windows.";
            MessageBox.Show(this, "Příkaz pro bod obnovení byl dokončen. Ověř jeho existenci v Ochrana systému. Pokud Windows už dnes bod vytvořil, nový mohl být odmítnut.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, "Bod obnovení se nepodařilo vytvořit. " + ex.Message, "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { SetBusy(false); }
    }

    private async Task RunFpsTestAsync(string exePath, Button runButton)
    {
        if (_busy) return;
        if (!File.Exists(exePath))
        {
            MessageBox.Show(this, "Nejdřív vyber oficiální PresentMon.exe.", "FPS tester", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var processName = _processName.Text.Trim();
        if (string.IsNullOrWhiteSpace(processName) || processName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "Zadej platný název procesu, například FortniteClient-Win64-Shipping.exe.", "FPS tester", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        SetBusy(true);
        runButton.IsEnabled = false;
        _benchmarkStatus.Text = "Spouštím záznam. Nech hru běžet a během testu neměň nastavení…";
        try
        {
            var folder = Path.Combine(DataFolder, "FpsTests");
            Directory.CreateDirectory(folder);
            var output = Path.Combine(folder, $"presentmon-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            using var process = new Process { StartInfo = new ProcessStartInfo(exePath) { UseShellExecute = false, CreateNoWindow = true } };
            process.StartInfo.ArgumentList.Add("--process_name");
            process.StartInfo.ArgumentList.Add(processName);
            process.StartInfo.ArgumentList.Add("--output_file");
            process.StartInfo.ArgumentList.Add(output);
            if (!process.Start()) throw new InvalidOperationException("PresentMon se nepodařilo spustit.");
            for (var seconds = 60; seconds > 0; seconds--)
            {
                _benchmarkStatus.Text = $"Měření běží… zbývá {seconds} s. Nech hru běžet.";
                await Task.Delay(1000);
                if (process.HasExited) break;
            }
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync(); } catch { }
            await Task.Delay(800);
            var frameTimes = ParseFrameTimes(output);
            if (frameTimes.Count < 30)
                throw new InvalidOperationException("PresentMon nezapsal dost snímků. Ověř název procesu, spusť hru a případně spusť PresentMon jako správce.");
            var sorted = frameTimes.OrderBy(x => x).ToArray();
            var avgFps = 1000d / frameTimes.Average();
            var p99 = sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * 0.99) - 1, 0, sorted.Length - 1)];
            var low1 = p99 > 0 ? 1000d / p99 : 0;
            var record = new FpsRecord { Date = DateTime.Now, Process = processName, DurationSeconds = 60, AverageFps = Math.Round(avgFps, 1), OnePercentLow = Math.Round(low1, 1), Frames = frameTimes.Count };
            var records = LoadBenchmarks();
            records.Insert(0, record);
            records = records.Take(15).ToList();
            File.WriteAllText(BenchmarkPath, JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            _benchmarkStatus.Text = $"Hotovo: průměr {record.AverageFps:0.0} FPS · 1% low přibližně {record.OnePercentLow:0.0} FPS · {record.Frames} snímků.";
            RefreshBenchmarkHistory();
        }
        catch (Exception ex)
        {
            _benchmarkStatus.Text = "Test se nepodařilo dokončit.";
            MessageBox.Show(this, ex.Message, "FPS tester", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { runButton.IsEnabled = true; SetBusy(false); }
    }

    private static List<double> ParseFrameTimes(string csvPath)
    {
        var values = new List<double>();
        if (!File.Exists(csvPath)) return values;
        using var reader = new StreamReader(csvPath);
        var header = reader.ReadLine();
        if (header is null) return values;
        var columns = header.Split(',');
        var index = Array.FindIndex(columns, x => x.Trim().Trim('"').Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = Array.FindIndex(columns, x => x.Trim().Trim('"').Equals("MsBetweenDisplayChange", StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = Array.FindIndex(columns, x => x.Trim().Trim('"').Equals("FrameTime", StringComparison.OrdinalIgnoreCase));
        if (index < 0) return values;
        while (!reader.EndOfStream)
        {
            var fields = (reader.ReadLine() ?? "").Split(',');
            if (index >= fields.Length) continue;
            if (double.TryParse(fields[index].Trim().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) && ms > 0 && ms < 1000)
                values.Add(ms);
        }
        return values;
    }

    private void RefreshBenchmarkHistory()
    {
        var records = LoadBenchmarks();
        _results.Text = records.Count == 0 ? "Zatím žádné uložené testy." :
            string.Join(Environment.NewLine, records.Select((x, i) => $"{i + 1}. {x.Date:dd.MM.yyyy HH:mm} · {x.Process} · průměr {x.AverageFps:0.0} FPS · 1% low {x.OnePercentLow:0.0} FPS · {x.DurationSeconds}s"));
    }

    private static List<FpsRecord> LoadBenchmarks()
    {
        try
        {
            if (!File.Exists(BenchmarkPath)) return new List<FpsRecord>();
            return JsonSerializer.Deserialize<List<FpsRecord>>(File.ReadAllText(BenchmarkPath)) ?? new List<FpsRecord>();
        }
        catch { return new List<FpsRecord>(); }
    }

    private static string LoadPresentMonPath()
    {
        var file = Path.Combine(DataFolder, "presentmon-path.txt");
        try { return File.Exists(file) ? File.ReadAllText(file).Trim() : ""; } catch { return ""; }
    }

    private static void SavePresentMonPath(string path)
    {
        Directory.CreateDirectory(DataFolder);
        File.WriteAllText(Path.Combine(DataFolder, "presentmon-path.txt"), path);
    }

    private static string RunPowerCfg(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("powercfg.exe", arguments)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        }) ?? throw new InvalidOperationException("powercfg.exe se nepodařilo spustit.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        return output;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        IsEnabled = !busy;
        _scanStatus.Opacity = busy ? 0.7 : 1;
    }

    private Border Card()
    {
        return new Border
        {
            Background = Brush("#121419"), BorderBrush = Brush("#24272E"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12)
        };
    }

    private static TextBlock Heading(string text) => new()
    {
        Text = text, FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Brush("#FF9A52"),
        Margin = new Thickness(0, 0, 0, 7)
    };

    private static TextBlock Body(string text) => new()
    {
        Text = text, FontSize = 12, Foreground = Brush("#A8ADB7"), TextWrapping = TextWrapping.Wrap
    };

    private static Button ActionButton(string text) => new()
    {
        Content = text, Background = Brush("#FF6A00"), Foreground = Brushes.White, BorderThickness = new Thickness(0),
        Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 10, 0, 0), Cursor = System.Windows.Input.Cursors.Hand
    };

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private sealed class Snapshot
    {
        public string? PowerScheme { get; set; }
        public List<RegistrySnapshot> RegistryValues { get; set; } = new();
    }

    private sealed class RegistrySnapshot
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Exists { get; set; }
        public string Kind { get; set; } = "String";
        public string Data { get; set; } = "";
    }

    private sealed class FpsRecord
    {
        public DateTime Date { get; set; }
        public string Process { get; set; } = "";
        public int DurationSeconds { get; set; }
        public double AverageFps { get; set; }
        public double OnePercentLow { get; set; }
        public int Frames { get; set; }
    }
}
