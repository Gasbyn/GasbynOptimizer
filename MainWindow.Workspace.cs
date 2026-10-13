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
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using GasbynOptimizer.Services;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private static readonly string WorkspaceDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer");
    private static readonly string WorkspaceStatePath = Path.Combine(WorkspaceDataDirectory, "optimization-state.json");
    private static readonly string WorkspaceBenchmarkPath = Path.Combine(WorkspaceDataDirectory, "fps-tests.json");
    private static readonly string WorkspacePresentMonPath = Path.Combine(WorkspaceDataDirectory, "presentmon-path.txt");

    private readonly Dictionary<string, ToggleButton> _workspaceSwitches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> _workspaceSwitchStatuses = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingWorkspaceSwitches;
    private bool _workspaceTweakBusy;
    private TextBlock? _workspaceBenchmarkStatus;
    private TextBlock? _workspaceBenchmarkHistory;
    private TextBox? _workspacePresentMonPath;
    private TextBox? _workspaceBenchmarkProcess;
    private Button? _workspaceRunBenchmarkButton;

    private void BuildTweaksWorkspace()
    {
        _workspaceSwitches.Clear();
        _workspaceSwitchStatuses.Clear();

        AddWorkspaceTweakCard(
            "Windows Game Mode",
            "Povolí herní režim Windows. Jeho přínos se liší podle PC a hry; FPS není garantováno.",
            "game-mode",
            "ON: uloží původní hodnoty a nastaví AutoGameModeEnabled=1 a AllowAutoGameMode=1. OFF: obnoví původní hodnoty včetně původně neexistujících hodnot.");

        AddWorkspaceTweakCard(
            "Vypnout Game DVR na pozadí",
            "Omezí zachytávání a nahrávání Windows na pozadí. Může vypnout nahrávání klipů přes Xbox Game Bar.",
            "game-dvr",
            "ON: nastaví GameDVR_Enabled=0 a AppCaptureEnabled=0. OFF: vrátí přesné původní hodnoty.");

        AddWorkspaceTweakCard(
            "Výkonové vizuální efekty",
            "Přepne Windows do režimu nejlepšího výkonu. Některé animace a vizuální efekty se mohou změnit.",
            "visual-effects",
            "ON: nastaví VisualFXSetting=2. OFF: obnoví původní režim nebo hodnotu odstraní, pokud původně nebyla nastavena.");

        AddWorkspaceTweakCard(
            "High Performance power plan",
            "Aktivuje vestavěný plán Vysoký výkon. Může zvýšit spotřebu energie a teploty.",
            "power-plan",
            "ON: spustí powercfg.exe /setactive SCHEME_MIN. OFF: aktivuje původní ID plánu uložené před změnou.");

        AddWorkspaceTweakCard(
            "Vypnout efekty průhlednosti",
            "Vypne průhledné prvky Windows. Jde hlavně o změnu vzhledu, nikoliv o garantované herní FPS.",
            "transparency",
            "ON: nastaví EnableTransparency=0. OFF: obnoví původní hodnotu nebo odstraní novou hodnotu, pokud dříve neexistovala.");

        AddWorkspaceTweakCard(
            "Rychlejší nabídky a animace oken",
            "Zkrátí prodlevu nabídek a vypne animaci minimalizace/maximalizace. Změní chování uživatelského rozhraní.",
            "fast-menus",
            "ON: nastaví MenuShowDelay=100 a MinAnimate=0. OFF: obnoví původní hodnoty.");

        var hw = _hardwareInventory;
        var note = hw is null
            ? "Hardware nebyl zatím načten. Obecné změny zůstávají dostupné; ovladačové a BIOS zásahy se neprovádějí."
            : hw.BuildRecommendations(OptimizerPreferencesStore.Load().AdvancedRecommendations);
        AddInfoCard("HARDWARE-AWARE DOPORUČENÍ", note, "Hardware Detection");
        AddInfoCard("Gaming audit", "Zobrazí aktuální Game Mode, HAGS, plán napájení a související herní konfiguraci bez automatických změn.", "GAMING OPTIMIZER");

        _ = RefreshWorkspaceTweakStatesAsync();
    }

    private void AddWorkspaceTweakCard(string title, string description, string id, string tooltip)
    {
        var card = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 12),
            ToolTip = tooltip
        };
        ToolTipService.SetShowDuration(card, 30000);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)FindResource("DisplayFont"),
            Foreground = (Brush)FindResource("OrangeLight"),
            ToolTip = tooltip
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMuted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0),
            ToolTip = tooltip
        });
        var status = new TextBlock
        {
            Text = "Načítám aktuální stav…",
            FontSize = 11,
            Foreground = (Brush)FindResource("TextDim"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 9, 0, 0)
        };
        _workspaceSwitchStatuses[id] = status;
        text.Children.Add(status);
        Grid.SetColumn(text, 0);
        root.Children.Add(text);

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("SwitchToggleButton"),
            IsChecked = false,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = tooltip,
            AutomationProperties = { Name = title }
        };
        toggle.Checked += async (_, _) =>
        {
            if (!_loadingWorkspaceSwitches)
                await ChangeWorkspaceTweakAsync(id, true, toggle, status);
        };
        toggle.Unchecked += async (_, _) =>
        {
            if (!_loadingWorkspaceSwitches)
                await ChangeWorkspaceTweakAsync(id, false, toggle, status);
        };
        _workspaceSwitches[id] = toggle;
        Grid.SetColumn(toggle, 1);
        root.Children.Add(toggle);
        card.Child = root;
        SecondaryContent.Children.Add(card);
    }

    private async Task RefreshWorkspaceTweakStatesAsync()
    {
        var ids = _workspaceSwitchStatuses.Keys.ToArray();
        if (ids.Length == 0) return;

        Dictionary<string, WorkspaceTweakSnapshot> state;
        Dictionary<string, string> actual;
        try
        {
            var scan = await Task.Run(() =>
            {
                var loaded = LoadWorkspaceState();
                var statuses = ids.ToDictionary(id => id, ReadWorkspaceActualStatus, StringComparer.OrdinalIgnoreCase);
                return (loaded, statuses);
            });
            state = scan.loaded;
            actual = scan.statuses;
        }
        catch (Exception ex)
        {
            foreach (var status in _workspaceSwitchStatuses.Values)
                status.Text = "Stav není dostupný: " + ex.Message;
            return;
        }

        _loadingWorkspaceSwitches = true;
        try
        {
            foreach (var id in ids)
            {
                var active = state.ContainsKey(id);
                if (_workspaceSwitches.TryGetValue(id, out var toggle))
                    toggle.IsChecked = active;
                if (_workspaceSwitchStatuses.TryGetValue(id, out var status))
                {
                    status.Text = $"Windows: {actual.GetValueOrDefault(id, "stav není dostupný")}  ·  Správa Gasbyn Optimizerem: {(active ? "ON — původní stav uložen" : "OFF")}";
                    status.Foreground = active
                        ? (Brush)FindResource("OrangeLight")
                        : (Brush)FindResource("TextDim");
                }
            }
        }
        finally { _loadingWorkspaceSwitches = false; }
    }

    private async Task ChangeWorkspaceTweakAsync(string id, bool enable, ToggleButton toggle, TextBlock status)
    {
        if (_workspaceTweakBusy)
        {
            _loadingWorkspaceSwitches = true;
            toggle.IsChecked = !enable;
            _loadingWorkspaceSwitches = false;
            return;
        }

        _workspaceTweakBusy = true;
        foreach (var item in _workspaceSwitches.Values) item.IsEnabled = false;
        status.Text = enable ? "Ukládám původní stav a aplikuji optimalizaci…" : "Obnovuji původní hodnoty…";
        try
        {
            await Task.Run(() => ApplyWorkspaceTweak(id, enable));
            await RefreshWorkspaceTweakStatesAsync();
            status.Text = enable
                ? $"Optimalizace zapnuta. Původní stav je uložen. Pro plné použití změn doporučuji restart PC. · {ReadWorkspaceActualStatus(id)}"
                : $"Původní hodnoty obnoveny. · {ReadWorkspaceActualStatus(id)}";
        }
        catch (Exception ex)
        {
            _loadingWorkspaceSwitches = true;
            toggle.IsChecked = !enable;
            _loadingWorkspaceSwitches = false;
            status.Text = "Nepodařilo se změnit nastavení: " + ex.Message;
            status.Foreground = Brushes.OrangeRed;
            OptimizerAuditLog.WriteChange(id, WorkspaceTweakName(id), enable ? "ENABLE" : "RESTORE", false,
                new[] { "WORKSPACE SWITCH requested " + (enable ? "ON" : "OFF") }, ex.ToString());
        }
        finally
        {
            foreach (var item in _workspaceSwitches.Values) item.IsEnabled = true;
            _workspaceTweakBusy = false;
        }
    }

    private static string WorkspaceTweakName(string id) => id switch
    {
        "game-mode" => "Windows Game Mode",
        "game-dvr" => "Vypnout Game DVR na pozadí",
        "visual-effects" => "Výkonové vizuální efekty",
        "power-plan" => "High Performance power plan",
        "transparency" => "Vypnout efekty průhlednosti",
        "fast-menus" => "Rychlejší nabídky a animace oken",
        _ => id
    };

    private static void ApplyWorkspaceTweak(string id, bool enable)
    {
        Directory.CreateDirectory(WorkspaceDataDirectory);
        var state = LoadWorkspaceState();
        var operations = new List<string>();
        WorkspaceTweakSnapshot? snapshot = null;
        var name = WorkspaceTweakName(id);
        var action = enable ? "ENABLE" : "RESTORE";

        try
        {
            if (enable)
            {
                if (!state.TryGetValue(id, out snapshot))
                {
                    snapshot = CaptureWorkspaceTweak(id);
                    state[id] = snapshot;
                    SaveWorkspaceState(state);
                    AddWorkspaceBackupLog(operations, snapshot);
                }
                else
                {
                    operations.Add("BACKUP REUSED: původní stav z prvního zapnutí zůstává zachován.");
                }

                switch (id)
                {
                    case "game-mode":
                        SetWorkspaceDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, operations);
                        SetWorkspaceDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, operations);
                        break;
                    case "game-dvr":
                        SetWorkspaceDword(@"System\GameConfigStore", "GameDVR_Enabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, operations);
                        break;
                    case "visual-effects":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, operations);
                        break;
                    case "power-plan":
                        operations.Add("COMMAND: powercfg.exe /setactive SCHEME_MIN");
                        operations.Add("COMMAND OUTPUT: " + RunWorkspacePowerCfg("/setactive SCHEME_MIN").Trim());
                        break;
                    case "transparency":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, operations);
                        break;
                    case "fast-menus":
                        SetWorkspaceString(@"Control Panel\Desktop", "MenuShowDelay", "100", operations);
                        SetWorkspaceString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", operations);
                        break;
                    default: throw new InvalidOperationException("Neznámý optimalizační modul: " + id);
                }

                OptimizerAuditLog.WriteChange(id, name, action, true, operations,
                    "Optimalizace byla aplikována. Původní hodnoty byly uloženy pro návrat.");
            }
            else
            {
                if (!state.TryGetValue(id, out snapshot))
                    throw new InvalidOperationException("Není uložený původní stav. Tento tweak nebyl zapnutý přes Gasbyn Optimizer.");
                RestoreWorkspaceTweak(id, snapshot, operations);
                state.Remove(id);
                SaveWorkspaceState(state);
                OptimizerAuditLog.WriteChange(id, name, action, true, operations, "Původní hodnoty byly obnoveny.");
            }
        }
        catch (Exception ex)
        {
            if (enable && snapshot is not null)
            {
                try
                {
                    operations.Add("ROLLBACK: vracím zachycené hodnoty po chybě.");
                    RestoreWorkspaceTweak(id, snapshot, operations);
                    state.Remove(id);
                    SaveWorkspaceState(state);
                }
                catch (Exception rollbackException)
                {
                    operations.Add("ROLLBACK ERROR: " + rollbackException);
                }
            }
            operations.Add("ERROR: " + ex);
            OptimizerAuditLog.WriteChange(id, name, action, false, operations, ex.ToString());
            throw;
        }
    }

    private static WorkspaceTweakSnapshot CaptureWorkspaceTweak(string id)
    {
        var snapshot = new WorkspaceTweakSnapshot();
        if (id == "power-plan")
        {
            var output = RunWorkspacePowerCfg("/getactivescheme");
            var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
            if (!match.Success) throw new InvalidOperationException("Nepodařilo se zjistit ID aktivního plánu napájení.");
            snapshot.PowerScheme = match.Value;
            return snapshot;
        }

        var values = id switch
        {
            "game-mode" => new[]
            {
                (Path: @"Software\Microsoft\GameBar", Name: "AutoGameModeEnabled"),
                (Path: @"Software\Microsoft\GameBar", Name: "AllowAutoGameMode")
            },
            "game-dvr" => new[]
            {
                (Path: @"System\GameConfigStore", Name: "GameDVR_Enabled"),
                (Path: @"Software\Microsoft\Windows\CurrentVersion\GameDVR", Name: "AppCaptureEnabled")
            },
            "visual-effects" => new[]
            {
                (Path: @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", Name: "VisualFXSetting")
            },
            "transparency" => new[]
            {
                (Path: @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", Name: "EnableTransparency")
            },
            "fast-menus" => new[]
            {
                (Path: @"Control Panel\Desktop", Name: "MenuShowDelay"),
                (Path: @"Control Panel\Desktop\WindowMetrics", Name: "MinAnimate")
            },
            _ => throw new InvalidOperationException("Neznámý optimalizační modul: " + id)
        };

        foreach (var item in values)
        {
            using var key = Registry.CurrentUser.OpenSubKey(item.Path);
            var value = key?.GetValue(item.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            snapshot.RegistryValues.Add(new WorkspaceTweakRegistrySnapshot
            {
                Path = item.Path, Name = item.Name, Exists = value is not null,
                Kind = value is null ? RegistryValueKind.String.ToString() : key!.GetValueKind(item.Name).ToString(),
                Data = value is null ? "" : SerializeWorkspaceRegistryValue(value)
            });
        }
        return snapshot;
    }

    private static string SerializeWorkspaceRegistryValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        string[] strings => string.Join("|", strings),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static void RestoreWorkspaceTweak(string id, WorkspaceTweakSnapshot snapshot, List<string> operations)
    {
        if (id == "power-plan")
        {
            if (string.IsNullOrWhiteSpace(snapshot.PowerScheme))
                throw new InvalidOperationException("Záloha plánu napájení je neplatná.");
            operations.Add("COMMAND: powercfg.exe /setactive " + snapshot.PowerScheme);
            operations.Add("COMMAND OUTPUT: " + RunWorkspacePowerCfg("/setactive " + snapshot.PowerScheme).Trim());
            return;
        }

        foreach (var item in snapshot.RegistryValues)
        {
            var fullName = $"HKCU\\{item.Path}\\{item.Name}";
            if (item.Exists)
            {
                operations.Add($"REGISTRY RESTORE: {fullName}; Type={item.Kind}; OriginalValue={item.Data}");
                using var key = Registry.CurrentUser.CreateSubKey(item.Path)
                    ?? throw new InvalidOperationException("Nepodařilo se obnovit " + fullName);
                var kind = Enum.TryParse<RegistryValueKind>(item.Kind, out var parsed) ? parsed : RegistryValueKind.String;
                object value = kind switch
                {
                    RegistryValueKind.DWord => int.Parse(item.Data, CultureInfo.InvariantCulture),
                    RegistryValueKind.QWord => long.Parse(item.Data, CultureInfo.InvariantCulture),
                    RegistryValueKind.Binary => Convert.FromBase64String(item.Data),
                    RegistryValueKind.MultiString => item.Data.Split('|'),
                    _ => item.Data
                };
                key.SetValue(item.Name, value, kind);
            }
            else
            {
                operations.Add("REGISTRY DELETE: " + fullName + "; hodnota před zapnutím neexistovala.");
                using var key = Registry.CurrentUser.OpenSubKey(item.Path, writable: true);
                key?.DeleteValue(item.Name, throwOnMissingValue: false);
            }
        }
    }

    private static void SetWorkspaceDword(string path, string name, int value, List<string> operations)
    {
        operations.Add($"REGISTRY SET: HKCU\\{path}\\{name}; Type=REG_DWORD; Value={value}");
        using var key = Registry.CurrentUser.CreateSubKey(path) ?? throw new InvalidOperationException("Nepodařilo se otevřít HKCU\\" + path);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void SetWorkspaceString(string path, string name, string value, List<string> operations)
    {
        operations.Add($"REGISTRY SET: HKCU\\{path}\\{name}; Type=REG_SZ; Value={value}");
        using var key = Registry.CurrentUser.CreateSubKey(path) ?? throw new InvalidOperationException("Nepodařilo se otevřít HKCU\\" + path);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static Dictionary<string, WorkspaceTweakSnapshot> LoadWorkspaceState()
    {
        try
        {
            if (!File.Exists(WorkspaceStatePath))
                return new Dictionary<string, WorkspaceTweakSnapshot>(StringComparer.OrdinalIgnoreCase);
            return JsonSerializer.Deserialize<Dictionary<string, WorkspaceTweakSnapshot>>(File.ReadAllText(WorkspaceStatePath))
                ?? new Dictionary<string, WorkspaceTweakSnapshot>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            throw new InvalidOperationException("Soubor se zálohami optimalizací nejde přečíst. Před další změnou zkontroluj zálohy ve workspace.");
        }
    }

    private static void SaveWorkspaceState(Dictionary<string, WorkspaceTweakSnapshot> state)
    {
        Directory.CreateDirectory(WorkspaceDataDirectory);
        var temp = WorkspaceStatePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, WorkspaceStatePath, overwrite: true);
    }

    private static void AddWorkspaceBackupLog(List<string> operations, WorkspaceTweakSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.PowerScheme))
            operations.Add("BACKUP POWER PLAN: original scheme ID=" + snapshot.PowerScheme);
        foreach (var item in snapshot.RegistryValues)
            operations.Add($"BACKUP REGISTRY: HKCU\\{item.Path}\\{item.Name}; Exists={item.Exists}; Kind={item.Kind}; Data={item.Data}");
    }

    private static string ReadWorkspaceActualStatus(string id)
    {
        try
        {
            if (id == "power-plan")
            {
                var output = RunWorkspacePowerCfg("/getactivescheme");
                var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
                return match.Success ? (match.Value.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase)
                    ? "High Performance" : "aktivní plán " + match.Value) : output.Trim();
            }

            object? Read(string path, string name)
            {
                using var key = Registry.CurrentUser.OpenSubKey(path);
                return key?.GetValue(name);
            }

            return id switch
            {
                "game-mode" => Read(@"Software\Microsoft\GameBar", "AutoGameModeEnabled") is int mode && mode == 1
                    ? "Game Mode zapnutý" : "Game Mode výchozí/vypnutý",
                "game-dvr" => Read(@"System\GameConfigStore", "GameDVR_Enabled") is int capture && capture == 0 &&
                              Read(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") is int app && app == 0
                    ? "Game DVR vypnutý" : "Game DVR výchozí/zapnutý",
                "visual-effects" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting") is int visual && visual == 2
                    ? "režim nejlepšího výkonu" : "výchozí/jiný režim",
                "transparency" => Read(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency") is int transparency && transparency == 0
                    ? "průhlednost vypnutá" : "průhlednost výchozí/zapnutá",
                "fast-menus" => $"MenuShowDelay={Read(@"Control Panel\Desktop", "MenuShowDelay") ?? "default"}, MinAnimate={Read(@"Control Panel\Desktop\WindowMetrics", "MinAnimate") ?? "default"}",
                _ => "stav není dostupný"
            };
        }
        catch (Exception ex) { return "stav nelze přečíst: " + ex.Message; }
    }

    private static string RunWorkspacePowerCfg(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("powercfg.exe", arguments)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        }) ?? throw new InvalidOperationException("powercfg.exe se nepodařilo spustit.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        return stdout;
    }

    private sealed class WorkspaceTweakSnapshot
    {
        public string? PowerScheme { get; set; }
        public List<WorkspaceTweakRegistrySnapshot> RegistryValues { get; set; } = new();
    }

    private sealed class WorkspaceTweakRegistrySnapshot
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Exists { get; set; }
        public string Kind { get; set; } = "String";
        public string Data { get; set; } = "";
    }

    private void BuildBenchmarkWorkspace()
    {
        var card = WorkspaceCard();
        card.Child = new StackPanel();
        var stack = (StackPanel)card.Child;
        stack.Children.Add(WorkspaceHeading("FPS BENCHMARK"));
        stack.Children.Add(WorkspaceBody("Záznam snímků přes oficiální PresentMon. Během 60s testu nech hru běžet a neměň nastavení. Výsledek slouží ke srovnání ve stejných podmínkách; neprokazuje sám o sobě příčinný vliv tweaku."));

        var pathRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _workspacePresentMonPath = new TextBox
        {
            Text = LoadWorkspacePresentMonPath(), Height = 34, Padding = new Thickness(9),
            VerticalContentAlignment = VerticalAlignment.Center, Background = WorkspaceBrush("#0D0F13"),
            Foreground = WorkspaceBrush("#F4F4F5"), BorderBrush = WorkspaceBrush("#343842")
        };
        _workspacePresentMonPath.ToolTip = "Cesta k oficiálnímu PresentMon.exe";
        Grid.SetColumn(_workspacePresentMonPath, 0);
        pathRow.Children.Add(_workspacePresentMonPath);
        var browse = WorkspaceButton("Vybrat PresentMon.exe");
        browse.Margin = new Thickness(8, 0, 0, 0);
        browse.Click += (_, _) =>
        {
            var dialog = new OpenFileDialog
            {
                Title = "Vyber PresentMon.exe",
                Filter = "PresentMon executable|PresentMon.exe;PresentMon-*.exe|EXE files|*.exe"
            };
            if (dialog.ShowDialog(this) == true)
            {
                _workspacePresentMonPath.Text = dialog.FileName;
                SaveWorkspacePresentMonPath(dialog.FileName);
            }
        };
        Grid.SetColumn(browse, 1);
        pathRow.Children.Add(browse);
        stack.Children.Add(pathRow);

        var processRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        processRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        processRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _workspaceBenchmarkProcess = new TextBox
        {
            Text = "FortniteClient-Win64-Shipping.exe", Height = 34, Padding = new Thickness(9),
            VerticalContentAlignment = VerticalAlignment.Center, Background = WorkspaceBrush("#0D0F13"),
            Foreground = WorkspaceBrush("#F4F4F5"), BorderBrush = WorkspaceBrush("#343842")
        };
        var label = WorkspaceBody("Proces:");
        label.Margin = new Thickness(0, 0, 10, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        processRow.Children.Add(label);
        Grid.SetColumn(_workspaceBenchmarkProcess, 1);
        processRow.Children.Add(_workspaceBenchmarkProcess);
        stack.Children.Add(processRow);

        _workspaceRunBenchmarkButton = WorkspaceButton("Spustit 60s test");
        _workspaceRunBenchmarkButton.Margin = new Thickness(0, 12, 0, 0);
        _workspaceRunBenchmarkButton.Click += async (_, _) =>
            await RunWorkspaceFpsTestAsync(
                _workspacePresentMonPath?.Text ?? "",
                _workspaceBenchmarkProcess?.Text ?? "",
                _workspaceBenchmarkStatus!,
                _workspaceBenchmarkHistory!,
                _workspaceRunBenchmarkButton);
        stack.Children.Add(_workspaceRunBenchmarkButton);

        _workspaceBenchmarkStatus = WorkspaceBody("Připraveno.");
        _workspaceBenchmarkStatus.Margin = new Thickness(0, 10, 0, 0);
        _workspaceBenchmarkStatus.Foreground = (Brush)FindResource("OrangeLight");
        stack.Children.Add(_workspaceBenchmarkStatus);
        _workspaceBenchmarkHistory = WorkspaceBody("");
        _workspaceBenchmarkHistory.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(_workspaceBenchmarkHistory);
        var refresh = WorkspaceButton("Obnovit historii");
        refresh.Margin = new Thickness(0, 10, 0, 0);
        refresh.Click += (_, _) => RefreshWorkspaceBenchmarkHistory();
        stack.Children.Add(refresh);
        SecondaryContent.Children.Add(card);
        RefreshWorkspaceBenchmarkHistory();
    }

    private async Task RunWorkspaceFpsTestAsync(string exePath, string processName, TextBlock status, TextBlock history, Button button)
    {
        if (button is null) return;
        if (!File.Exists(exePath))
        {
            status.Text = "Nejdřív vyber platný PresentMon.exe.";
            return;
        }
        if (string.IsNullOrWhiteSpace(processName) || processName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            status.Text = "Zadej platný název procesu, například FortniteClient-Win64-Shipping.exe.";
            return;
        }

        button.IsEnabled = false;
        status.Text = "Spouštím měření…";
        try
        {
            SaveWorkspacePresentMonPath(exePath);
            var folder = Path.Combine(WorkspaceDataDirectory, "FpsTests");
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
                status.Text = $"Měření běží… zbývá {seconds} s. Nech hru běžet.";
                await Task.Delay(1000);
                if (process.HasExited) break;
            }
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync(); } catch { }
            await Task.Delay(800);

            var frameTimes = ParseWorkspaceFrameTimes(output);
            if (frameTimes.Count < 30)
                throw new InvalidOperationException("PresentMon nezapsal dost snímků. Ověř název procesu, nech hru běžet a případně spusť PresentMon jako správce.");

            var sorted = frameTimes.OrderBy(x => x).ToArray();
            var averageFps = 1000d / frameTimes.Average();
            var p99 = sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * 0.99) - 1, 0, sorted.Length - 1)];
            var record = new WorkspaceFpsRecord
            {
                Date = DateTime.Now,
                Process = processName,
                DurationSeconds = 60,
                AverageFps = Math.Round(averageFps, 1),
                OnePercentLow = Math.Round(p99 > 0 ? 1000d / p99 : 0, 1),
                Frames = frameTimes.Count
            };
            var records = LoadWorkspaceBenchmarks();
            records.Insert(0, record);
            File.WriteAllText(WorkspaceBenchmarkPath, JsonSerializer.Serialize(records.Take(15).ToList(), new JsonSerializerOptions { WriteIndented = true }));
            status.Text = $"Hotovo: průměr {record.AverageFps:0.0} FPS · 1% low přibližně {record.OnePercentLow:0.0} FPS · {record.Frames} snímků.";
            RefreshWorkspaceBenchmarkHistory();
        }
        catch (Exception ex)
        {
            status.Text = "Test selhal: " + ex.Message;
        }
        finally { button.IsEnabled = true; }
    }

    private static List<double> ParseWorkspaceFrameTimes(string csvPath)
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
            if (index < fields.Length &&
                double.TryParse(fields[index].Trim().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) &&
                ms > 0 && ms < 1000)
                values.Add(ms);
        }
        return values;
    }

    private void RefreshWorkspaceBenchmarkHistory()
    {
        if (_workspaceBenchmarkHistory is null) return;
        var records = LoadWorkspaceBenchmarks();
        _workspaceBenchmarkHistory.Text = records.Count == 0
            ? "Zatím žádné uložené testy. Historie ponechává maximálně 15 posledních měření."
            : string.Join(Environment.NewLine, records.Take(15).Select((x, i) =>
                $"{i + 1}. {x.Date:dd.MM.yyyy HH:mm} · {x.Process} · průměr {x.AverageFps:0.0} FPS · 1% low {x.OnePercentLow:0.0} FPS · {x.DurationSeconds}s"));
    }

    private static List<WorkspaceFpsRecord> LoadWorkspaceBenchmarks()
    {
        try
        {
            if (!File.Exists(WorkspaceBenchmarkPath)) return new List<WorkspaceFpsRecord>();
            return JsonSerializer.Deserialize<List<WorkspaceFpsRecord>>(File.ReadAllText(WorkspaceBenchmarkPath))
                ?? new List<WorkspaceFpsRecord>();
        }
        catch { return new List<WorkspaceFpsRecord>(); }
    }

    private static string LoadWorkspacePresentMonPath()
    {
        try { return File.Exists(WorkspacePresentMonPath) ? File.ReadAllText(WorkspacePresentMonPath).Trim() : ""; }
        catch { return ""; }
    }

    private static void SaveWorkspacePresentMonPath(string path)
    {
        Directory.CreateDirectory(WorkspaceDataDirectory);
        File.WriteAllText(WorkspacePresentMonPath, path);
    }

    private void BuildBackupWorkspace()
    {
        var card = WorkspaceCard();
        var stack = new StackPanel();
        stack.Children.Add(WorkspaceHeading("WINDOWS RESTORE POINT"));
        stack.Children.Add(WorkspaceBody("Požádá Windows o vytvoření bodu obnovení před systémovými změnami. Vyžaduje zapnutou Ochranu systému a oprávnění správce; Windows může omezit četnost vytváření bodů."));
        var status = WorkspaceBody("Bod obnovení ještě nebyl vytvořen z této obrazovky.");
        status.Margin = new Thickness(0, 10, 0, 0);
        status.TextWrapping = TextWrapping.Wrap;
        var button = WorkspaceButton("Vytvořit bod obnovení");
        button.Margin = new Thickness(0, 12, 0, 0);
        button.Click += async (_, _) => await CreateWorkspaceRestorePointAsync(button, status);
        stack.Children.Add(button);
        stack.Children.Add(status);
        card.Child = stack;
        SecondaryContent.Children.Add(card);

        AddInfoCard("Tweak Backups", "Otevře složku záloh a soubor s uloženými původními hodnotami.", "Tweak Backups");
        AddInfoCard("Restore All", "Zobrazí bezpečný postup pro obnovení aktivních tweaků.", "Restore All");
        AddInfoCard("Change History", "Zobrazí historii zaznamenaných změn.", "Change History");
    }

    private async Task CreateWorkspaceRestorePointAsync(Button button, TextBlock status)
    {
        button.IsEnabled = false;
        status.Text = "Žádám Windows o vytvoření bodu obnovení…";
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
                using var process = Process.Start(psi) ?? throw new InvalidOperationException("PowerShell se nepodařilo spustit.");
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Windows bod obnovení nevytvořil. Ověř Ochranu systému; Windows může povolit jen jeden bod za 24 hodin.");
            });
            status.Text = "Příkaz byl dokončen. Ověř bod obnovení ve Windows v nástroji Ochrana systému.";
        }
        catch (Exception ex) { status.Text = "Bod obnovení selhal: " + ex.Message; }
        finally { button.IsEnabled = true; }
    }

    private static SolidColorBrush WorkspaceBrush(string hex) =>
        new((Color)ColorConverter.ConvertFromString(hex));

    private Border WorkspaceCard() => new()
    {
        Background = (Brush)FindResource("Panel"),
        BorderBrush = (Brush)FindResource("Border"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(20),
        Margin = new Thickness(0, 0, 0, 12)
    };

    private TextBlock WorkspaceHeading(string value) => new()
    {
        Text = value, FontSize = 22, FontWeight = FontWeights.Bold,
        FontFamily = (FontFamily)FindResource("DisplayFont"),
        Foreground = (Brush)FindResource("OrangeLight"),
        Margin = new Thickness(0, 0, 0, 7)
    };

    private TextBlock WorkspaceBody(string value) => new()
    {
        Text = value, FontSize = 12, Foreground = (Brush)FindResource("TextMuted"),
        TextWrapping = TextWrapping.Wrap
    };

    private Button WorkspaceButton(string text) => new()
    {
        Content = text, Background = (Brush)FindResource("Orange"), Foreground = Brushes.White,
        BorderThickness = new Thickness(0), Padding = new Thickness(14, 9, 14, 9),
        Cursor = System.Windows.Input.Cursors.Hand
    };

    private sealed class WorkspaceFpsRecord
    {
        public DateTime Date { get; set; }
        public string Process { get; set; } = "";
        public int DurationSeconds { get; set; }
        public double AverageFps { get; set; }
        public double OnePercentLow { get; set; }
        public int Frames { get; set; }
    }
}
