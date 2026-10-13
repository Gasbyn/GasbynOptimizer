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
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GasbynOptimizer.Services;

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
    private readonly Dictionary<string, ToggleButton> _toggleButtons = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingToggles;
    private TextBlock _hardwareSummary = new();
    private TextBlock _hardwareRecommendationStatus = new();
    private TextBlock _amdGpuStatus = new();
    private Border? _amdGpuCard;

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

        var hardwareCard = Card();
        hardwareCard.Children.Add(Heading("HARDWARE-AWARE SCAN"));
        _hardwareSummary = Body("Probíhá kontrola CPU, GPU, RAM, základní desky a disků…");
        _hardwareSummary.Margin = new Thickness(0, 0, 0, 10);
        hardwareCard.Children.Add(_hardwareSummary);
        _hardwareRecommendationStatus = Body("Doporučení se načtou po dokončení kontroly hardwaru.");
        hardwareCard.Children.Add(_hardwareRecommendationStatus);
        _root.Children.Add(hardwareCard);

        AddSectionHeader("GAMING");
        AddToggleCard("Windows Game Mode",
            "Umožní Windows upřednostnit herní úlohy. Nezaručuje vyšší FPS; výsledek závisí na PC a hře.",
            "game-mode",
            "ON: Zapíše herní režim Windows jako zapnutý. OFF: Vrátí všechny upravené hodnoty do stavu před zapnutím.");
        AddToggleCard("Vypnout Game DVR na pozadí",
            "Vypne zachytávání/nahrávání Windows Game DVR na pozadí. Může omezit nahrávání klipů přes Xbox Game Bar.",
            "game-dvr",
            "ON: Vypne background capture a App Capture. OFF: Obnoví původní hodnoty a jejich typy.");
        
        AddSectionHeader("WINDOWS PERFORMANCE");
        AddToggleCard("Výkonové vizuální efekty",
            "Nastaví Windows na režim nejlepšího výkonu; některé animace a vizuální efekty se mohou vypnout.",
            "visual-effects",
            "ON: Nastaví VisualFXSetting=2. OFF: Vrátí původní hodnotu, nebo odstraní hodnotu, pokud původně neexistovala.");
        AddToggleCard("High Performance power plan",
            "Přepne na vestavěný plán Vysoký výkon. Může zvýšit spotřebu a teploty; původní plán se uloží.",
            "power-plan",
            "ON: Spustí powercfg.exe /setactive SCHEME_MIN. OFF: Znovu aktivuje původní ID power plánu.");

        AddSectionHeader("WINDOWS RESPONSIVENESS");
        AddToggleCard("Vypnout efekty průhlednosti",
            "Vypne průhlednost nabídky Start a hlavního panelu. Očekávej hlavně změnu vzhledu, ne garantované FPS.",
            "transparency",
            "ON: Nastaví EnableTransparency=0. OFF: Vrátí původní hodnotu nebo odstraní nově vytvořenou hodnotu.");
        AddToggleCard("Rychlejší nabídky a animace oken",
            "Nastaví MenuShowDelay na 100 ms a vypne animaci minimalizace/maximalizace. Změní chování UI Windows.",
            "fast-menus",
            "ON: Nastaví MenuShowDelay=100 a MinAnimate=0. OFF: Obnoví původní obě hodnoty."); 

        AddAmdGpuCompatibilityCard();

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

    private void AddSectionHeader(string title)
    {
        _root.Children.Add(new TextBlock
        {
            Text = title, FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = Brush("#858A94"), Margin = new Thickness(2, 12, 0, 8)
        });
    }

    private void AddToggleCard(string title, string description, string id, string onOffTooltip)
    {
        var card = Card();
        card.ToolTip = onOffTooltip;
        ToolTipService.SetShowDuration(card, 30000);
        var heading = Heading(title);
        heading.ToolTip = onOffTooltip;
        card.Children.Add(heading);
        var body = Body(description);
        body.ToolTip = onOffTooltip;
        card.Children.Add(body);
        var status = new TextBlock
        {
            Text = "Načítám aktuální stav…", Foreground = Brush("#FF9A52"),
            Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap
        };
        card.Children.Add(status);
        _toggleStatuses[id] = status;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var toggle = new ToggleButton
        {
            Content = "OFF", Width = 76, Height = 34, IsChecked = false,
            Background = Brush("#292D35"), Foreground = Brushes.White,
            BorderBrush = Brush("#3A3D45"), BorderThickness = new Thickness(1),
            FontWeight = FontWeights.Bold, Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = onOffTooltip
        };
        toggle.Checked += async (_, _) =>
        {
            if (!_loadingToggles) await ChangeSettingAsync(id, true, status, toggle);
        };
        toggle.Unchecked += async (_, _) =>
        {
            if (!_loadingToggles) await ChangeSettingAsync(id, false, status, toggle);
        };
        _toggleButtons[id] = toggle;
        row.Children.Add(toggle);
        var hint = Body("ON = aplikovat optimalizaci   ·   OFF = obnovit původní stav");
        hint.Margin = new Thickness(12, 0, 0, 0);
        hint.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(hint);
        card.Children.Add(row);
        _root.Children.Add(card);
    }

    private void AddAmdGpuCompatibilityCard()
    {
        var card = Card();
        card.Opacity = 0.62;
        _amdGpuCard = card;
        card.Children.Add(Heading("AMD RADEON-SPECIFIC GPU TUNING"));
        var description = Body("Tento řádek ukazuje kompatibilitu podle nalezené grafiky. V této verzi nejsou přidávány neověřené ovladačové zásahy; nekompatibilní GPU moduly nikdy neprovedou příkaz.");
        card.Children.Add(description);
        _amdGpuStatus = Body("Čekám na kontrolu GPU…");
        _amdGpuStatus.Margin = new Thickness(0, 8, 0, 0);
        card.Children.Add(_amdGpuStatus);
        var unavailable = new ToggleButton
        {
            Content = "NEDOSTUPNÉ", IsChecked = false, IsEnabled = false, Width = 118, Height = 32,
            Background = Brush("#24272E"), Foreground = Brush("#858A94"),
            BorderBrush = Brush("#343842"), BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 10, 0, 0),
            ToolTip = "Zakázáno, pokud daný hardware není přítomen nebo neexistuje ověřená vratná implementace."
        };
        card.Children.Add(unavailable);
        _root.Children.Add(card);
    }

    private async Task ScanCurrentStateAsync()
    {
        SetBusy(true);
        _scanStatus.Text = "Kontroluji aktuální stav Windows i hardware…";
        try
        {
            var scan = await Task.Run(() =>
            {
                var hardware = HardwareInventoryService.Scan();
                var saved = LoadState();
                var actual = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in new[] { "game-mode", "game-dvr", "visual-effects", "power-plan", "transparency", "fast-menus" })
                    actual[id] = ReadActualStateSummary(id);
                return new { Hardware = hardware, Saved = saved, Actual = actual };
            });

            var hw = scan.Hardware;
            var advanced = OptimizerPreferencesStore.Load().AdvancedRecommendations;
            _hardwareSummary.Text =
                $"CPU: {hw.Cpu}\nGPU: {hw.Gpu}\nRAM: {hw.Memory}\nZákladní deska: {hw.Motherboard}\nDisky: {hw.Disks}\nWindows: {hw.Windows}";
            _hardwareRecommendationStatus.Text = hw.HasNvidiaGpu
                ? "DOPORUČENÍ · Detekována NVIDIA GPU. Obecné vratné Windows tweaky jsou dostupné; AMD Radeon-only úpravy nejsou použity."
                : hw.HasAmdGpu
                    ? "DOPORUČENÍ · Detekována AMD Radeon GPU. Používej obecné vratné změny; ovladačové zásahy nejsou automatizované."
                    : "DOPORUČENÍ · Výrobce GPU není jasný. Používej pouze obecné, vratné změny a čti popisky.";
            _amdGpuStatus.Text = !hw.HasAmdGpu
                ? (advanced
                    ? "INFO · AMD Radeon GPU nebyla detekována. Modul zůstává zakázaný, protože není kompatibilní s hardwarem."
                    : "NEDOPORUČENO · AMD Radeon GPU nebyla detekována. Modul je zašedlý a nedá se zapnout.")
                : "AMD GPU detekována, ale tato verze neobsahuje ověřenou vratnou změnu ovladače; žádný příkaz se neprovede.";
            if (_amdGpuCard is not null) _amdGpuCard.Opacity = advanced ? 0.82 : 0.55;

            _loadingToggles = true;
            try
            {
                foreach (var pair in _toggleButtons)
                {
                    var managed = scan.Saved.ContainsKey(pair.Key);
                    pair.Value.IsChecked = managed;
                    pair.Value.Content = managed ? "ON" : "OFF";
                    if (!_toggleStatuses.TryGetValue(pair.Key, out var status)) continue;

                    var note = hw.GetCompatibilityNote(pair.Key, advanced);
                    status.Text = $"Aktuální Windows: {scan.Actual[pair.Key]}\nSpravováno Gasbyn Optimizerem: {(managed ? "ON — původní stav uložen" : "OFF — bez aktivní zálohy")}\n{note}";
                    status.Foreground = note.StartsWith("NEDOPORUČENO", StringComparison.OrdinalIgnoreCase)
                        ? Brush(advanced ? "#858A94" : "#B17B4B")
                        : Brush("#FF9A52");
                }
            }
            finally { _loadingToggles = false; }

            _scanStatus.Text = "Kontrola dokončena. Přepínač ON uloží původní hodnoty; OFF je obnoví.";
        }
        catch (Exception ex) { _scanStatus.Text = "Kontrola dokončena s upozorněním: " + ex.Message; }
        finally { SetBusy(false); }
    }

    private static string ReadActualStateSummary(string id)
    {
        try
        {
            switch (id)
            {
                case "game-mode":
                    return GetGameModeState() switch
                    {
                        true => "Game Mode zapnutý",
                        false => "Game Mode vypnutý",
                        _ => "výchozí Windows / nenastaveno"
                    };
                case "game-dvr":
                    using (var key = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore"))
                    using (var capture = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR"))
                    {
                        var background = key?.GetValue("GameDVR_Enabled");
                        var appCapture = capture?.GetValue("AppCaptureEnabled");
                        if (background is int b && b == 0 && appCapture is int c && c == 0) return "background capture vypnuté";
                        if (background is int || appCapture is int) return "nahrávání povolené nebo částečně nakonfigurované";
                        return "výchozí Windows / nenastaveno";
                    }
                case "visual-effects":
                    var effects = GetVisualEffectsState();
                    return effects is null ? "výchozí Windows" : Convert.ToInt32(effects, CultureInfo.InvariantCulture) == 2
                        ? "režim nejlepšího výkonu"
                        : "jiný režim (" + effects + ")";
                case "power-plan":
                    var output = GetPowerPlanState();
                    var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
                    if (!match.Success) return output.Trim();
                    return match.Value.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase)
                        ? "High Performance"
                        : "aktivní plán ID " + match.Value;
                case "transparency":
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        var value = key?.GetValue("EnableTransparency");
                        return value is null ? "výchozí Windows" : Convert.ToInt32(value, CultureInfo.InvariantCulture) == 0 ? "průhlednost vypnutá" : "průhlednost zapnutá";
                    }
                case "fast-menus":
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
                    using (var metrics = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics"))
                        return $"MenuShowDelay={key?.GetValue("MenuShowDelay") ?? "default"}, MinAnimate={metrics?.GetValue("MinAnimate") ?? "default"}";
                default:
                    return "stav není dostupný";
            }
        }
        catch (Exception ex) { return "stav nelze přečíst: " + ex.Message; }
    }

    private static string GetTweakName(string id) => id switch
    {
        "game-mode" => "Windows Game Mode",
        "game-dvr" => "Vypnout Game DVR na pozadí",
        "visual-effects" => "Výkonové vizuální efekty",
        "power-plan" => "High Performance power plan",
        "transparency" => "Vypnout efekty průhlednosti",
        "fast-menus" => "Rychlejší nabídky a animace oken",
        _ => id
    };

    private void SetToggleValue(ToggleButton toggle, bool enabled)
    {
        _loadingToggles = true;
        try
        {
            toggle.IsChecked = enabled;
            toggle.Content = enabled ? "ON" : "OFF";
            toggle.Background = Brush(enabled ? "#FF6A00" : "#292D35");
        }
        finally { _loadingToggles = false; }
    }

    private async Task ChangeSettingAsync(string id, bool enable, TextBlock status, ToggleButton toggle)
    {
        if (_busy)
        {
            SetToggleValue(toggle, !enable);
            return;
        }

        var label = GetTweakName(id);
        var prompt = enable
            ? $"Zapnout modul „{label}“?\n\nUložím původní hodnoty, zaznamenám všechny operace do logu (pokud je povolený) a po změně znovu ověřím stav. Některé změny mohou vyžadovat odhlášení nebo restart."
            : $"Vrátit modul „{label}“ přesně do původního stavu uloženého při zapnutí?\n\nPůvodní hodnoty budou obnoveny a akce se zapíše do logu.";
        if (MessageBox.Show(prompt, "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            SetToggleValue(toggle, !enable);
            return;
        }

        SetBusy(true);
        status.Text = enable ? "Ukládám původní stav a aplikuji změnu…" : "Obnovuji uložené původní hodnoty…";
        try
        {
            await Task.Run(() => ApplySetting(id, enable));
            _scanStatus.Text = "Změna dokončena. Ověřuji stav Windows…";
            await ScanCurrentStateAsync();
            status.Text += enable ? "\nPoslední akce: úspěšné zapnutí." : "\nPoslední akce: úspěšné obnovení.";
        }
        catch (Exception ex)
        {
            status.Text = "Akce selhala; podrobnosti jsou v chybové zprávě a logu (pokud je povolený).";
            SetToggleValue(toggle, !enable);
            MessageBox.Show(ex.Message, "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false); }
    }

    private static void ApplySetting(string id, bool enable)
    {
        Directory.CreateDirectory(DataFolder);
        var state = LoadState();
        var operations = new List<string>();
        Snapshot? snapshot = null;
        var name = GetTweakName(id);
        var action = enable ? "ENABLE" : "RESTORE";

        try
        {
            if (enable)
            {
                if (!state.TryGetValue(id, out snapshot))
                {
                    snapshot = Capture(id);
                    state[id] = snapshot;
                    SaveState(state);
                    AddBackupOperations(operations, snapshot);
                }
                else
                {
                    operations.Add("BACKUP REUSED: the original state from the first enable is kept; it is not overwritten.");
                }

                switch (id)
                {
                    case "game-mode":
                        SetRegistryDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, operations);
                        SetRegistryDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, operations);
                        break;
                    case "game-dvr":
                        SetRegistryDword(@"System\GameConfigStore", "GameDVR_Enabled", 0, operations);
                        SetRegistryDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, operations);
                        break;
                    case "visual-effects":
                        SetRegistryDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, operations);
                        break;
                    case "power-plan":
                        operations.Add("COMMAND: powercfg.exe /setactive SCHEME_MIN");
                        var output = RunPowerCfg("/setactive SCHEME_MIN");
                        operations.Add("COMMAND OUTPUT: " + (string.IsNullOrWhiteSpace(output) ? "(no stdout)" : output.Trim()));
                        break;
                    case "transparency":
                        SetRegistryDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, operations);
                        break;
                    case "fast-menus":
                        SetRegistryString(@"Control Panel\Desktop", "MenuShowDelay", "100", operations);
                        SetRegistryString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", operations);
                        break;
                    default:
                        throw new InvalidOperationException("Neznámý optimalizační modul.");
                }
            }
            else
            {
                if (!state.TryGetValue(id, out snapshot))
                    throw new InvalidOperationException("Pro tento modul není uložený původní stav. Nic nebylo změněno.");

                Restore(id, snapshot, operations);
                state.Remove(id);
                SaveState(state);
            }

            OptimizerAuditLog.WriteChange(id, name, action, true, operations,
                enable
                    ? "Tweak byl aplikován a jeho původní stav je uložen. Některé změny UI mohou vyžadovat odhlášení/restart."
                    : "Obnovení původních hodnot proběhlo a záznam aktivní zálohy byl odstraněn.");
        }
        catch (Exception ex)
        {
            if (enable && snapshot is not null)
            {
                try
                {
                    operations.Add("ROLLBACK: restore captured values because the enable operation did not complete.");
                    Restore(id, snapshot, operations);
                    state.Remove(id);
                    SaveState(state);
                    operations.Add("ROLLBACK RESULT: original values were restored.");
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

    private static void AddBackupOperations(List<string> operations, Snapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(snapshot.PowerScheme))
            operations.Add("BACKUP POWER SCHEME: original active power scheme ID = " + snapshot.PowerScheme);
        foreach (var item in snapshot.RegistryValues)
            operations.Add($"BACKUP REGISTRY: HKCU\\{item.Path}\\{item.Name}; Exists={item.Exists}; Kind={item.Kind}; Data={item.Data}");
    }

    private static void SetRegistryDword(string path, string name, int value, List<string> operations)
    {
        operations.Add($"REGISTRY SET: HKCU\\{path}\\{name}; Type=REG_DWORD; Value={value}");
        using var key = Registry.CurrentUser.CreateSubKey(path)
            ?? throw new InvalidOperationException("Nepodařilo se otevřít nebo vytvořit HKCU\\" + path);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    private static void SetRegistryString(string path, string name, string value, List<string> operations)
    {
        operations.Add($"REGISTRY SET: HKCU\\{path}\\{name}; Type=REG_SZ; Value={value}");
        using var key = Registry.CurrentUser.CreateSubKey(path)
            ?? throw new InvalidOperationException("Nepodařilo se otevřít nebo vytvořit HKCU\\" + path);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    private static Snapshot Capture(string id)
    {
        var snapshot = new Snapshot();
        if (id == "power-plan")
        {
            var output = RunPowerCfg("/getactivescheme");
            var match = System.Text.RegularExpressions.Regex.Match(output, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
            if (!match.Success) throw new InvalidOperationException("Nepodařilo se zjistit aktivní plán napájení.");
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
            snapshot.RegistryValues.Add(new RegistrySnapshot
            {
                Path = item.Path,
                Name = item.Name,
                Exists = value is not null,
                Kind = value is null ? RegistryValueKind.String.ToString() : key!.GetValueKind(item.Name).ToString(),
                Data = value is null ? "" : SerializeRegistryValue(value)
            });
        }
        return snapshot;
    }

    private static string SerializeRegistryValue(object value) => value switch
    {
        byte[] bytes => Convert.ToBase64String(bytes),
        string[] strings => string.Join("|", strings),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };

    private static void Restore(string id, Snapshot snapshot, List<string> operations)
    {
        if (id == "power-plan")
        {
            if (string.IsNullOrWhiteSpace(snapshot.PowerScheme))
                throw new InvalidOperationException("Backup plánu napájení je neplatný.");
            operations.Add("COMMAND: powercfg.exe /setactive " + snapshot.PowerScheme);
            var output = RunPowerCfg("/setactive " + snapshot.PowerScheme);
            operations.Add("COMMAND OUTPUT: " + (string.IsNullOrWhiteSpace(output) ? "(no stdout)" : output.Trim()));
            return;
        }

        foreach (var item in snapshot.RegistryValues)
        {
            var fullName = $"HKCU\\{item.Path}\\{item.Name}";
            if (item.Exists)
            {
                operations.Add($"REGISTRY RESTORE: {fullName}; Type={item.Kind}; OriginalValue={item.Data}");
                using var key = Registry.CurrentUser.CreateSubKey(item.Path)
                    ?? throw new InvalidOperationException("Nepodařilo se obnovit HKCU\\" + item.Path);
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
                operations.Add("REGISTRY DELETE: " + fullName + "; the value did not exist before enable.");
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
            MessageBox.Show("Příkaz pro bod obnovení byl dokončen. Ověř jeho existenci v Ochrana systému. Pokud Windows už dnes bod vytvořil, nový mohl být odmítnut.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("Bod obnovení se nepodařilo vytvořit. " + ex.Message, "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { SetBusy(false); }
    }

    private async Task RunFpsTestAsync(string exePath, Button runButton)
    {
        if (_busy) return;
        if (!File.Exists(exePath))
        {
            MessageBox.Show("Nejdřív vyber oficiální PresentMon.exe.", "FPS tester", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var processName = _processName.Text.Trim();
        if (string.IsNullOrWhiteSpace(processName) || processName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show("Zadej platný název procesu, například FortniteClient-Win64-Shipping.exe.", "FPS tester", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show(ex.Message, "FPS tester", MessageBoxButton.OK, MessageBoxImage.Warning);
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

    private CardPanel Card() => new()
    {
        Background = Brush("#121419"), BorderBrush = Brush("#24272E"), BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 12)
    };

    private sealed class CardPanel : Border
    {
        private readonly StackPanel _content = new();
        public UIElementCollection Children => _content.Children;
        public CardPanel() => Child = _content;
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