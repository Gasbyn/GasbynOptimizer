using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>Aktualizace ovladačů přes Windows Update, podkategorie v Aktualizaci (vedle aplikací).</summary>
public partial class OptimizerWindow
{
    private StackPanel _driversPanel = null!;
    private StackPanel _updateAppsPanel = null!;
    private StackPanel _driverList = null!;
    private TextBlock _driverStatus = null!;
    private readonly List<(DriverUpdate Driver, CheckBox Check)> _driverRows = new();

    private static readonly Dictionary<string, string> DriverEn = new()
    {
        ["Aplikace"] = "Apps",
        ["Drivery"] = "Drivers",
        ["Aktualizace ovladačů"] = "Driver updates",
        ["Používá Windows Update, takže nabídne jen ovladače, které pro tento počítač zveřejnil výrobce. Nic se nestahuje z cizích webů. Před instalací se vytvoří bod obnovení."] =
            "Uses Windows Update, so it only offers drivers the manufacturer published for this PC. Nothing is downloaded from third-party sites. A restore point is created before installing.",
        ["Vyhledat ovladače"] = "Search for drivers",
        ["Nainstalovat vybrané"] = "Install selected"
    };

    /// <summary>Přidá do Aktualizace přepínač Aplikace / Drivery. Drivery se nabídnou jen když je ve Windows k dispozici Windows Update.</summary>
    private void BuildDriversUi()
    {
        L.Add(DriverEn);
        if (Type.GetTypeFromProgID("Microsoft.Update.Session") == null)
        {
            Log.Write("Aktualizace ovladačů: rozhraní Windows Update není k dispozici, kategorie Drivery se nezobrazí.");
            return;
        }

        _updateAppsPanel = new StackPanel();
        foreach (var child in MaintUpdate.Children.Cast<UIElement>().ToList())
        {
            MaintUpdate.Children.Remove(child);
            _updateAppsPanel.Children.Add(child);
        }

        var chips = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        chips.Children.Add(MakeChip("Aplikace", "apps", "updsub", true, (_, _) => ShowUpdateSub(false)));
        chips.Children.Add(MakeChip("Drivery", "drivers", "updsub", false, (_, _) => ShowUpdateSub(true)));

        var card = MakeCard("Aktualizace ovladačů",
            "Používá Windows Update, takže nabídne jen ovladače, které pro tento počítač zveřejnil výrobce. Nic se nestahuje z cizích webů. Před instalací se vytvoří bod obnovení.",
            out var body, 0);
        var buttons = new WrapPanel();
        buttons.Children.Add(GhostButton("Vyhledat ovladače", async (_, _) => await SearchDriversAsync()));
        var installButton = new Button
        {
            Style = (Style)FindResource("PrimaryButton"),
            Content = "Nainstalovat vybrané",
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 8)
        };
        installButton.Click += async (_, _) => await InstallDriversAsync();
        buttons.Children.Add(installButton);
        body.Children.Add(buttons);
        _driverStatus = new TextBlock { Foreground = (Brush)FindResource("GreenBrush"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        body.Children.Add(_driverStatus);

        _driverList = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        _driversPanel = new StackPanel { Visibility = Visibility.Collapsed };
        _driversPanel.Children.Add(card);
        _driversPanel.Children.Add(_driverList);

        MaintUpdate.Children.Add(chips);
        MaintUpdate.Children.Add(_updateAppsPanel);
        MaintUpdate.Children.Add(_driversPanel);
    }

    private void ShowUpdateSub(bool drivers)
    {
        _updateAppsPanel.Visibility = drivers ? Visibility.Collapsed : Visibility.Visible;
        _driversPanel.Visibility = drivers ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task SearchDriversAsync()
    {
        ShowBusy("Hledám ovladače přes Windows Update… může to trvat i několik minut.");
        DriverSearchResult result;
        try { result = await Task.Run(DriverUpdater.Search); }
        finally { HideBusy(); }

        _driverList.Children.Clear();
        _driverRows.Clear();

        if (!result.Ok)
        {
            _driverStatus.Foreground = (Brush)FindResource("AmberBrush");
            _driverStatus.Text = result.Message;
            return;
        }

        _driverStatus.Foreground = (Brush)FindResource("GreenBrush");
        if (result.Updates.Count == 0)
        {
            _driverStatus.Text = "Windows Update nenabízí žádné nové ovladače." +
                                 (DriverUpdater.BlockedByPolicy()
                                     ? " Máš zapnuté „Nestahovat ovladače přes Windows Update“ v kategorii Aktualizace, proto se žádné nenabízí."
                                     : " Všechny ovladače, které výrobce zveřejnil, jsou aktuální.");
            return;
        }

        _driverStatus.Text = "Nalezeno ovladačů: " + result.Updates.Count + ". Zaškrtni, které chceš nainstalovat.";
        foreach (var driver in result.Updates)
        {
            var row = MakeCard(driver.Title, "", out var rowBody, 6);
            if (driver.Subtitle.Length > 0)
                rowBody.Children.Add(new TextBlock { Text = driver.Subtitle, FontSize = 12, Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            var check = new CheckBox { Content = "Nainstalovat", IsChecked = true, Foreground = (Brush)FindResource("TextBrush") };
            rowBody.Children.Add(check);
            _driverRows.Add((driver, check));
            _driverList.Children.Add(row);
        }
        L.Apply(_driverList);
    }

    private async Task InstallDriversAsync()
    {
        var selected = _driverRows.Where(r => r.Check.IsChecked == true).Select(r => r.Driver).ToList();
        if (selected.Count == 0)
        {
            _driverStatus.Foreground = (Brush)FindResource("AmberBrush");
            _driverStatus.Text = "Nejdřív ovladače vyhledej a zaškrtni, které chceš nainstalovat.";
            return;
        }

        var answer = MessageBox.Show(this,
            "Nainstalovat " + selected.Count + " ovladačů přes Windows Update?\n\nPřed instalací se vytvoří bod obnovení. Instalace může trvat několik minut, obrazovka může na chvíli zablikat a někdy je potřeba restart.",
            "Aktualizace ovladačů", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        if (!await EnsureRestorePointAsync()) return;

        ShowBusy("Instaluji ovladače… nevypínej počítač, může to trvat několik minut.");
        DriverInstallResult result;
        try { result = await Task.Run(() => DriverUpdater.Install(selected)); }
        finally { HideBusy(); }

        _driverStatus.Foreground = (Brush)FindResource(result.Ok ? "GreenBrush" : "AmberBrush");
        _driverStatus.Text = result.Message.Length > 0
            ? result.Message
            : "Nainstalováno: " + result.Succeeded + (result.Failed > 0 ? ", selhalo: " + result.Failed : "") + "." +
              (result.RebootRequired ? " Pro dokončení restartuj počítač." : "");

        if (result.Succeeded > 0) await SearchDriversAfterInstallAsync();
    }

    private async Task SearchDriversAfterInstallAsync()
    {
        var keep = _driverStatus.Text;
        var keepBrush = _driverStatus.Foreground;
        await SearchDriversAsync();
        _driverStatus.Text = keep + (_driverRows.Count > 0 ? "  Zbývá ovladačů: " + _driverRows.Count + "." : "");
        _driverStatus.Foreground = keepBrush;
    }
}
