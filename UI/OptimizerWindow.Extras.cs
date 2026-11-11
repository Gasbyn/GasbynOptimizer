using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GasbynOptimizer.Core;
using Microsoft.Win32;

namespace GasbynOptimizer.UI;

/// <summary>Soubor exportovaného nastavení.</summary>
public sealed class SettingsExport
{
    public string App { get; set; } = "GasbynOptimizer";
    public string Version { get; set; } = "";
    public string Exported { get; set; } = "";
    public string Language { get; set; } = "";
    public string Theme { get; set; } = "";
    public List<string> Enabled { get; set; } = new();
}

/// <summary>Nové stránky a karty: chytrý detektor, spouštěné aplikace, samokontrola, export/import, jazyk a motiv.</summary>
public partial class OptimizerWindow
{
    private ScrollViewer _pageStartup = null!;
    private StackPanel _startupList = null!;
    private TextBlock _startupInfo = null!;
    private TextBlock _selfCheckText = null!;
    private bool _selfCheckRunning;

    private static readonly Dictionary<string, string> ExtraEn = new()
    {
        ["Chytrý detektor doporučí optimalizace podle tvého počítače: hardwaru, disku, paměti, notebooku nebo stolního PC a nainstalovaných programů. Všechno se vyhodnocuje přímo v tomto počítači, nic se neodesílá."] =
            "The smart detector recommends optimizations based on your PC: hardware, disk, memory, laptop or desktop and installed programs. Everything is evaluated locally, nothing is sent anywhere.",
        ["Analyzovat znovu"] = "Analyze again",
        ["Zapnout"] = "Enable",
        ["Zapnout všechna doporučení"] = "Enable all suggestions",
        ["Detektor nenašel nic, co by dávalo smysl zapnout. Tvůj počítač je v pořádku nastavený."] = "The detector found nothing worth enabling. Your PC is set up well.",
        ["Zde vidíš programy, které se spouští s Windows. Vypnutím zrychlíš start systému. Nic se nemaže, kdykoli ho můžeš zase zapnout. Používá stejný záznam jako Správce úloh."] =
            "These programs start with Windows. Disabling them speeds up boot. Nothing is deleted and you can turn them back on anytime. It uses the same record as Task Manager.",
        ["Obnovit seznam"] = "Refresh list",
        ["Spouštět s Windows"] = "Start with Windows",
        ["Po spuštění aplikace se automaticky zkontroluje, jestli Windows nebo jiný program nevrátil optimalizace, které jsi zapnul(a). Když ano, nabídne se jejich opětovné zapnutí. Kontrola nic nemění sama od sebe."] =
            "On every launch the app checks whether Windows or another program reverted optimizations you enabled. If so, you can re-enable them. The check changes nothing on its own.",
        ["Kontrolovat automaticky při každém spuštění"] = "Check automatically on every launch",
        ["Můžeš uložit zapnuté optimalizace do souboru a na jiném počítači (nebo po přeinstalaci Windows) je zase načíst. Umístění souboru si vybereš sám."] =
            "Save your enabled optimizations to a file and load them on another PC (or after reinstalling Windows). You choose where the file goes.",
        ["Jazyk aplikace. Texty u jednotlivých optimalizací zatím zůstávají česky."] = "App language. Texts of individual optimizations are still in Czech for now.",
        ["Výchozí je tmavý vzhled. Světlý vzhled je volitelný."] = "Dark is the default. Light is optional.",
        ["Odkazy"] = "Links"
    };

    private Border MakeCard(string title, string text, out StackPanel body, double marginTop = 12)
    {
        body = new StackPanel();
        body.Children.Add(new TextBlock { Text = title, Style = (Style)FindResource("SectionTitle") });
        if (text.Length > 0)
            body.Children.Add(new TextBlock { Text = text, Style = (Style)FindResource("SectionText") });
        return new Border
        {
            Background = (Brush)FindResource("PanelBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20),
            Margin = new Thickness(0, marginTop, 0, 0),
            Child = body
        };
    }

    private Button GhostButton(string text, RoutedEventHandler click, double right = 8)
    {
        var button = new Button
        {
            Style = (Style)FindResource("GhostButton"),
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, right, 8)
        };
        button.Click += click;
        return button;
    }

    private static ScrollViewer MakePage(string title, out StackPanel root)
    {
        root = new StackPanel { Margin = new Thickness(28, 24, 28, 28), MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });
        return new ScrollViewer { Visibility = Visibility.Collapsed, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
    }

    private void BuildExtras()
    {
        L.Add(ExtraEn);
        var host = (Panel)PageSettings.Parent;

        // --- Spouštěné aplikace
        _pageStartup = MakePage("Spouštěné aplikace", out var startupRoot);
        startupRoot.Children.Add(new TextBlock
        {
            Text = "Zde vidíš programy, které se spouští s Windows. Vypnutím zrychlíš start systému. Nic se nemaže, kdykoli ho můžeš zase zapnout. Používá stejný záznam jako Správce úloh.",
            Style = (Style)FindResource("SectionText")
        });
        _startupInfo = new TextBlock { Style = (Style)FindResource("SectionText"), Foreground = (Brush)FindResource("TextBrush") };
        startupRoot.Children.Add(_startupInfo);
        startupRoot.Children.Add(GhostButton("Obnovit seznam", (_, _) => LoadStartup()));
        _startupList = new StackPanel();
        startupRoot.Children.Add(_startupList);
        host.Children.Add(_pageStartup);

        // --- karty v Nastavení
        var settingsRoot = (StackPanel)((ScrollViewer)PageSettings).Content;

        var selfCard = MakeCard("Samokontrola",
            "Po spuštění aplikace se automaticky zkontroluje, jestli Windows nebo jiný program nevrátil optimalizace, které jsi zapnul(a). Když ano, nabídne se jejich opětovné zapnutí. Kontrola nic nemění sama od sebe.",
            out var selfBody, 0);
        var auto = new CheckBox
        {
            Content = "Kontrolovat automaticky při každém spuštění",
            Foreground = (Brush)FindResource("TextBrush"),
            IsChecked = _settings.AutoSelfCheck,
            Margin = new Thickness(0, 0, 0, 10)
        };
        auto.Click += (_, _) => { _settings.AutoSelfCheck = auto.IsChecked == true; _settings.Save(); };
        selfBody.Children.Add(auto);
        _selfCheckText = new TextBlock { Foreground = (Brush)FindResource("TextBrush"), TextWrapping = TextWrapping.Wrap, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 10) };
        selfBody.Children.Add(_selfCheckText);
        selfBody.Children.Add(GhostButton("Zkontrolovat teď", async (_, _) => await RunSelfCheckAsync(true)));

        var exportCard = MakeCard("Export a import nastavení",
            "Můžeš uložit zapnuté optimalizace do souboru a na jiném počítači (nebo po přeinstalaci Windows) je zase načíst. Umístění souboru si vybereš sám.",
            out var exportBody);
        var exportButtons = new WrapPanel();
        exportButtons.Children.Add(GhostButton("Exportovat nastavení…", ExportSettings_Click));
        exportButtons.Children.Add(GhostButton("Importovat nastavení…", ImportSettings_Click));
        exportBody.Children.Add(exportButtons);

        var langCard = MakeCard("Jazyk", "Jazyk aplikace. Texty u jednotlivých optimalizací zatím zůstávají česky.", out var langBody);
        var langPanel = new WrapPanel();
        langPanel.Children.Add(MakeChip("Čeština", "cs", "lang", L.Language == "cs", (_, _) => SetLanguage("cs")));
        langPanel.Children.Add(MakeChip("English", "en", "lang", L.Language == "en", (_, _) => SetLanguage("en")));
        langBody.Children.Add(langPanel);

        var themeCard = MakeCard("Vzhled aplikace", "Výchozí je tmavý vzhled. Světlý vzhled je volitelný.", out var themeBody);
        var themePanel = new WrapPanel();
        themePanel.Children.Add(MakeChip("Tmavý", "dark", "theme", !Core.Theme.IsLight, (_, _) => SetTheme("dark")));
        themePanel.Children.Add(MakeChip("Světlý", "light", "theme", Core.Theme.IsLight, (_, _) => SetTheme("light")));
        themeBody.Children.Add(themePanel);
        themeBody.Children.Add(new TextBlock { Text = "Změna se projeví po restartu aplikace.", Style = (Style)FindResource("SectionText"), Margin = new Thickness(0, 4, 0, 0) });

        // Vložit za nadpis stránky (index 0) v pořadí: samokontrola, jazyk, vzhled, export.
        settingsRoot.Children.Insert(1, selfCard);
        settingsRoot.Children.Insert(2, langCard);
        settingsRoot.Children.Insert(3, themeCard);
        settingsRoot.Children.Insert(4, exportCard);

        UpdateSelfCheckText(null);
        Loaded += async (_, _) =>
        {
            if (_settings.AutoSelfCheck) await RunSelfCheckAsync(false);
        };
    }

    private RadioButton MakeChip(string text, string tag, string group, bool selected, RoutedEventHandler click)
    {
        var chip = new RadioButton
        {
            Style = (Style)FindResource("ChipButton"),
            Content = text,
            Tag = tag,
            GroupName = group,
            IsChecked = selected,
            Margin = new Thickness(0, 0, 6, 6)
        };
        chip.Click += click;
        return chip;
    }

    private void SetLanguage(string code)
    {
        _settings.Language = code;
        _settings.Save();
        MessageBox.Show(this, code == "en"
            ? "The language changes after you restart the app."
            : "Jazyk se změní po restartu aplikace.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SetTheme(string code)
    {
        _settings.Theme = code;
        _settings.Save();
        MessageBox.Show(this, L.T("Změna se projeví po restartu aplikace."), "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowExtraPage(string tag)
    {
        _pageStartup.Visibility = tag == "#startup" ? Visibility.Visible : Visibility.Collapsed;
        if (tag == "#startup") { LoadStartup(); Motion.Page(_pageStartup); }
    }

    // --- samokontrola ---------------------------------------------------------------------------

    private void UpdateSelfCheckText(SelfCheckResult? result)
    {
        if (result == null)
        {
            _selfCheckText.Text = L.English ? "Not checked yet in this session." : "V tomto spuštění zatím nezkontrolováno.";
            return;
        }

        var when = result.When.ToString("d. M. yyyy H:mm");
        if (result.AllOk)
        {
            _selfCheckText.Text = L.English
                ? "All good (" + result.Checked + " optimizations checked, " + when + ")."
                : "Vše v pořádku (zkontrolováno " + result.Checked + " optimalizací, " + when + ").";
            return;
        }

        var lines = new List<string>();
        if (result.Drifted.Count > 0)
            lines.Add((L.English ? "Reverted by the system: " : "Systém nebo jiný program vrátil: ") +
                      string.Join(", ", result.Drifted.Select(d => d.Tweak.Name)));
        lines.AddRange(result.Problems);
        _selfCheckText.Text = string.Join(Environment.NewLine, lines);
    }

    private async Task RunSelfCheckAsync(bool manual)
    {
        if (_selfCheckRunning) return;
        _selfCheckRunning = true;
        try
        {
            var all = _items.Select(i => i.Tweak).ToList();
            var result = await Task.Run(() => SelfCheck.Run(_service, all));
            UpdateSelfCheckText(result);

            if (result.Drifted.Count > 0)
            {
                var names = string.Join("\n• ", result.Drifted.Take(12).Select(d => d.Tweak.Name));
                var more = result.Drifted.Count > 12 ? "\n… a další (" + (result.Drifted.Count - 12) + ")" : "";
                var answer = MessageBox.Show(this,
                    "Samokontrola zjistila, že Windows nebo jiný program vrátil tyto optimalizace, které jsi dříve zapnul(a):\n\n• " + names + more +
                    "\n\nZapnout je znovu?",
                    "Samokontrola", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);

                if (answer == MessageBoxResult.Yes)
                {
                    var targets = _items.Where(i => result.Drifted.Any(d => ReferenceEquals(d.Tweak, i.Tweak))).ToList();
                    if (await EnsureRestorePointAsync())
                    {
                        ShowBusy("Zapínám vrácené optimalizace…");
                        for (var i = 0; i < targets.Count; i++)
                        {
                            BusyText.Text = "Zapínám vrácené optimalizace… (" + (i + 1) + "/" + targets.Count + ")";
                            await RunToggleAsync(targets[i], true);
                        }
                        HideBusy();
                        UpdateSelfCheckText(await Task.Run(() => SelfCheck.Run(_service, all)));
                    }
                }
            }
            else if (manual)
            {
                MessageBox.Show(this, result.AllOk
                        ? (L.English ? "Everything is in order." : "Vše je v pořádku. Zapnuté optimalizace platí.")
                        : string.Join("\n", result.Problems),
                    "Samokontrola", MessageBoxButton.OK, result.AllOk ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Samokontrola selhala: " + ex);
            if (manual) MessageBox.Show(this, "Samokontrola se nepodařila: " + ex.Message, "Samokontrola", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _selfCheckRunning = false;
            _settings.Save();
        }
    }

    // --- spouštěné aplikace ---------------------------------------------------------------------

    private async void LoadStartup()
    {
        List<StartupEntry> list;
        try { list = await Task.Run(StartupManager.List); }
        catch (Exception ex)
        {
            Log.Write("Spouštěné aplikace selhaly: " + ex);
            list = new List<StartupEntry>();
        }

        _startupList.Children.Clear();
        UpdateStartupInfo(list);

        foreach (var entry in list)
        {
            var card = MakeCard(entry.Name, "", out var body, 8);
            var command = entry.Command.Length > 140 ? entry.Command[..140] + "…" : entry.Command;
            body.Children.Add(new TextBlock
            {
                Text = command + Environment.NewLine + entry.Where,
                FontSize = 12,
                Foreground = (Brush)FindResource("MutedBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var check = new CheckBox
            {
                Content = "Spouštět s Windows",
                Foreground = (Brush)FindResource("TextBrush"),
                IsChecked = entry.Enabled
            };
            var captured = entry;
            check.Click += async (_, _) =>
            {
                var want = check.IsChecked == true;
                var (ok, message) = await Task.Run(() => StartupManager.SetEnabled(captured, want));
                if (!ok)
                {
                    check.IsChecked = !want;
                    MessageBox.Show(this, "Změna se nepodařila: " + message, "Spouštěné aplikace", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                UpdateStartupInfo(list);
            };
            body.Children.Add(check);
            _startupList.Children.Add(card);
        }
        L.Apply(_startupList);
    }

    private void UpdateStartupInfo(List<StartupEntry> list)
    {
        var on = list.Count(e => e.Enabled);
        _startupInfo.Text = list.Count == 0
            ? (L.English ? "No startup apps found." : "Nenašel jsem žádné spouštěné aplikace.")
            : (L.English ? "Enabled " + on + " of " + list.Count + " startup apps." : "Spouští se " + on + " z " + list.Count + " aplikací.");
    }

    // --- export a import ------------------------------------------------------------------------

    private void ExportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportovat nastavení",
            Filter = "Nastavení Gasbyn Optimizer (*.json)|*.json",
            FileName = "GasbynOptimizer-nastaveni.json",
            DefaultExt = ".json",
            AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var export = new SettingsExport
            {
                Version = UpdateLogWindow.CurrentVersion(),
                Exported = DateTime.Now.ToString("o"),
                Language = _settings.Language,
                Theme = _settings.Theme,
                Enabled = _items.Where(i => i.IsOn).Select(i => i.Tweak.Id).ToList()
            };
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true }),
                new System.Text.UTF8Encoding(false));
            MessageBox.Show(this, "Nastavení uloženo (" + export.Enabled.Count + " zapnutých optimalizací):\n" + dialog.FileName,
                "Export nastavení", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log.Write("Export selhal: " + ex);
            MessageBox.Show(this, "Export se nepodařil: " + ex.Message, "Export nastavení", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ImportSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importovat nastavení",
            Filter = "Nastavení Gasbyn Optimizer (*.json)|*.json|Všechny soubory (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;

        SettingsExport? import;
        try
        {
            import = JsonSerializer.Deserialize<SettingsExport>(File.ReadAllText(dialog.FileName),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Soubor se nepodařilo přečíst: " + ex.Message, "Import nastavení", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (import == null || import.App != "GasbynOptimizer")
        {
            MessageBox.Show(this, "Tohle není soubor nastavení z Gasbyn Optimizeru.", "Import nastavení", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var wanted = new HashSet<string>(import.Enabled, StringComparer.OrdinalIgnoreCase);
        var toEnable = _items.Where(i => wanted.Contains(i.Tweak.Id) && !i.IsOn && i.IsAvailable && i.IsSupported && !i.IsApplyOnly).ToList();
        var toRevert = _items.Where(i => !wanted.Contains(i.Tweak.Id) && (i.IsOn || i.IsPartial) && !i.IsApplyOnly).ToList();

        var answer = MessageBox.Show(this,
            "Soubor obsahuje " + wanted.Count + " zapnutých optimalizací.\n\n" +
            "Zapnout se jich musí " + toEnable.Count + ", jiných zapnutých máš teď " + toRevert.Count + ".\n\n" +
            "ANO = nastavit přesně podle souboru (ostatní zapnuté se vrátí do původního stavu)\n" +
            "NE = jen zapnout to, co v souboru je, a nic nevracet\n" +
            "ZRUŠIT = nic neimportovat",
            "Import nastavení", MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Cancel) return;

        if (import.Language is "cs" or "en") _settings.Language = import.Language;
        if (import.Theme is "dark" or "light") _settings.Theme = import.Theme;
        _settings.Save();

        var total = toEnable.Count + (answer == MessageBoxResult.Yes ? toRevert.Count : 0);
        if (total > 0)
        {
            if (toEnable.Count > 0 && !await EnsureRestorePointAsync()) return;

            ShowBusy("Importuji nastavení…");
            var done = 0;
            foreach (var item in toEnable)
            {
                BusyText.Text = "Importuji nastavení… (" + (++done) + "/" + total + ")";
                await RunToggleAsync(item, true);
            }
            if (answer == MessageBoxResult.Yes)
            {
                foreach (var item in toRevert)
                {
                    BusyText.Text = "Importuji nastavení… (" + (++done) + "/" + total + ")";
                    await RunToggleAsync(item, false);
                }
            }
            HideBusy();
        }

        MessageBox.Show(this, "Import dokončen. Jazyk a vzhled se změní po restartu aplikace.", "Import nastavení",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
