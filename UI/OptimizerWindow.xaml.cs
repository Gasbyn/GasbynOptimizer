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

    private bool _restorePointDone;
    private string _filter = "all";

    public OptimizerWindow(TweakService service, IReadOnlyList<Tweak> tweaks, Dictionary<string, TweakState> states)
    {
        InitializeComponent();

        _service = service;

        foreach (var tweak in tweaks)
        {
            var state = states.TryGetValue(tweak.Id, out var known) ? known : TweakState.Off;
            _items.Add(new TweakItem(tweak, state));
        }

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TweakItem.Category)));
        _view.Filter = FilterItem;
        TweakList.ItemsSource = _view;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version == null ? "" : "Verze " + version.Major + "." + version.Minor + "." + version.Build;
        AutoRestoreCheck.IsChecked = _settings.AutoRestorePoint;

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

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        var settings = ReferenceEquals(sender, NavSettings);
        PageTweaks.Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        PageSettings.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
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

        switch (_filter)
        {
            case "on" when !item.IsOn: return false;
            case "off" when item.IsOn: return false;
            case "rec" when !item.Recommended: return false;
        }

        var text = SearchBox.Text?.Trim() ?? "";
        if (text.Length == 0) return true;

        return item.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
               || item.Description.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    private void UpdateCounts()
    {
        var available = _items.Count(i => i.IsAvailable);
        var on = _items.Count(i => i.IsOn);
        CountText.Text = "Zapnuto " + on + " z " + available + " dostupných optimalizací";
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

        // Zapnuto -> vrátit původní stav. Vypnuto nebo částečně -> zapnout celé.
        await RunToggleAsync(item, !item.IsOn);
    }

    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        var targets = _items.Where(i => i.Recommended && i.IsAvailable && !i.IsOn).ToList();
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
