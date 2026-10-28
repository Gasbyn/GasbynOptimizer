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
    private SearchIndex.Query _query = new("");
    private bool _grouped = true;

    // Animace vjezdu karet: po změně seznamu (hledání, filtr, kategorie) karty naběhnou jedna po druhé.
    private bool _animateNext;
    private int _enterIndex;

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
        _animateNext = true;
        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(() => _animateNext = false), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        TweakList.ItemsSource = _view;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version == null
            ? ""
            : version.Build == 1 && version.Revision > 0
                ? "Verze " + version.Major + "." + version.Minor + ".b" + version.Build + "." + version.Revision
                : "Verze " + version.Major + "." + version.Minor + "." + Math.Max(version.Build, 0) + "." + Math.Max(version.Revision, 0);
        BuildNav();
        AutoRestoreCheck.IsChecked = _settings.AutoRestorePoint;
        AdvancedCheck.IsChecked = _settings.AdvancedMode;
        SystemText.Text = profile.Summary;
        SystemDetails.Text = BuildSystemDetails(profile);

        UpdateCounts();
        SourceInitialized += (_, _) => EnableDarkTitleBar();
        StateChanged += (_, _) => UpdateWindowState();
        WindowRoot.SizeChanged += (_, _) => ClipRoot();
    }

    /// <summary>Ořízne obsah do zaoblených rohů okna.</summary>
    private void ClipRoot()
    {
        var radius = WindowState == WindowState.Maximized ? 0 : WindowRoot.CornerRadius.TopLeft;
        WindowRoot.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, WindowRoot.ActualWidth, WindowRoot.ActualHeight), radius, radius);
    }

    // --- vlastní horní lišta ------------------------------------------------------------------

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaxIcon.Visibility = maximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreIcon.Visibility = maximized ? Visibility.Visible : Visibility.Collapsed;

        // Okno bez systémového rámu po maximalizaci přesahuje obrazovku o šířku rámu, proto ho odsadíme.
        WindowRoot.Margin = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
        WindowRoot.CornerRadius = new CornerRadius(maximized ? 0 : 14);
        ClipRoot();
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

    private static StackPanel NavContent(string text, string tag)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Icons.Create<RadioButton>(Icons.ForCategory(tag), 18);
        icon.Margin = new Thickness(0, 0, 12, 0);
        icon.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(icon);
        panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        return panel;
    }

    private RadioButton AddNav(string text, string tag, bool selected)
    {
        var button = new RadioButton
        {
            Content = NavContent(text, tag),
            Tag = tag,
            GroupName = "nav",
            IsChecked = selected,
            Style = (Style)FindResource("NavButton")
        };
        button.Click += Nav_Click;
        NavPanel.Children.Add(button);
        return button;
    }

    // Levé menu je záměrně krátké. Ostatní kategorie (soukromí, aktualizace, internet atd.)
    // se zobrazují jen v položce „Všechny optimalizace“, kde jsou seskupené pod nadpisy.
    private static readonly (string Name, string[] Categories)[] NavGroups =
    {
        ("Hraní a výkon", new[] { Catalog.CatGaming, CatalogPower.CatPower, Catalog.CatSystem, CatalogHardware.CatHardware }),
        ("Vzhled", new[] { Catalog.CatVisual, CatalogFonts.CatFonts, Catalog.CatUi }),
        ("Služby", new[] { Catalog.CatServices }),
        ("Debloat", new[] { CatalogDebloat.CatDebloat }),
        ("Pokročilé", new[] { CatalogAdvanced.CatAdvanced })
    };

    private string[] _categories = Array.Empty<string>();

    private void BuildNav()
    {
        AddNav("Všechny optimalizace", "", true);

        foreach (var (name, _) in NavGroups)
            AddNav(name, name, false);

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
        if (tag == "#maint") Motion.Page(PageMaintenance);
        if (tag == "#settings") Motion.Page(PageSettings);
        if (tag.StartsWith('#')) return;

        _categories = NavGroups.FirstOrDefault(g => g.Name == tag).Categories ?? Array.Empty<string>();
        PageTitle.Text = tag.Length == 0 ? "Všechny optimalizace" : tag;
        RefreshList();
        Motion.Blink(PageTitle);
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
            RefreshList();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchPlaceholder == null) return;
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        _query = new SearchIndex.Query(SearchBox.Text);
        RefreshList();
    }

    /// <summary>Znovu sestaví seznam (filtr, hledání, kategorie) a spustí plynulý vjezd karet.</summary>
    private void RefreshList()
    {
        var searching = !_query.IsEmpty;
        using (_view.DeferRefresh())
        {
            // Při hledání se výsledky řadí podle relevance (bez rozdělení do kategorií), jinak jsou seskupené.
            if (searching == _grouped)
            {
                _view.GroupDescriptions.Clear();
                _view.SortDescriptions.Clear();
                if (searching)
                    _view.SortDescriptions.Add(new System.ComponentModel.SortDescription(nameof(TweakItem.SearchScore), System.ComponentModel.ListSortDirection.Descending));
                else
                    _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TweakItem.Category)));
                _grouped = !searching;
            }
        }

        _animateNext = true;
        _enterIndex = 0;
        _view.Refresh();
        UpdateNoResults();
        Dispatcher.BeginInvoke(new Action(() => _animateNext = false), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private string? _suggestion;

    private void UpdateNoResults()
    {
        var empty = _view.IsEmpty;
        NoResults.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        TweakList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        if (!empty) return;

        var text = SearchBox.Text.Trim();
        if (text.Length == 0)
        {
            NoResultsText.Text = "V této kategorii a filtru nic není.";
            NoResultsSuggest.Text = "";
            _suggestion = null;
            return;
        }

        NoResultsText.Text = "Nic jsem nenašel pro „" + text + "“.";
        _suggestion = SearchIndex.Suggest(_items.Select(i => i.SearchDoc), text);
        NoResultsSuggest.Text = _suggestion == null ? "Zkus jiné slovo, třeba název kategorie nebo anglický výraz." : "Mysleli jste „" + _suggestion + "“?";
        NoResultsSuggest.Cursor = _suggestion == null ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.Hand;
    }

    private void Suggest_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_suggestion == null) return;
        SearchBox.Text = _suggestion;
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    /// <summary>Krátké „cuknutí“ karty při kliknutí a po změně stavu.</summary>
    private void PopCard(TweakItem item)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (TweakList.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement card) Motion.Pop(card);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void Card_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_animateNext || sender is not FrameworkElement element) return;
        if (_enterIndex >= 16) return;
        Motion.Enter(element, _enterIndex++);
    }

    private bool FilterItem(object obj)
    {
        if (obj is not TweakItem item) return false;

        if (_categories.Length > 0 && Array.IndexOf(_categories, item.Category) < 0) return false;

        switch (_filter)
        {
            case "on" when !item.IsOn: return false;
            case "off" when item.IsOn: return false;
            case "rec" when !item.IsRecommended: return false;
            case "new" when !item.IsNew: return false;
            case "exp" when !item.IsExperimental: return false;
            case "unsup" when item.IsSupported: return false;
        }

        if (_query.IsEmpty) { item.SearchScore = 0; return true; }

        item.SearchScore = SearchIndex.Score(item.SearchDoc, _query);
        return item.SearchScore > 0;
    }

    private void UpdateCounts()
    {
        var available = _items.Count(i => i.IsAvailable && i.IsSupported && !i.IsApplyOnly);
        var on = _items.Count(i => i.IsOn && i.IsSupported && !i.IsApplyOnly);
        var unsupported = _items.Count(i => !i.IsSupported);

        CountText.Text = "Zapnuto " + on + " z " + available + " dostupných optimalizací" +
                         (unsupported > 0 ? "  ·  " + unsupported + " nepodporovaných" : "");
    }

    // --- čekací překrytí ----------------------------------------------------------------------

    private void ShowBusy(string text)
    {
        BusyText.Text = text;
        Icons.Spin(BusySpinner, true);
        BusyOverlay.Opacity = 0;
        BusyOverlay.Visibility = Visibility.Visible;
        BusyOverlay.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        Motion.Indeterminate(BusyTrack, BusyFill, true);
        // Okno se musí stihnout vykreslit dřív, než začne těžká práce, jinak animace začne se zpožděním.
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
    }

    private void HideBusy()
    {
        Icons.Spin(BusySpinner, false);
        Motion.Indeterminate(BusyTrack, BusyFill, false);
        BusyOverlay.BeginAnimation(OpacityProperty, null);
        BusyOverlay.Visibility = Visibility.Collapsed;
    }

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

        // U plánů napájení je aktivní vždy jen jeden, ostatní se musí překreslit.
        if (item.IsApplyOnly)
        {
            foreach (var other in _items.Where(i => i.IsApplyOnly && !ReferenceEquals(i, item)).ToList())
            {
                var captured = other;
                captured.State = await Task.Run(() => _service.Scan(captured.Tweak));
            }
        }

        if (outcome.Result.Success && outcome.Result.RestartNeeded)
            RestartBanner.Visibility = Visibility.Visible;

        _view.Refresh();
        UpdateCounts();
        PopCard(item);
        return outcome.Result.Success;
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TweakItem item } || !item.CanToggle) return;

        var enable = !item.IsOn;
        PopCard(item);

        // Pokročilý režim dovolí kliknout i na optimalizaci, která na tomto PC nemá co měnit. Není co provést.
        if (!item.IsAvailable)
        {
            item.SetStatus(TweakItem.NothingToChange, true);
            return;
        }

        if (enable && !item.IsSupported)
        {
            var answer = MessageBox.Show(this,
                "Optimalizace „" + item.Name + "“ není podporovaná na tomto počítači.\n\n" + item.UnsupportedReason +
                "\n\nZapnutí může být zbytečné nebo způsobit potíže. Chceš ji přesto zapnout?",
                "Nepodporovaná optimalizace", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        if (enable && item.Tweak.Confirm is { } confirm)
        {
            var sure = MessageBox.Show(this, confirm, item.Name, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (sure != MessageBoxResult.Yes) return;
        }

        // Zapnuto -> vrátit původní stav. Vypnuto nebo částečně -> zapnout celé.
        await RunToggleAsync(item, enable);
    }

    private async void ApplyRecommended_Click(object sender, RoutedEventArgs e)
    {
        var targets = _items.Where(i => i.IsRecommended && i.IsSupported && i.IsAvailable && !i.IsOn && !i.IsApplyOnly).ToList();
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
        var targets = _items.Where(i => (i.IsOn || i.IsPartial) && !i.IsApplyOnly).ToList();
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

    private void ShowTerms_Click(object sender, RoutedEventArgs e) => new TermsWindow(false) { Owner = this }.ShowDialog();

    private void DiscordServer_Click(object sender, RoutedEventArgs e) => SupportLinks.Open(SupportLinks.Server);

    private void DiscordChat_Click(object sender, RoutedEventArgs e) => SupportLinks.Open(SupportLinks.Chat);

    private async void CleanTemp_Click(object sender, RoutedEventArgs e)
    {
        ShowBusy("Čistím dočasné a systémové soubory…");
        var (bytes, files) = await Task.Run(() => Maintenance.CleanTemp());
        HideBusy();

        MaintenanceResult.Text = "Smazáno " + files + " souborů, uvolněno " + (bytes / 1024 / 1024) + " MB.";
    }

    private async void DeepClean_Click(object sender, RoutedEventArgs e)
    {
        var components = DeepComponents.IsChecked == true;
        var drivers = DeepDrivers.IsChecked == true;
        var languages = DeepLanguages.IsChecked == true;
        if (!components && !drivers && !languages)
        {
            DeepResult.Text = "Vyber aspoň jednu položku.";
            return;
        }

        if (languages)
        {
            var answer = MessageBox.Show(this,
                "Odebrat jazykové balíčky, které nepoužíváš?\n\nOdeberou se jen jazyky mimo tvůj seznam jazyků a jazyka systému. Vrátit je jde jen znovu přidáním v nastavení Windows.",
                "Jazykové prostředky", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes) return;
        }

        ShowBusy("Čistím disk… úklid součástí Windows může trvat i několik minut.");
        var result = await Task.Run(() => Maintenance.DeepClean(components, drivers, languages));
        HideBusy();

        DeepResult.Text = "Hotovo (" + result.Summary + "). Uvolněno přibližně " + (result.Bytes / 1024 / 1024) + " MB.";
        Motion.Pop(DeepResult);
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
