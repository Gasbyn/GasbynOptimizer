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

    private void BuildTweaksWorkspace(bool gamingOnly = false)
    {
        _workspaceSwitches.Clear();
        _workspaceSwitchStatuses.Clear();

        if (gamingOnly)
        {
            AddWorkspaceTweakCard(
                "Windows Game Mode",
                "Povolí herní režim Windows. Jeho přínos se liší podle PC a hry; FPS není garantováno.",
                "game-mode",
                "ON: uloží původní hodnoty a nastaví AutoGameModeEnabled=1 a AllowAutoGameMode=1. OFF: obnoví původní hodnoty včetně původně neexistujících hodnot.", "DOPORUČENO", "FPS", "GAMING");

            AddWorkspaceTweakCard(
                "Vypnout Game DVR na pozadí",
                "Omezí zachytávání a nahrávání Windows na pozadí. Může vypnout nahrávání klipů přes Xbox Game Bar.",
                "game-dvr",
                "ON: nastaví GameDVR_Enabled=0 a AppCaptureEnabled=0. OFF: vrátí přesné původní hodnoty.", "PODMÍNĚNĚ", "FPS", "ZÁZNAMY");

            _ = RefreshWorkspaceTweakStatesAsync();
            return;
        }

        AddWorkspaceTweakCard(
            "Výkonové vizuální efekty",
            "Přepne Windows do režimu nejlepšího výkonu. Některé animace a vizuální efekty se mohou změnit.",
            "visual-effects",
            "ON: nastaví VisualFXSetting=2. OFF: obnoví původní režim nebo hodnotu odstraní, pokud původně nebyla nastavena.", "VOLITELNÉ", "VZHLED", "ODEZVA");

        AddWorkspaceTweakCard(
            "High Performance power plan",
            "Aktivuje vestavěný plán Vysoký výkon. Může zvýšit spotřebu energie a teploty.",
            "power-plan",
            "ON: spustí powercfg.exe /setactive SCHEME_MIN. OFF: aktivuje původní ID plánu uložené před změnou.", "POKROČILÉ", "VÝKON", "SPOTŘEBA");

        AddWorkspaceTweakCard(
            "Vypnout efekty průhlednosti",
            "Vypne průhledné prvky Windows. Jde hlavně o změnu vzhledu, nikoliv o garantované herní FPS.",
            "transparency",
            "ON: nastaví EnableTransparency=0. OFF: obnoví původní hodnotu nebo odstraní novou hodnotu, pokud dříve neexistovala.", "VOLITELNÉ", "VZHLED");

        AddWorkspaceTweakCard(
            "Rychlejší nabídky a animace oken",
            "Zkrátí prodlevu nabídek a vypne animaci minimalizace/maximalizace. Změní chování uživatelského rozhraní.",
            "fast-menus",
            "ON: nastaví MenuShowDelay=100 a MinAnimate=0. OFF: obnoví původní hodnoty.", "VOLITELNÉ", "ODEZVA", "UI");

        var hw = _hardwareInventory;
        var advanced = OptimizerPreferencesStore.Load().AdvancedRecommendations;
        var note = hw is null
            ? "Hardware nebyl zatím načten. Obecné změny zůstávají dostupné; ovladačové a BIOS zásahy se neprovádějí."
            : hw.BuildRecommendations(advanced);
        AddInfoCard("HARDWARE-AWARE DOPORUČENÍ", note, "Hardware Detection");
        AddAmdCompatibilityInfo(hw, advanced);
        _ = RefreshWorkspaceTweakStatesAsync();
    }

    private void AddAmdCompatibilityInfo(GasbynOptimizer.Services.HardwareInventory? hardware, bool advanced)
    {
        var warning = hardware is null
            ? "INFO · GPU nebyla ověřena, proto zůstává ovladačový modul nedostupný."
            : hardware.GetCompatibilityNote("amd-gpu-profile", advanced);
        if (hardware?.HasAmdGpu == true)
            warning = "INFO · AMD Radeon GPU detekována, ale zatím není nakonfigurována ověřená vratná změna ovladače; nic se nepřepíše.";

        var card = WorkspaceCard();
        card.Opacity = advanced ? 0.78 : 0.58;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        copy.Children.Add(WorkspaceHeading("AMD RADEON-ONLY GPU TUNING"));
        copy.Children.Add(WorkspaceBody("Tento modul je pouze indikátor kompatibility. Nepoužívá neověřené ovladačové, napěťové ani taktovací zásahy."));
        var status = WorkspaceBody(warning);
        status.Margin = new Thickness(0, 8, 0, 0);
        copy.Children.Add(status);
        Grid.SetColumn(copy, 0);
        root.Children.Add(copy);

        var unavailable = new ToggleButton
        {
            Style = (Style)FindResource("SwitchToggleButton"),
            IsChecked = false,
            IsEnabled = false,
            ToolTip = warning,
            VerticalAlignment = VerticalAlignment.Center
        };
        System.Windows.Automation.AutomationProperties.SetName(unavailable, "AMD Radeon-only GPU tuning — unavailable");
        Grid.SetColumn(unavailable, 1);
        root.Children.Add(unavailable);
        card.Child = root;
        SecondaryContent.Children.Add(card);
    }

    private void AddWorkspaceTweakCard(string title, string description, string id, string tooltip, params string[] badges)
    {
        var detailForHover = BuildTweakToolTip(title, description, tooltip);
        var card = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 12),
            ToolTip = detailForHover
        };
        ToolTipService.SetShowDuration(card, 30000);
        ToolTipService.SetInitialShowDelay(card, 400);

        card.MouseEnter += (_, _) =>
        {
            card.BorderBrush = new SolidColorBrush(Color.FromRgb(91, 54, 30));
        };
        card.MouseLeave += (_, _) =>
        {
            card.BorderBrush = (Brush)FindResource("Border");
        };

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        text.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)FindResource("DisplayFont"),
            Foreground = (Brush)FindResource("OrangeLight")
        });
        text.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = (Brush)FindResource("TextMuted"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });

        var badgePanel = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var badge in badges.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            badgePanel.Children.Add(CreateTweakBadge(badge));
        if (badgePanel.Children.Count > 0)
            text.Children.Add(badgePanel);

        var status = new TextBlock
        {
            Text = "Načítám aktuální stav…",
            FontSize = 11,
            Foreground = (Brush)FindResource("TextDim"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
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
            ToolTip = BuildTweakToolTip(title, description, tooltip)
        };
        ToolTipService.SetShowDuration(toggle, 30000);
        ToolTipService.SetInitialShowDelay(toggle, 400);
        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
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

    private Border CreateTweakBadge(string label)
    {
        var palette = label.Trim().ToUpperInvariant() switch
        {
            "DOPORUČENO" => ("#143126", "#285D45", "#67E0A8"),
            "PODMÍNĚNĚ" => ("#302516", "#644824", "#FFD08A"),
            "EXPERIMENTÁLNÍ" => ("#351C18", "#6D342A", "#FF9B87"),
            "POKROČILÉ" => ("#271D3A", "#4D3676", "#D0B5FF"),
            "NOVÉ" => ("#152B3D", "#2D5575", "#94D0FF"),
            "FPS" or "GAMING" or "VÝKON" => ("#21150E", "#4A2814", "#FF9A52"),
            "PING" or "INTERNET" or "DELAY" or "ODEZVA" or "SÍŤ" => ("#122434", "#2A4F6A", "#91CFFF"),
            "SPOTŘEBA" => ("#2D2116", "#55402A", "#DDBB90"),
            _ => ("#1A1D23", "#343842", "#B8BDC6")
        };

        var badge = new Border
        {
            Background = WorkspaceBrush(palette.Item1),
            BorderBrush = WorkspaceBrush(palette.Item2),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 0, 6, 5),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = label.Trim().ToUpperInvariant(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceBrush(palette.Item3),
            }
        };
        badge.ToolTip = label.Trim().ToUpperInvariant() switch
        {
            "DOPORUČENO" => "Obecně rozumná volba; skutečný přínos závisí na PC a použití.",
            "PODMÍNĚNĚ" => "Zapni pouze tehdy, když ti nevadí uvedené vedlejší účinky.",
            "EXPERIMENTÁLNÍ" => "Může mít nečekané výsledky. Používej pouze po přečtení detailů.",
            "POKROČILÉ" => "Změna může mít vedlejší dopady; před zapnutím zkontroluj detaily.",
            "NOVÉ" => "Nově přidaná funkce v Gasbyn Optimizeru.",
            "FPS" => "Primární cíl: herní snímková frekvence. Zvýšení FPS není zaručeno.",
            "PING" or "INTERNET" or "DELAY" or "ODEZVA" or "SÍŤ" => "Primární cíl: síťová odezva nebo diagnostika spojení; změna pingu není zaručena.",
            "VZHLED" or "UI" => "Primární dopad: vzhled nebo odezva rozhraní Windows.",
            "SPOTŘEBA" => "Může ovlivnit spotřebu energie a teploty.",
            "ZÁZNAMY" => "Může ovlivnit nahrávání či zachytávání herních klipů.",
            _ => "Štítek označuje hlavní oblast nebo možný dopad tohoto tweaku."
        };
        return badge;
    }

    private ToolTip BuildTweakToolTip(string title, string description, string details)
    {
        var onStart = details.IndexOf("ON:", StringComparison.OrdinalIgnoreCase);
        var offStart = details.IndexOf("OFF:", StringComparison.OrdinalIgnoreCase);

        var onDescription = onStart >= 0
            ? details.Substring(onStart + 3, (offStart > onStart ? offStart : details.Length) - (onStart + 3)).Trim().TrimEnd('.', ' ')
            : details.Trim();
        var offDescription = offStart >= 0
            ? details[(offStart + 4)..].Trim().TrimEnd('.', ' ')
            : "Přepnutí na OFF obnoví původní uložené nastavení.";

        var tooltipContent = new StackPanel { Width = 360 };
        tooltipContent.Children.Add(new TextBlock
        {
            Text = "DETAILNÍ POPIS",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Orange"),
            Margin = new Thickness(0, 0, 0, 5)
        });
        tooltipContent.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)FindResource("DisplayFont"),
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        });
        tooltipContent.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = WorkspaceBrush("#BEC3CC"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 10)
        });
        tooltipContent.Children.Add(new Border
        {
            Height = 1,
            Background = WorkspaceBrush("#343842"),
            Margin = new Thickness(0, 0, 0, 10)
        });
        tooltipContent.Children.Add(BuildTweakStateRow("ON", onDescription, "#48D597", "#143126"));
        tooltipContent.Children.Add(BuildTweakStateRow("OFF", offDescription, "#FFB36B", "#302116"));

        return new ToolTip
        {
            Content = new Border
            {
                Background = WorkspaceBrush("#111318"),
                BorderBrush = WorkspaceBrush("#454A54"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Child = tooltipContent
            },
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HasDropShadow = true,
            Placement = PlacementMode.Mouse
        };
    }

    private FrameworkElement BuildTweakStateRow(string label, string description, string accentHex, string backgroundHex)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8)
        };
        row.Children.Add(new Border
        {
            Background = WorkspaceBrush(backgroundHex),
            BorderBrush = WorkspaceBrush(accentHex),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 3, 7, 3),
            Margin = new Thickness(0, 0, 9, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceBrush(accentHex)
            }
        });
        row.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = WorkspaceBrush("#D1D5DB"),
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 295
        });
        return row;
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
        public WorkspaceTweakSnapshot() { }
        public string? PowerScheme { get; set; }
        public List<WorkspaceTweakRegistrySnapshot> RegistryValues { get; set; } = new();
    }

    private sealed class WorkspaceTweakRegistrySnapshot
    {
        public WorkspaceTweakRegistrySnapshot() { }
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
        public WorkspaceFpsRecord() { }
        public DateTime Date { get; set; }
        public string Process { get; set; } = "";
        public int DurationSeconds { get; set; }
        public double AverageFps { get; set; }
        public double OnePercentLow { get; set; }
        public int Frames { get; set; }
    }
}
