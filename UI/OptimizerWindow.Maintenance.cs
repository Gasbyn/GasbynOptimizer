using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>Stránka Údržba: úklid, aktualizace a odinstalace aplikací, čištění prohlížečů.</summary>
public partial class OptimizerWindow
{
    private readonly ObservableCollection<InstalledApp> _apps = new();
    private ICollectionView? _appsView;
    private bool _appsLoaded;

    private async void MaintTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag }) return;

        MaintClean.Visibility = tag == "clean" ? Visibility.Visible : Visibility.Collapsed;
        MaintUpdate.Visibility = tag == "update" ? Visibility.Visible : Visibility.Collapsed;
        MaintUninstall.Visibility = tag == "uninstall" ? Visibility.Visible : Visibility.Collapsed;
        MaintBrowser.Visibility = tag == "browser" ? Visibility.Visible : Visibility.Collapsed;

        if (tag == "uninstall" && !_appsLoaded)
            await LoadAppsAsync();
    }

    private static string Megabytes(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0.#");

    /// <summary>Posledních několik řádků výstupu bez ovládacích znaků a animace průběhu.</summary>
    private static string Tail(string output, int lines = 6)
    {
        var cleaned = output.Replace("\r", "\n")
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 1 && !Regex.IsMatch(l, @"^[-\\|/ ]+$") && !Regex.IsMatch(l, @"^[█▒ ]+"))
            .ToList();
        return string.Join(Environment.NewLine, cleaned.Skip(Math.Max(0, cleaned.Count - lines)));
    }

    // --- mezipaměť aplikací ---------------------------------------------------------------------

    private async void CleanAppCaches_Click(object sender, RoutedEventArgs e)
    {
        ShowBusy("Čistím mezipaměť aplikací…");
        var result = await Task.Run(() => Maintenance.CleanAppCaches());
        HideBusy();

        AppCacheResult.Text = result.Files == 0
            ? "Nebylo co mazat, mezipaměť aplikací je prázdná."
            : "Smazáno " + result.Files + " souborů, uvolněno " + Megabytes(result.Bytes) + " MB.";
    }

    // --- aktualizace aplikací -------------------------------------------------------------------

    private async Task CheckUpdatesAsync()
    {
        ShowBusy("Kontroluji aktualizace aplikací (winget)… může to trvat chvíli.");
        var (apps, raw) = await Winget.ListUpgradesAsync();
        HideBusy();

        UpdateList.ItemsSource = null;

        if (!raw.Available)
        {
            UpdateStatus.Text = "Windows Package Manager (winget) na tomto počítači není. Nainstaluj „Instalační program aplikací“ z Microsoft Storu.";
            return;
        }

        if (apps == null)
        {
            UpdateStatus.Text = "Výstup wingetu se nepodařilo zpracovat:" + Environment.NewLine + Tail(raw.Output);
            return;
        }

        UpdateList.ItemsSource = apps;
        UpdateStatus.Text = apps.Count == 0
            ? "Winget nenašel žádné aktualizace." + (raw.ExitCode != 0 ? " (kód výstupu " + raw.ExitCode + ")" : "")
            : "Dostupných aktualizací: " + apps.Count;
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e) => await CheckUpdatesAsync();

    private async void UpdateApp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: UpgradableApp app }) return;

        ShowBusy("Aktualizuji „" + app.Name + "“…");
        var result = await Winget.UpgradeOneAsync(app.Id);
        HideBusy();

        if (!result.Available)
        {
            UpdateStatus.Text = "Winget na tomto počítači není.";
            return;
        }

        var tail = Tail(result.Output, 3);
        await CheckUpdatesAsync();
        UpdateStatus.Text = "„" + app.Name + "“: " + (result.ExitCode == 0 ? "aktualizováno." : "aktualizace skončila s kódem " + result.ExitCode + ".") +
                            (tail.Length > 0 ? Environment.NewLine + tail : "") + Environment.NewLine + UpdateStatus.Text;
    }

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Aktualizovat všechny aplikace, které winget zná?\n\nAktualizace běží na pozadí a může trvat několik minut. Některé aplikace se při tom zavřou nebo restartují.",
            "Aktualizace aplikací", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        ShowBusy("Aktualizuji všechny aplikace… může to trvat i několik minut.");
        var result = await Winget.UpgradeAllAsync();
        HideBusy();

        if (!result.Available)
        {
            UpdateStatus.Text = "Winget na tomto počítači není.";
            return;
        }

        var tail = Tail(result.Output, 4);
        await CheckUpdatesAsync();
        UpdateStatus.Text = (result.ExitCode == 0 ? "Aktualizace dokončena." : "Aktualizace skončila s kódem " + result.ExitCode + ".") +
                            (tail.Length > 0 ? Environment.NewLine + tail : "") + Environment.NewLine + UpdateStatus.Text;
    }

    // --- odinstalace aplikací -------------------------------------------------------------------

    private async Task LoadAppsAsync()
    {
        ShowBusy("Načítám seznam nainstalovaných aplikací…");
        var list = await Task.Run(() => InstalledApps.List());
        HideBusy();

        _apps.Clear();
        foreach (var app in list) _apps.Add(app);

        if (_appsView == null)
        {
            _appsView = CollectionViewSource.GetDefaultView(_apps);
            _appsView.Filter = AppFilter;
            AppList.ItemsSource = _appsView;
        }

        _appsView.Refresh();
        _appsLoaded = true;
        UninstallStatus.Text = "Nainstalováno " + _apps.Count + " aplikací.";
    }

    private bool AppFilter(object obj)
    {
        if (obj is not InstalledApp app) return false;
        var text = AppSearchBox.Text?.Trim() ?? "";
        if (text.Length == 0) return true;
        return app.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
               app.Publisher.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (AppSearchPlaceholder == null) return;
        AppSearchPlaceholder.Visibility = string.IsNullOrEmpty(AppSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        _appsView?.Refresh();
    }

    private async void RefreshApps_Click(object sender, RoutedEventArgs e) => await LoadAppsAsync();

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: InstalledApp app }) return;

        var answer = MessageBox.Show(this,
            "Odinstalovat „" + app.Name + "“?\n\nSpustí se odinstalátor této aplikace. Odinstalaci nejde vrátit zpět.",
            "Odinstalace aplikace", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        ShowBusy("Odinstalovávám „" + app.Name + "“… dokonči případné okno odinstalátoru.");
        var (ok, message) = await Task.Run(() => InstalledApps.Uninstall(app));
        HideBusy();

        await LoadAppsAsync();
        UninstallStatus.Text = ok
            ? "„" + app.Name + "“: " + message
            : "Odinstalace „" + app.Name + "“ selhala: " + message;
    }

    // --- prohlížeče -----------------------------------------------------------------------------

    private async void CleanBrowsers_Click(object sender, RoutedEventArgs e)
    {
        var running = await Task.Run(() => BrowserCleaner.RunningBrowsers());
        if (running.Count > 0)
        {
            MessageBox.Show(this,
                "Nejdřív zavři tyto prohlížeče: " + string.Join(", ", running) + ".\n\nBěžící prohlížeče mají soubory zamčené a nešly by vyčistit.",
                "Vyčistit prohlížeče", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            "Smazat mezipaměť a cookies všech prohlížečů?\n\nPo vyčištění se budeš muset znovu přihlásit na všech webech. Hesla, historie a záložky zůstanou.\n\nTuto akci nejde vrátit zpět a nedoporučujeme ji.",
            "Vyčistit prohlížeče (nedoporučeno)", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        ShowBusy("Čistím prohlížeče…");
        var result = await Task.Run(() => BrowserCleaner.Clean());
        HideBusy();

        if (!result.Done)
        {
            BrowserResult.Text = "Čištění se neprovedlo, běží: " + string.Join(", ", result.RunningBrowsers) + ".";
            return;
        }

        BrowserResult.Text = result.Cleaned.Count == 0
            ? "Nenašel jsem žádný podporovaný prohlížeč."
            : "Vyčištěno (" + string.Join(", ", result.Cleaned) + "): " + result.Files + " souborů, uvolněno " + Megabytes(result.Bytes) + " MB.";
    }
}
