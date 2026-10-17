using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

public partial class OptimizerWindow : Window
{
    private sealed record Outcome(ToggleResult Result, TweakState State);

    private readonly TweakService _service;
    private readonly ObservableCollection<TweakItem> _items = new();
    private readonly ICollectionView _view;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly SystemProfile _profile;

    private bool _restorePointDone;
    private string _filter = "all";
    private string _category = "";

    public OptimizerWindow(TweakService service, IReadOnlyList<Tweak> tweaks, Dictionary<string, TweakState> states, SystemProfile profile)
    {
        InitializeComponent();

        _service = service;
        _profile = profile;

        var ordered = tweaks.OrderBy(t =>
        {
            var index = Array.IndexOf(Catalog.CategoryOrder, t.Category);
            return index < 0 ? int.MaxValue : index;
        });

        foreach (var tweak in ordered)
        {
            var state = states.TryGetValue(tweak.Id, out var known) ? known : TweakState.Off;
            var reason = service.UnsupportedReason(tweak, profile);
            var recommended = service.IsRecommended(tweak, profile);
            _items.Add(new TweakItem(tweak, state, reason, recommended, _settings.AdvancedMode));
        }

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TweakItem.Category)));
        _view.Filter = FilterItem;
        TweakList.ItemsSource = _view;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version == null
            ? ""
            : "Verze " + version.Major + "." + version.Minor + ".b" + version.Build + "." + Math.Max(version.Revision, 0);
        BuildNav();
        AutoRestoreCheck.IsChecked = _settings.AutoRestorePoint;
        AdvancedCheck.IsChecked = _settings.AdvancedMode;
        SystemText.Text = profile.Summary;
        SystemDetails.Text = BuildSystemDetails(profile);

        UpdateCounts();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
    }

    // --- tmavý titulek okna -------------------------------------------------------------------

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void EnableDarkTitleBar()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            var on = 1;
            DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        }
        catch
        {
            // Starší Windows tuto funkci nemají, titulek zůstane světlý.
        }
    }

    // --- navigace a filtry --------------------------------------------------------------------

    private RadioButton AddNav(string text, string tag, bool selected)
    {
        var button = new RadioButton
        {
            Content = text,
            Tag = tag,
            GroupName = "nav",
            IsChecked = selected,
            Style = (Style)FindResource("NavButton")
        };
        button.Click += Nav_Click;
        NavPanel.Children.Add(button);
        return button;
    }

    private void BuildNav()
    {
        AddNav("Všechny optimalizace", "", true);

        foreach (var category in _items.Select(i => i.Category).Distinct())
            AddNav(category, category, false);

        NavPanel.Children.Add(new Border { Height = 1, Background = (System.Windows.Media.Brush)FindResource("BorderBrush"), Margin = new Thickness(8, 10, 8, 10) });

        var fps = AddNav("FPS tester (brzy)", "#fps", false);
        fps.IsEnabled = false;
        AddNav("Údržba", "#maint", false);
        AddNav("Nastavení", "#settings", false);
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;

        PageTweaks.Visibility = tag.StartsWith('#') ? Visibility.Collapsed : Visibility.Visible;
        PageMaintenance.Visibility = tag == "#maint" ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = tag == "#settings" ? Visibility.Visible : Visibility.Collapsed;
        if (tag.StartsWith('#')) return;

        _category = tag;
        PageTitle.Text = tag.Length == 0 ? "Všechny optimalizace" : tag;
        _view.Refresh();
    }

    private static string BuildSystemDetails(SystemProfile p)
    {
        var lines = new List<string>
        {
            "Windows:  " + p.WindowsName + " (build " + p.Build + ")",
            "Procesor:  " + (p.CpuName.Length > 0 ? p.CpuName : "nezjištěno") +
                (p.Cores > 0 ? "  (" + p.Cores + " jader, " + p.Threads + " vláken)" : ""),
            "Grafika:  " + p.GpuText,
            "Základní deska:  " + p.BoardText,
            "Typ počítače:  " + p.TypeText,
            "Systémový disk:  " + p.DriveText
        };
        if (p.RamGb > 0) lines.Add("Paměť:  " + p.RamGb + " GB");
        return string.Join(Environment.NewLine, lines);
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
        {
            _filter = tag;
            _view.Refresh();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder == null) return;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        _view.Refresh();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not TweakItem item) return false;

        if (_category.Length > 0 && !string.Equals(item.Category, _category, StringComparison.Ordinal)) return false;

        switch (_filter)
        {
            case "on" when !item.IsOn: return false;
            case "off" when item.IsOn: return false;
            case "rec" when !item.IsRecommended: return false;
            case "new" when !item.IsNew: return false;
            case "exp" when !item.IsExperimental: return false;
            case "unsup" when item.IsSupported: return false;
        }

        var text = SearchBox.Text?.Trim() ?? "";
        if (text.Length == 0) return true;

        return item.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
               || item.Description.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateCounts()
    {
        var available = _items.Count(i => i.IsAvailable && i.IsSupported);
        var on = _items.Count(i => i.IsOn && i.IsSupported);
        var unsupported = _items.Count(i => !i.IsSupported);

        CountText.Text = "Zapnuto " + on + " z " + available + " dostupných optimalizací" +
                         (unsupported > 0 ? "  ·  " + unsupported + " nepodporovaných" : "");
    }

    // --- čekací překrytí ----------------------------------------------------------------------

    private void ShowBusy(string text)
    {
        BusyText.Text = text;
        BusyOverlay.Visibility = Visibility.Visible;
    }

    private void HideBusy() => BusyOverlay.Visibility = Visibility.Collapsed;

    // --- změny optimalizací -------------------------------------------------------------------

    private async Task<bool> EnsureRestorePointAsync()
    {
        if (_restorePointDone || !_settings.AutoRestorePoint) return true;

        ShowBusy("Vytvářím bod obnovení systému… může to trvat až minutu.");
        var (ok, message) = await Task.Run(() => RestorePoint.Create());
        HideBusy();

        if (ok)
        {
            _restorePointDone = true;
            return true;
        }

        var answer = MessageBox.Show(this,
            "Bod obnovení se nepodařilo vytvořit:\n" + message + "\n\nChceš přesto pokračovat bez něj?",
            "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes) return false;

        _restorePointDone = true; // nebudeme se ptát při každé další změně
        return true;
    }

    private async Task<bool> RunToggleAsync(TweakItem item, bool enable)
    {
        if (enable && !await EnsureRestorePointAsync()) return false;

        item.IsBusy = true;
        item.SetStatus(enable ? "Zapínám…" : "Vracím…", false);

        Outcome outcome;
        try
        {
            outcome = await Task.Run(() =>
            {
                var result = enable ? _service.Apply(item.Tweak) : _service.Revert(item.Tweak);
                return new Outcome(result, _service.Scan(item.Tweak));
            });
        }
        catch (Exception ex)
        {
            Log.Write("Změna selhala (" + item.Tweak.Id + "): " + ex);
            outcome = new Outcome(new ToggleResult(false, ex.Message, false), item.State);
        }

        item.State = outcome.State;
        item.SetStatus(outcome.Result.Message, !outcome.Result.Success);
        item.IsBusy = false;

        if (outcome.Result.Success && outcome.Result.RestartNeeded)
            RestartBanner.Visibility = Visibility.Visible;

        _view.Refresh();
        UpdateCounts();
        return outcome.Result.Success;
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TweakItem item } || !item.CanToggle) return;

        var enable = !item.IsOn;

        if (enable && !item.IsSupported)
        {
            var answer = MessageBox.Show(this,
                "Optimalizace „" + item.Name + "“ není podporovaná na tomto počítači.\n\n" + item.UnsupportedReason +
                "\n\nZapnutí může být zbytečné nebo způsobit potíže. Chceš ji přesto zapnout?",
                "Nepodporovaná optimalizace", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        // Zapnuto -> vrátit původní stav. Vypnuto nebo částečně -> zapnout celé.
        await RunToggleAsync(item, enable);
    }

    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        var targets = _items.Where(i => i.IsRecommended && i.IsSupported && i.IsAvailable && !i.IsOn).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Všechny doporučené optimalizace jsou už zapnuté.", "Gasbyn Optimizer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            "Zapnout " + targets.Count + " doporučených optimalizací?\nKaždou z nich můžeš později vypnout a vrátit původní stav.",
            "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        if (!await EnsureRestorePointAsync()) return;

        ShowBusy("Zapínám doporučené optimalizace…");
        var failed = 0;
        for (var i = 0; i < targets.Count; i++)
        {
            BusyText.Text = "Zapínám doporučené optimalizace… (" + (i + 1) + "/" + targets.Count + ")";
            if (!await RunToggleAsync(targets[i], true)) failed++;
        }
        HideBusy();

        if (failed > 0)
        {
            MessageBox.Show(this,
                failed + " optimalizací se nepodařilo zapnout celé. Podrobnosti jsou u jednotlivých položek.",
                "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RevertAll_Click(object sender, RoutedEventArgs e)
    {
        var targets = _items.Where(i => i.IsOn || i.IsPartial).ToList();
        if (targets.Count == 0)
        {
            MessageBox.Show(this, "Žádná optimalizace není zapnutá.", "Gasbyn Optimizer",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(this,
            "Vrátit " + targets.Count + " optimalizací do původního stavu?",
            "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        ShowBusy("Vracím optimalizace do původního stavu…");
        for (var i = 0; i < targets.Count; i++)
        {
            BusyText.Text = "Vracím optimalizace do původního stavu… (" + (i + 1) + "/" + targets.Count + ")";
            await RunToggleAsync(targets[i], false);
        }
        HideBusy();
    }

    private void Restart_Click(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(this,
            "Počítač se restartuje za 10 sekund. Ulož si práci.\n\nRestartovat?",
            "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Shell.System32("shutdown.exe"),
                Arguments = "/r /t 10 /c \"Gasbyn Optimizer: restart pro dokončení změn\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Restart se nepodařilo spustit: " + ex.Message, "Gasbyn Optimizer",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- nastavení a údržba -------------------------------------------------------------------

    private void Advanced_Click(object sender, RoutedEventArgs e)
    {
        _settings.AdvancedMode = AdvancedCheck.IsChecked == true;
        _settings.Save();

        foreach (var item in _items)
            item.SetAdvanced(_settings.AdvancedMode);

        _view.Refresh();
        UpdateCounts();
    }

    private void AutoRestore_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoRestorePoint = AutoRestoreCheck.IsChecked == true;
        _settings.Save();
    }

    private async void CreateRestorePoint_Click(object sender, RoutedEventArgs e)
    {
        ShowBusy("Vytvářím bod obnovení systému… může to trvat až minutu.");
        var (ok, message) = await Task.Run(() => RestorePoint.Create());
        HideBusy();

        if (ok) _restorePointDone = true;

        MessageBox.Show(this, message, "Gasbyn Optimizer", MessageBoxButton.OK,
            ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private async void CleanTemp_Click(object sender, RoutedEventArgs e)
    {
        ShowBusy("Čistím dočasné soubory…");
        var (bytes, files) = await Task.Run(() => Maintenance.CleanTemp());
        HideBusy();

        MaintenanceResult.Text = "Smazáno " + files + " souborů, uvolněno " + (bytes / 1024 / 1024) + " MB.";
    }

    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        var ok = await Task.Run(() => Maintenance.FlushDns());
        MaintenanceResult.Text = ok ? "Mezipaměť DNS byla vyprázdněna." : "Mezipaměť DNS se nepodařilo vyprázdnit.";
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureDataDir();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + AppPaths.DataDir + "\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Složku se nepodařilo otevřít: " + ex.Message, "Gasbyn Optimizer",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
