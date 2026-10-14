using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
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
    private static readonly string CustomTweakConfigPath = Path.Combine(WorkspaceDataDirectory, "custom-tweaks.json");
    private static List<CustomTweakDefinition>? _customTweakDefinitions;
    private static string? _customTweakConfigError;

    private readonly Dictionary<string, ToggleButton> _workspaceSwitches = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _workspaceDriftedTweaks = new();
    private readonly Dictionary<string, TextBlock> _workspaceSwitchStatuses = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingWorkspaceSwitches;
    private bool _workspaceTweakBusy;
    private TextBlock? _workspaceBenchmarkStatus;
    private TextBlock? _workspaceBenchmarkHistory;
    private TextBox? _workspacePresentMonPath;
    private TextBox? _workspaceBenchmarkProcess;
    private Button? _workspaceRunBenchmarkButton;

    private sealed record AdditionalTweakValue(string Path, string Name, int Value);
    private sealed record AdditionalTweakDefinition(string Name, string Description, string Badge, AdditionalTweakValue[] Values);

    // User-authored tweaks are loaded from %LocalAppData%\\GasbynOptimizer\\custom-tweaks.json.
    // Commands in this file run as the current user; inspect them before enabling a tweak.
    private sealed class CustomTweakDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "Tweaks";
        public string OnCommand { get; set; } = "";
        public string OffCommand { get; set; } = "";
        public string OnDescription { get; set; } = "";
        public string OffDescription { get; set; } = "";
        public string IsOnCommand { get; set; } = "";
        public string IsOffCommand { get; set; } = "";
        public string[] Badges { get; set; } = Array.Empty<string>();
    }

    private const string CustomTweakIdPrefix = "custom-json:";

    private static IReadOnlyList<CustomTweakDefinition> GetCustomTweakDefinitions()
    {
        if (_customTweakDefinitions is not null) return _customTweakDefinitions;
        try
        {
            Directory.CreateDirectory(WorkspaceDataDirectory);
            if (!File.Exists(CustomTweakConfigPath))
            {
                var bundledTemplate = Path.Combine(AppContext.BaseDirectory, "custom-tweaks.json");
                if (File.Exists(bundledTemplate)) File.Copy(bundledTemplate, CustomTweakConfigPath);
                else File.WriteAllText(CustomTweakConfigPath, "[]\n");
            }

            var loaded = JsonSerializer.Deserialize<List<CustomTweakDefinition>>(
                File.ReadAllText(CustomTweakConfigPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CustomTweakDefinition>();
            var valid = new List<CustomTweakDefinition>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in loaded)
            {
                if (item is null || !System.Text.RegularExpressions.Regex.IsMatch(item.Id ?? "", @"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$") ||
                    string.IsNullOrWhiteSpace(item.Name) || string.IsNullOrWhiteSpace(item.Description) ||
                    string.IsNullOrWhiteSpace(item.Category) || string.IsNullOrWhiteSpace(item.OnCommand) ||
                    string.IsNullOrWhiteSpace(item.OffCommand) || string.IsNullOrWhiteSpace(item.OnDescription) ||
                    string.IsNullOrWhiteSpace(item.OffDescription) ||
                    string.IsNullOrWhiteSpace(item.IsOnCommand) || string.IsNullOrWhiteSpace(item.IsOffCommand) ||
                    !ids.Add(item.Id))
                    continue;

                item.Badges ??= Array.Empty<string>();
                valid.Add(item);
            }
            _customTweakConfigError = null;
            _customTweakDefinitions = valid;
        }
        catch (Exception ex)
        {
            _customTweakConfigError = ex.Message;
            _customTweakDefinitions = new List<CustomTweakDefinition>();
        }
        return _customTweakDefinitions;
    }

    private static CustomTweakDefinition? GetCustomTweakDefinition(string id)
    {
        if (!id.StartsWith(CustomTweakIdPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        var customId = id[CustomTweakIdPrefix.Length..];
        return GetCustomTweakDefinitions().FirstOrDefault(x => x.Id.Equals(customId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ProbeCustomTweakState(CustomTweakDefinition custom)
    {
        try
        {
            var isOn = RunCustomTweakProbe(custom.IsOnCommand);
            var isOff = RunCustomTweakProbe(custom.IsOffCommand);
            if (isOn && !isOff) return "ON";
            if (isOff && !isOn) return "OFF";
            return "UNKNOWN";
        }
        catch { return "UNKNOWN"; }
    }

    private static bool RunCustomTweakProbe(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "/d /s /c \\"" + command + "\\""
            }
        };
        if (!process.Start()) throw new InvalidOperationException("Kontrolní příkaz se nepodařilo spustit.");
        if (!process.WaitForExit(15000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("Kontrolní příkaz běžel déle než 15 sekund.");
        }
        return process.ExitCode == 0;
    }

    private void AddCustomTweakDefinition_Click(object sender, RoutedEventArgs e)
    {
        var editor = new Window
        {
            Title = "Přidat vlastní optimalizaci",
            Owner = this,
            Width = 760,
            Height = 820,
            MinWidth = 620,
            MinHeight = 650,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = WorkspaceBrush("#101216"),
            Foreground = Brushes.White,
            ResizeMode = ResizeMode.CanResize
        };

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(20) };
        var panel = new StackPanel();
        scroll.Content = panel;
        editor.Content = scroll;

        panel.Children.Add(WorkspaceHeading("PŘIDAT VLASTNÍ OPTIMALIZACI"));
        panel.Children.Add(WorkspaceBody("Vytvoř kartu ON/OFF bez úpravy zdrojového kódu. Stav se při každém otevření stránky a po zapnutí aplikace ověří pomocí dvou kontrolních příkazů, které čtou aktuální Windows. Kontrolní příkaz musí vrátit exit code 0, když je jeho stav pravdivý."));
        panel.Children.Add(WorkspaceBody("Pozor: příkazy se spouští pod tvým Windows účtem. Nepřidávej příkazy, kterým nerozumíš."));

        TextBox AddField(string label, string hint, string initial = "", int height = 34)
        {
            panel.Children.Add(new TextBlock { Text = label, Foreground = WorkspaceBrush("#FF9A52"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 4) });
            var box = new TextBox
            {
                Text = initial,
                ToolTip = hint,
                Height = height,
                Padding = new Thickness(9, 7, 9, 7),
                AcceptsReturn = height > 34,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = height > 34 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                Background = WorkspaceBrush("#171A20"),
                Foreground = Brushes.White,
                BorderBrush = WorkspaceBrush("#343842"),
                CaretBrush = Brushes.White
            };
            panel.Children.Add(box);
            return box;
        }

        var idBox = AddField("ID (unikátní, jen a-z, 0-9, - a _)", "Např. network-latency");
        var nameBox = AddField("Název", "Název zobrazený na kartě");
        var descriptionBox = AddField("Krátký popis", "Popis zobrazený pod názvem");
        var categoryBox = AddField("Kategorie", "Např. Internet, Gaming, Windows nebo Tweaks", "Tweaks");
        var onCommandBox = AddField("Příkaz při ON", "CMD příkaz, který změnu aplikuje.", "", 62);
        var offCommandBox = AddField("Příkaz při OFF", "CMD příkaz, který změnu vrátí nebo vypne.", "", 62);
        var onCheckBox = AddField("Kontrola stavu ON", "CMD test: exit code 0 pouze pokud je optimalizace v systému opravdu ON.", "", 62);
        var offCheckBox = AddField("Kontrola stavu OFF", "CMD test: exit code 0 pouze pokud je optimalizace v systému opravdu OFF.", "", 62);
        var onDescriptionBox = AddField("Co se stane při ON", "Popis skutečné změny");
        var offDescriptionBox = AddField("Co se stane při OFF", "Popis obnovení nebo vypnutí");
        var badgesBox = AddField("Štítky (oddělené čárkou)", "Např. FPS, INTERNET, EXPERIMENTAL, RESTART");

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = WorkspaceButton("Zrušit");
        cancel.Margin = new Thickness(0, 0, 8, 0);
        cancel.Background = WorkspaceBrush("#292D35");
        cancel.Click += (_, _) => editor.DialogResult = false;
        var save = WorkspaceButton("Uložit optimalizaci");
        save.Click += (_, _) =>
        {
            var id = idBox.Text.Trim();
            var current = GetCustomTweakDefinitions().ToList();
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, @"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,63}$"))
            {
                MessageBox.Show(editor, "ID musí začínat písmenem nebo číslem a obsahovat pouze a-z, 0-9, - nebo _. ", "Neplatné ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (current.Any(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(editor, "Toto ID už existuje. Zvol jiné, jedinečné ID.", "Duplicitní ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var definition = new CustomTweakDefinition
            {
                Id = id,
                Name = nameBox.Text.Trim(),
                Description = descriptionBox.Text.Trim(),
                Category = categoryBox.Text.Trim(),
                OnCommand = onCommandBox.Text.Trim(),
                OffCommand = offCommandBox.Text.Trim(),
                IsOnCommand = onCheckBox.Text.Trim(),
                IsOffCommand = offCheckBox.Text.Trim(),
                OnDescription = onDescriptionBox.Text.Trim(),
                OffDescription = offDescriptionBox.Text.Trim(),
                Badges = badgesBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            };
            if (string.IsNullOrWhiteSpace(definition.Name) || string.IsNullOrWhiteSpace(definition.Description) ||
                string.IsNullOrWhiteSpace(definition.Category) || string.IsNullOrWhiteSpace(definition.OnCommand) ||
                string.IsNullOrWhiteSpace(definition.OffCommand) || string.IsNullOrWhiteSpace(definition.IsOnCommand) ||
                string.IsNullOrWhiteSpace(definition.IsOffCommand) || string.IsNullOrWhiteSpace(definition.OnDescription) ||
                string.IsNullOrWhiteSpace(definition.OffDescription))
            {
                MessageBox.Show(editor, "Vyplň všechna pole kromě štítků. Kontrolní příkazy ON a OFF jsou povinné.", "Chybějící údaje", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show(editor, "Uložit vlastní optimalizaci? Při použití přepínače se spustí zadané ON/OFF příkazy. Kontrolní příkazy se spustí při zjišťování stavu. Všechny poběží pod tvým aktuálním Windows účtem.", "Potvrdit vlastní příkazy", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                current.Add(definition);
                Directory.CreateDirectory(WorkspaceDataDirectory);
                File.WriteAllText(CustomTweakConfigPath, JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true }));
                _customTweakDefinitions = null;
                _customTweakConfigError = null;
                editor.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(editor, "Optimalizaci se nepodařilo uložit: " + ex.Message, "Chyba ukládání", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);

        if (editor.ShowDialog() == true)
            ShowPage("Tweaks");
    }

    private static readonly Dictionary<string, AdditionalTweakDefinition> AdditionalTweakDefinitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["show-hidden-files"] = new("Zobrazit skryté soubory", "Zobrazí skryté soubory v Průzkumníku; výkon her tím nestoupne.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Hidden", 1) }),
            ["show-file-extensions"] = new("Zobrazit přípony souborů", "Zobrazí přípony jako .exe a .cmd pro snazší rozpoznání typu souboru.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0) }),
            ["disable-search-highlights"] = new("Vypnout zvýraznění ve vyhledávání", "Omezí doporučený obsah ve vyhledávání Windows.", "WINDOWS", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDynamicSearchBoxEnabled", 0) }),
            ["hide-task-view"] = new("Skrýt tlačítko Zobrazení úloh", "Skryje tlačítko Task View na hlavním panelu.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowTaskViewButton", 0) }),
            ["hide-taskbar-badges"] = new("Skrýt odznaky aplikací na hlavním panelu", "Vypne odznaky s počtem oznámení na ikonách hlavního panelu.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarBadges", 0) }),
            ["disable-sync-provider-notifications"] = new("Vypnout tipy synchronizace v Průzkumníku", "Vypne tipy synchronizace; samotná synchronizace zůstane zapnutá.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowSyncProviderNotifications", 0) }),
            ["hide-meet-now"] = new("Skrýt Meet Now na hlavním panelu", "Skryje ikonu Meet Now, pokud ji daná verze Windows používá.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1) }),
            ["hide-people-taskbar"] = new("Skrýt ikonu Lidé", "Skryje starší panel Lidé na hlavním panelu, pokud je dostupný.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "PeopleBand", 0) }),
            ["disable-start-app-tracking"] = new("Vypnout sledování spouštěných aplikací", "Omezí sledování aplikací používané pro personalizaci nabídky Start.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackProgs", 0) }),
            ["disable-start-recent-documents"] = new("Vypnout sledování nedávných dokumentů", "Omezí historii nedávno otevřených dokumentů v některých částech Windows.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_TrackDocs", 0) }),
            ["disable-recently-added-apps"] = new("Skrýt upozornění na nově přidané aplikace", "Vypne zvýrazňování nově nainstalovaných aplikací v nabídce Start.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "Start_NotifyNewApps", 0) }),
            ["disable-toast-notifications"] = new("Vypnout oznámení Windows", "Vypne systémová oznámení uživatele; můžeš přehlédnout důležitá upozornění.", "POZOR", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\PushNotifications", "ToastEnabled", 0) }),
            ["disable-notification-center"] = new("Vypnout Centrum oznámení", "Skryje Centrum oznámení; může omezit přístup k oznámením aplikací.", "POZOR", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\Explorer", "DisableNotificationCenter", 1) }),
            ["disable-cloud-app-notifications"] = new("Omezit cloudová oznámení aplikací", "Zakáže cloudová oznámení aplikací, pokud daná verze Windows zásadu podporuje.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\CurrentVersion\PushNotifications", "NoCloudApplicationNotification", 1) }),
            ["disable-spotlight-lockscreen"] = new("Vypnout funkce Windows Spotlight", "Vypne vybrané funkce Spotlight; dopad na výkon je zanedbatelný.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightFeatures", 1) }),
            ["disable-spotlight-welcome"] = new("Vypnout Spotlight uvítací obrazovky", "Omezí doporučení Spotlight v uvítacích obrazovkách Windows.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsSpotlightWindowsWelcomeExperience", 1) }),
            ["disable-third-party-suggestions"] = new("Vypnout návrhy aplikací třetích stran", "Omezí doporučení aplikací třetích stran ve Windows.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableThirdPartySuggestions", 1) }),
            ["disable-windows-consumer-features"] = new("Omezit spotřebitelský doporučený obsah", "Omezí automaticky doporučovaný obsah Windows; dostupnost závisí na edici.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1) }),
            ["disable-tailored-experiences"] = new("Vypnout personalizované tipy", "Omezí personalizaci tipů podle diagnostických dat.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0) }),
            ["disable-feedback-notifications"] = new("Vypnout výzvy ke zpětné vazbě", "Omezí výzvy Windows k hodnocení a odeslání zpětné vazby.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\DataCollection", "DoNotShowFeedbackNotifications", 1) }),
            ["disable-activity-feed"] = new("Omezit historii aktivit Windows", "Vypne historii aktivit a její publikování/synchronizaci; některé funkce Windows se změní.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0), new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\System", "PublishUserActivities", 0), new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\System", "UploadUserActivities", 0) }),
            ["disable-cross-device-resume"] = new("Vypnout pokračování mezi zařízeními", "Vypne pokračování aktivit mezi zařízeními pro aktuálního uživatele.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\CrossDeviceResume\Configuration", "IsResumeAllowed", 0) }),
            ["disable-bing-start-search"] = new("Omezit webové výsledky v nabídce Start", "Vypne hodnotu BingSearchEnabled; chování závisí na verzi Windows.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0) }),
            ["disable-autoplay"] = new("Vypnout automatické přehrávání médií", "Vypne automatické přehrávání médií pro uživatele; zařízení budeš otevírat ručně.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers", "DisableAutoplay", 1) }),
            ["disable-autorun"] = new("Omezit automatické spouštění médií", "Blokuje automatické spouštění obsahu v Průzkumníku pro aktuálního uživatele.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoDriveTypeAutoRun", 255) }),
            ["disable-suggested-apps"] = new("Vypnout návrhy aplikací v nabídce Start", "Omezí jeden z mechanismů doporučování aplikací Windows.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338389Enabled", 0) }),
            ["disable-settings-tips"] = new("Vypnout tipy Windows", "Omezí vybraný kanál tipů a doporučení systému.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338393Enabled", 0) }),
            ["disable-welcome-after-updates"] = new("Vypnout uvítací tipy po aktualizacích", "Omezí uvítací obrazovky s tipy po aktualizacích Windows.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-310093Enabled", 0) }),
            ["disable-lockscreen-spotlight-overlay"] = new("Vypnout překryvné tipy na zamykací obrazovce", "Vypne překryvné tipy Spotlight na zamykací obrazovce.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenOverlayEnabled", 0) }),
            ["disable-lockscreen-rotation"] = new("Vypnout střídání obrázků zamykací obrazovky", "Vypne rotaci obrázků Windows Spotlight na zamykací obrazovce.", "VOLITELNÉ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "RotatingLockScreenEnabled", 0) }),
            ["disable-clipboard-sync"] = new("Vypnout synchronizaci schránky mezi zařízeními", "Vypne synchronizaci schránky mezi zařízeními; místní schránka zůstane dostupná.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Clipboard", "AllowCrossDeviceClipboard", 0) }),
            ["disable-gamebar-startup-panel"] = new("Vypnout úvodní panel Xbox Game Bar", "Vypne úvodní panel Game Bar, nikoliv samotnou aplikaci.", "GAMING", new[] { new AdditionalTweakValue(@"Software\Microsoft\GameBar", "ShowStartupPanel", 0) }),
            ["disable-gamebar-nexus"] = new("Vypnout přesměrování Game Bar na Nexus", "Změní způsob otevírání rozhraní Xbox Game Bar; chování se může lišit.", "GAMING", new[] { new AdditionalTweakValue(@"Software\Microsoft\GameBar", "UseNexusForGameBarEnabled", 0) }),
            ["disable-edge-prelaunch"] = new("Vypnout přednačítání Microsoft Edge", "Vypne přednačítání Edge, pokud zásadu daná verze podporuje.", "APLIKACE", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Edge\Main", "AllowPrelaunch", 0) }),
            ["disable-edge-background"] = new("Vypnout běh Edge na pozadí", "Zakáže běh procesů Edge na pozadí přes uživatelskou zásadu.", "APLIKACE", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Edge", "BackgroundModeEnabled", 0) }),
            ["enable-edge-sleeping-tabs"] = new("Povolit uspávání neaktivních panelů Edge", "Zapne zásadu uspávání panelů Edge; může snížit jeho využití zdrojů.", "APLIKACE", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Edge", "SleepingTabsEnabled", 1) }),
            ["disable-chrome-background"] = new("Vypnout běh Chrome na pozadí", "Zakáže běh aplikací Chrome na pozadí přes uživatelskou zásadu.", "APLIKACE", new[] { new AdditionalTweakValue(@"Software\Policies\Google\Chrome", "BackgroundModeEnabled", 0) }),
            ["disable-chrome-metrics"] = new("Vypnout odesílání metrik Chrome zásadou", "Vypne zásadu metrik Chrome; nastavení může být spravováno prohlížečem.", "APLIKACE", new[] { new AdditionalTweakValue(@"Software\Policies\Google\Chrome", "MetricsReportingEnabled", 0) }),
            ["disable-search-location"] = new("Omezit polohu ve vyhledávání Windows", "Zakáže používání polohy vyhledáváním, pokud Windows zásadu podporuje.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation", 0) }),
            ["disable-lockscreen-status"] = new("Vypnout tipy Spotlight na zamykací obrazovce", "Omezí další kanál doporučeného obsahu na zamykací obrazovce.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338387Enabled", 0) }),
            ["disable-settings-suggested-content"] = new("Omezit další doporučený obsah Windows", "Vypne další uživatelský kanál doporučení, pokud jej Windows používá.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353696Enabled", 0) }),
            ["disable-start-tips"] = new("Vypnout další tipy v nabídce Start", "Omezí další kanál tipů Windows v nabídce Start.", "SOUKROMÍ", new[] { new AdditionalTweakValue(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338392Enabled", 0) }),
            ["disable-app-location-access"] = new("Zakázat aplikacím přístup k poloze", "Odepře aplikacím přístup k poloze; může omezit mapy a další aplikace využívající polohu.", "POZOR", new[] { new AdditionalTweakValue(@"Software\Policies\Microsoft\Windows\AppPrivacy", "LetAppsAccessLocation", 2) }),
        };

    private static AdditionalTweakDefinition GetAdditionalTweakDefinition(string id) =>
        AdditionalTweakDefinitions.TryGetValue(id, out var definition)
            ? definition
            : throw new InvalidOperationException("Neznámý optimalizační modul: " + id);

    private static bool IsAdditionalTweakAtOptimizedState(string id)
    {
        if (!AdditionalTweakDefinitions.TryGetValue(id, out var definition)) return true;
        foreach (var value in definition.Values)
        {
            using var key = Registry.CurrentUser.OpenSubKey(value.Path);
            var actual = key?.GetValue(value.Name);
            if (actual is null || Convert.ToInt32(actual, CultureInfo.InvariantCulture) != value.Value)
                return false;
        }
        return true;
    }

    private static string ReadAdditionalTweakStatus(string id)
    {
        if (!AdditionalTweakDefinitions.TryGetValue(id, out _)) return "stav není dostupný";
        return IsAdditionalTweakAtOptimizedState(id) ? "nastavení aplikováno" : "výchozí nebo jiné nastavení";
    }

    private void BuildTweaksWorkspace(bool gamingOnly = false)
    {
        _workspaceSwitches.Clear();
        _workspaceSwitchStatuses.Clear();

        if (!gamingOnly)
        {
            var managerCard = WorkspaceCard();
            var managerPanel = new StackPanel();
            managerPanel.Children.Add(WorkspaceHeading("VLASTNÍ OPTIMALIZACE"));
            managerPanel.Children.Add(WorkspaceBody("Přidej si vlastní optimalizaci s ON/OFF příkazy a dvěma kontrolami skutečného stavu Windows. Přepínač nebude používat historii kliknutí jako zdroj pravdy."));
            var addButton = WorkspaceButton("＋  PŘIDAT VLASTNÍ OPTIMALIZACI");
            addButton.Margin = new Thickness(0, 12, 0, 0);
            addButton.Click += AddCustomTweakDefinition_Click;
            managerPanel.Children.Add(addButton);
            managerCard.Child = managerPanel;
            SecondaryContent.Children.Add(managerCard);
        }

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

            AddWorkspaceTweakCard(
                "Vypnout akceleraci myši",
                "Vypne Windows pointer acceleration (Enhance pointer precision). Může pomoct se stálým pohybem myši; osobní preference se liší.",
                "mouse-acceleration",
                "ON: nastaví MouseSpeed, MouseThreshold1 a MouseThreshold2 na 0. OFF: obnoví původní hodnoty.", "VOLITELNÉ", "GAMING", "AIM");

            AddWorkspaceTweakCard(
                "Fortnite – preferovat výkonnou GPU",
                "Požádá Windows, aby pro Fortnite preferoval výkonnou grafiku. Na PC s jedinou GPU se nemusí projevit.",
                "gpu-preference",
                "ON: nastaví uživatelskou DirectX preferenci GpuPreference=2 pro Fortnite. OFF: obnoví původní hodnotu. Pokud detekce GPU selže, tweak je označen jako nepodporovaný; pokročilý režim dovolí ruční obejití upozornění.", "PODMÍNĚNĚ", "FPS", "POKROČILÉ");

            AddWorkspaceTweakCard(
                "Vypnout Fullscreen Optimizations pro Fortnite",
                "Nastaví kompatibilitní volbu Windows pro Fortnite. Účinek závisí na cestě ke skutečné instalaci hry; po změně hru restartuj.",
                "fullscreen-optimizations",
                "ON: nastaví DISABLEDXMAXIMIZEDWINDOWEDMODE pro nalezený Fortnite executable. OFF: obnoví původní hodnotu. Není-li executable nalezen, modul je označen jako nepodporovaný.", "POKROČILÉ", "FORTNITE");

            _ = RefreshWorkspaceTweakStatesAsync();
            return;
        }

        AddWorkspaceTweakCard(
            "Hardware-accelerated GPU scheduling (HAGS)",
            "Zapne HAGS ve Windows. Podpora a přínos závisí na GPU, ovladači a verzi Windows; po změně může být nutný restart.",
            "hags",
            "ON: nastaví HwSchMode=2 v HKLM. OFF: obnoví původní hodnotu. Pokud ovladač nebo Windows tuto funkci nepodporuje, nemusí mít změna účinek.", "POKROČILÉ", "GPU", "RESTART");

        AddWorkspaceTweakCard(
            "Výkonové vizuální efekty",
            "Přepne Windows do režimu nejlepšího výkonu. Některé animace a vizuální efekty se mohou změnit.",
            "visual-effects",
            "ON: nastaví VisualFXSetting=2. OFF: obnoví původní režim nebo hodnotu odstraní, pokud původně nebyla nastavena.", "VOLITELNÉ", "VZHLED", "ODEZVA");

        AddWorkspaceTweakCard(
            "High Performance power plan",
            "Aktivuje vestavěný plán Vysoký výkon. Může zvýšit spotřebu energie a teploty.",
            "power-plan",
            "ON: aktivuje plán napájení Vysoký výkon. OFF: aktivuje původní ID plánu uložené před změnou. Vyšší spotřeba a teploty jsou možné.", "POKROČILÉ", "VÝKON", "SPOTŘEBA");

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

        AddWorkspaceTweakCard(
            "Zkrátit odklad spouštění aplikací",
            "Nastaví uživatelský odklad při spouštění aplikací na 0 ms. Nezrychluje samotné aplikace a přínos závisí na systému.",
            "startup-delay",
            "ON: nastaví StartupDelayInMSec=0 pro aktuálního uživatele. OFF: obnoví původní hodnotu.", "VOLITELNÉ", "STARTUP");

        AddWorkspaceTweakCard(
            "Vypnout animace hlavního panelu",
            "Vypne animace hlavního panelu Windows. Jde hlavně o vizuální změnu; vyšší FPS ve hrách není garantováno.",
            "taskbar-animations",
            "ON: nastaví TaskbarAnimations=0. OFF: obnoví původní hodnotu.", "VOLITELNÉ", "UI", "ODEZVA");

        AddWorkspaceTweakCard(
            "Skrýt tlačítko Widgets",
            "Skryje tlačítko Widgetů na hlavním panelu. Samotná funkce Widgets se tím neodinstaluje.",
            "widgets",
            "ON: nastaví TaskbarDa=0 pro aktuálního uživatele. OFF: obnoví původní hodnotu.", "VOLITELNÉ", "UI");

        AddWorkspaceTweakCard(
            "Omezit webové návrhy ve vyhledávání",
            "Vypne návrhy z webu v hledání Windows pro aktuálního uživatele. Dostupnost nastavení závisí na edici a verzi Windows.",
            "search-suggestions",
            "ON: nastaví DisableSearchBoxSuggestions=1 v uživatelských zásadách Průzkumníka. OFF: obnoví původní hodnotu.", "SOUKROMÍ", "SEARCH");

        AddWorkspaceTweakCard(
            "Omezit reklamní tipy a doporučení Windows",
            "Vypne několik uživatelských návrhů, reklamní ID a doporučený obsah Windows. Nejde o měřitelnou FPS optimalizaci.",
            "recommendations",
            "ON: nastaví vypnutí vybraných doporučení a reklamního ID pro aktuálního uživatele. OFF: obnoví všechny původní hodnoty.", "SOUKROMÍ", "UI");

        AddWorkspaceTweakCard(
            "Zobrazit skryté soubory",
            "Zobrazí skryté soubory v Průzkumníku; výkon her tím nestoupne.",
            "show-hidden-files",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Zobrazit přípony souborů",
            "Zobrazí přípony jako .exe a .cmd pro snazší rozpoznání typu souboru.",
            "show-file-extensions",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout zvýraznění ve vyhledávání",
            "Omezí doporučený obsah ve vyhledávání Windows.",
            "disable-search-highlights",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "WINDOWS", "WINDOWS");

        AddWorkspaceTweakCard(
            "Skrýt tlačítko Zobrazení úloh",
            "Skryje tlačítko Task View na hlavním panelu.",
            "hide-task-view",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Skrýt odznaky aplikací na hlavním panelu",
            "Vypne odznaky s počtem oznámení na ikonách hlavního panelu.",
            "hide-taskbar-badges",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout tipy synchronizace v Průzkumníku",
            "Vypne tipy synchronizace; samotná synchronizace zůstane zapnutá.",
            "disable-sync-provider-notifications",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Skrýt Meet Now na hlavním panelu",
            "Skryje ikonu Meet Now, pokud ji daná verze Windows používá.",
            "hide-meet-now",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Skrýt ikonu Lidé",
            "Skryje starší panel Lidé na hlavním panelu, pokud je dostupný.",
            "hide-people-taskbar",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout sledování spouštěných aplikací",
            "Omezí sledování aplikací používané pro personalizaci nabídky Start.",
            "disable-start-app-tracking",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout sledování nedávných dokumentů",
            "Omezí historii nedávno otevřených dokumentů v některých částech Windows.",
            "disable-start-recent-documents",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Skrýt upozornění na nově přidané aplikace",
            "Vypne zvýrazňování nově nainstalovaných aplikací v nabídce Start.",
            "disable-recently-added-apps",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout oznámení Windows",
            "Vypne systémová oznámení uživatele; můžeš přehlédnout důležitá upozornění.",
            "disable-toast-notifications",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "POZOR", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout Centrum oznámení",
            "Skryje Centrum oznámení; může omezit přístup k oznámením aplikací.",
            "disable-notification-center",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "POZOR", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit cloudová oznámení aplikací",
            "Zakáže cloudová oznámení aplikací, pokud daná verze Windows zásadu podporuje.",
            "disable-cloud-app-notifications",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout funkce Windows Spotlight",
            "Vypne vybrané funkce Spotlight; dopad na výkon je zanedbatelný.",
            "disable-spotlight-lockscreen",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout Spotlight uvítací obrazovky",
            "Omezí doporučení Spotlight v uvítacích obrazovkách Windows.",
            "disable-spotlight-welcome",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout návrhy aplikací třetích stran",
            "Omezí doporučení aplikací třetích stran ve Windows.",
            "disable-third-party-suggestions",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit spotřebitelský doporučený obsah",
            "Omezí automaticky doporučovaný obsah Windows; dostupnost závisí na edici.",
            "disable-windows-consumer-features",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout personalizované tipy",
            "Omezí personalizaci tipů podle diagnostických dat.",
            "disable-tailored-experiences",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout výzvy ke zpětné vazbě",
            "Omezí výzvy Windows k hodnocení a odeslání zpětné vazby.",
            "disable-feedback-notifications",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit historii aktivit Windows",
            "Vypne historii aktivit a její publikování/synchronizaci; některé funkce Windows se změní.",
            "disable-activity-feed",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout pokračování mezi zařízeními",
            "Vypne pokračování aktivit mezi zařízeními pro aktuálního uživatele.",
            "disable-cross-device-resume",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit webové výsledky v nabídce Start",
            "Vypne hodnotu BingSearchEnabled; chování závisí na verzi Windows.",
            "disable-bing-start-search",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout automatické přehrávání médií",
            "Vypne automatické přehrávání médií pro uživatele; zařízení budeš otevírat ručně.",
            "disable-autoplay",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit automatické spouštění médií",
            "Blokuje automatické spouštění obsahu v Průzkumníku pro aktuálního uživatele.",
            "disable-autorun",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout návrhy aplikací v nabídce Start",
            "Omezí jeden z mechanismů doporučování aplikací Windows.",
            "disable-suggested-apps",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout tipy Windows",
            "Omezí vybraný kanál tipů a doporučení systému.",
            "disable-settings-tips",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout uvítací tipy po aktualizacích",
            "Omezí uvítací obrazovky s tipy po aktualizacích Windows.",
            "disable-welcome-after-updates",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout překryvné tipy na zamykací obrazovce",
            "Vypne překryvné tipy Spotlight na zamykací obrazovce.",
            "disable-lockscreen-spotlight-overlay",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout střídání obrázků zamykací obrazovky",
            "Vypne rotaci obrázků Windows Spotlight na zamykací obrazovce.",
            "disable-lockscreen-rotation",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "VOLITELNÉ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout synchronizaci schránky mezi zařízeními",
            "Vypne synchronizaci schránky mezi zařízeními; místní schránka zůstane dostupná.",
            "disable-clipboard-sync",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout úvodní panel Xbox Game Bar",
            "Vypne úvodní panel Game Bar, nikoliv samotnou aplikaci.",
            "disable-gamebar-startup-panel",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "GAMING", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout přesměrování Game Bar na Nexus",
            "Změní způsob otevírání rozhraní Xbox Game Bar; chování se může lišit.",
            "disable-gamebar-nexus",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "GAMING", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout přednačítání Microsoft Edge",
            "Vypne přednačítání Edge, pokud zásadu daná verze podporuje.",
            "disable-edge-prelaunch",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "APLIKACE", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout běh Edge na pozadí",
            "Zakáže běh procesů Edge na pozadí přes uživatelskou zásadu.",
            "disable-edge-background",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "APLIKACE", "WINDOWS");

        AddWorkspaceTweakCard(
            "Povolit uspávání neaktivních panelů Edge",
            "Zapne zásadu uspávání panelů Edge; může snížit jeho využití zdrojů.",
            "enable-edge-sleeping-tabs",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "APLIKACE", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout běh Chrome na pozadí",
            "Zakáže běh aplikací Chrome na pozadí přes uživatelskou zásadu.",
            "disable-chrome-background",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "APLIKACE", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout odesílání metrik Chrome zásadou",
            "Vypne zásadu metrik Chrome; nastavení může být spravováno prohlížečem.",
            "disable-chrome-metrics",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "APLIKACE", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit polohu ve vyhledávání Windows",
            "Zakáže používání polohy vyhledáváním, pokud Windows zásadu podporuje.",
            "disable-search-location",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout tipy Spotlight na zamykací obrazovce",
            "Omezí další kanál doporučeného obsahu na zamykací obrazovce.",
            "disable-lockscreen-status",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Omezit další doporučený obsah Windows",
            "Vypne další uživatelský kanál doporučení, pokud jej Windows používá.",
            "disable-settings-suggested-content",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Vypnout další tipy v nabídce Start",
            "Omezí další kanál tipů Windows v nabídce Start.",
            "disable-start-tips",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "SOUKROMÍ", "WINDOWS");

        AddWorkspaceTweakCard(
            "Zakázat aplikacím přístup k poloze",
            "Odepře aplikacím přístup k poloze; může omezit mapy a další aplikace využívající polohu.",
            "disable-app-location-access",
            "ON: uloží původní hodnoty registru včetně hodnot, které předtím neexistovaly. OFF: obnoví původní hodnoty nebo nově vytvořené hodnoty odstraní.", "POZOR", "WINDOWS");

        foreach (var custom in GetCustomTweakDefinitions())
        {
            var customId = CustomTweakIdPrefix + custom.Id;
            var badges = new List<string> { custom.Category };
            badges.AddRange(custom.Badges.Where(x => !string.IsNullOrWhiteSpace(x)));
            var tooltip = $"ON: {custom.OnDescription} OFF: {custom.OffDescription}";
            AddWorkspaceTweakCard(custom.Name, custom.Description, customId, tooltip, badges.ToArray());
        }

        if (!string.IsNullOrWhiteSpace(_customTweakConfigError))
            AddInfoCard("CUSTOM TWEAKS JSON – CHYBA", $"Konfiguraci se nepodařilo načíst: {_customTweakConfigError}. Oprav JSON soubor: {CustomTweakConfigPath}", "Open Tweaks");
        else
            AddInfoCard("VLASTNÍ TWEAKY", $"Přidávej vlastní položky do souboru {CustomTweakConfigPath}. Po úpravě souboru aplikaci restartuj. Příkazy se spouštějí přes CMD pod aktuálním uživatelem; před zapnutím si je zkontroluj.", "Open Tweaks");

        var hw = _hardwareInventory;
        var advanced = OptimizerPreferencesStore.Load().AdvancedRecommendations;
        var note = hw is null
            ? "Hardware nebyl zatím načten. Obecné změny zůstávají dostupné; ovladačové a BIOS zásahy se neprovádějí."
            : hw.BuildRecommendations(advanced);
        AddInfoCard("HARDWARE-AWARE DOPORUČENÍ", note, "Hardware Detection");
        AddAmdCompatibilityInfo(hw, advanced);
        _ = RefreshWorkspaceTweakStatesAsync();
    }

    private bool IsWorkspaceTweakIncompatible(string id)
    {
        if (string.Equals(id, "gpu-preference", StringComparison.OrdinalIgnoreCase))
        {
            var gpu = _hardwareInventory?.Gpu;
            return string.IsNullOrWhiteSpace(gpu) ||
                   gpu.Contains("GPU nedostupné", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(id, "fullscreen-optimizations", StringComparison.OrdinalIgnoreCase))
            return !File.Exists(FindFortniteExecutablePath());

        return false;
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
        var incompatible = IsWorkspaceTweakIncompatible(id);
        var advanced = OptimizerPreferencesStore.Load().AdvancedRecommendations;
        var locked = incompatible && !advanced;
        object detailForHover = locked
            ? "🔒 Tato optimalizace není podporována na zjištěné konfiguraci. Zapni pokročilý režim v Settings, pokud ji chceš odemknout na vlastní odpovědnost."
            : BuildTweakToolTip(title, description, tooltip);
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
        card.Opacity = locked ? 0.48 : 1.0;
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
        if (incompatible)
            badgePanel.Children.Add(CreateTweakBadge(advanced ? "NEPODPOROVÁNO" : "NEKOMPATIBILNÍ"));
        if (locked)
            badgePanel.Children.Add(CreateTweakBadge("ZAMČENO"));
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

        var switchPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (locked)
        {
            var lockIcon = new TextBlock
            {
                Text = "🔒",
                FontSize = 16,
                Foreground = (Brush)FindResource("TextDim"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                ToolTip = "Zamčeno — zapni pokročilý režim v Settings."
            };
            switchPanel.Children.Add(lockIcon);
        }

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("SwitchToggleButton"),
            IsChecked = false,
            IsEnabled = !locked,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            ToolTip = locked
                ? "🔒 Nepodporováno na této konfiguraci. Pokročilý režim v Settings umožní obejít toto omezení."
                : incompatible && advanced
                    ? "NEPODPOROVÁNO — povoleno pouze díky pokročilému režimu. Změna může selhat nebo nemít účinek."
                    : BuildTweakToolTip(title, description, tooltip)
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
        switchPanel.Children.Add(toggle);
        Grid.SetColumn(switchPanel, 1);
        root.Children.Add(switchPanel);
        card.Child = root;
        SecondaryContent.Children.Add(card);
    }

    private Border CreateTweakBadge(string label)
    {
        var palette = label.Trim().ToUpperInvariant() switch
        {
            "DOPORUČENO" => ("#143126", "#285D45", "#67E0A8"),
            "PODMÍNĚNĚ" => ("#302516", "#644824", "#FFD08A"),
            "EXPERIMENTÁLNÍ" or "EXPERIMENTAL" => ("#351C18", "#6D342A", "#FF9B87"),
            "POKROČILÉ" => ("#271D3A", "#4D3676", "#D0B5FF"),
            "NOVÉ" => ("#152B3D", "#2D5575", "#94D0FF"),
            "NEKOMPATIBILNÍ" or "INCOMPATIBLE" => ("#3B171B", "#812B34", "#FF7B86"),
            "ZAMČENO" => ("#24262B", "#454952", "#A8ADB7"),
            "SOUKROMÍ" => ("#1D2433", "#364B71", "#B7C9FF"),
            "STARTUP" or "SEARCH" or "UI" or "AIM" or "MOUSE" => ("#1A1D23", "#343842", "#B8BDC6"),
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
            "EXPERIMENTÁLNÍ" or "EXPERIMENTAL" => "Může mít nečekané výsledky. Používej pouze po přečtení detailů.",
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
                var systemStatus = actual.GetValueOrDefault(id, "stav není dostupný");
                var isCustom = GetCustomTweakDefinition(id) is not null;
                var active = isCustom ? systemStatus == "ON" : state.ContainsKey(id);
                if (_workspaceSwitches.TryGetValue(id, out var toggle))
                {
                    toggle.IsChecked = isCustom && systemStatus == "UNKNOWN" ? null : active;
                    toggle.IsEnabled = !(isCustom && systemStatus == "UNKNOWN");
                }
                if (_workspaceSwitchStatuses.TryGetValue(id, out var status))
                {
                    status.Text = isCustom
                        ? $"Skutečný stav Windows: {systemStatus}"
                        : $"Windows: {systemStatus}  ·  Správa Gasbyn Optimizerem: {(active ? "ON — původní stav uložen" : "OFF")}";
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
        if (GetCustomTweakDefinition(id) is { } customDefinition)
        {
            var command = enable ? customDefinition.OnCommand : customDefinition.OffCommand;
            if (MessageBox.Show(this, $"Spustit vlastní příkaz {(enable ? "ON" : "OFF")} pro „{customDefinition.Name}“?\n\n{command}\n\nPříkaz se spustí pod aktuálním Windows účtem.", "Potvrdit vlastní příkaz", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                _loadingWorkspaceSwitches = true;
                toggle.IsChecked = !enable;
                _loadingWorkspaceSwitches = false;
                return;
            }
        }
        if (_workspaceTweakBusy)
        {
            _loadingWorkspaceSwitches = true;
            toggle.IsChecked = !enable;
            _loadingWorkspaceSwitches = false;
            return;
        }

        _workspaceTweakBusy = true;
        foreach (var item in _workspaceSwitches.Values) item.IsEnabled = false;
        status.Text = GetCustomTweakDefinition(id) is not null
            ? (enable ? "Spouštím vlastní ON CMD příkaz…" : "Spouštím vlastní OFF CMD příkaz…")
            : (enable ? "Ukládám původní stav a aplikuji optimalizaci…" : "Obnovuji původní hodnoty…");
        try
        {
            await Task.Run(() => ApplyWorkspaceTweak(id, enable));
            await RefreshWorkspaceTweakStatesAsync();
            status.Text = GetCustomTweakDefinition(id) is not null
                ? (enable
                    ? $"Vlastní ON CMD příkaz úspěšně dokončen; stav přepínače je uložen. · {ReadWorkspaceActualStatus(id)}"
                    : $"Vlastní OFF CMD příkaz úspěšně dokončen; stav přepínače je uložen. · {ReadWorkspaceActualStatus(id)}")
                : (enable
                    ? $"Optimalizace zapnuta. Původní stav je uložen. Pro plné použití změn doporučuji restart PC. · {ReadWorkspaceActualStatus(id)}"
                    : $"Původní hodnoty obnoveny. · {ReadWorkspaceActualStatus(id)}");
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

    private static string WorkspaceTweakName(string id) => GetCustomTweakDefinition(id)?.Name ?? (id switch
    {
        "hags" => "Hardware-accelerated GPU scheduling (HAGS)",
        "game-mode" => "Windows Game Mode",
        "game-dvr" => "Vypnout Game DVR na pozadí",
        "mouse-acceleration" => "Vypnout akceleraci myši",
        "gpu-preference" => "Fortnite – preferovat výkonnou GPU",
        "fullscreen-optimizations" => "Vypnout Fullscreen Optimizations pro Fortnite",
        "startup-delay" => "Zkrátit odklad spouštění aplikací",
        "taskbar-animations" => "Vypnout animace hlavního panelu",
        "widgets" => "Skrýt tlačítko Widgets",
        "search-suggestions" => "Omezit webové návrhy ve vyhledávání",
        "recommendations" => "Omezit reklamní tipy a doporučení Windows",
        "visual-effects" => "Výkonové vizuální efekty",
        "power-plan" => "High Performance power plan",
        "transparency" => "Vypnout efekty průhlednosti",
        "fast-menus" => "Rychlejší nabídky a animace oken",
        _ => AdditionalTweakDefinitions.TryGetValue(id, out var additional) ? additional.Name : id
    });

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
            if (GetCustomTweakDefinition(id) is { } custom)
            {
                var command = enable ? custom.OnCommand : custom.OffCommand;
                operations.Add($"CUSTOM CMD ({(enable ? "ON" : "OFF")}): {command}");
                RunCustomTweakCommand(command);
                operations.Add("CUSTOM CMD: proces skončil s návratovým kódem 0.");
                var detected = ProbeCustomTweakState(custom);
                operations.Add("CUSTOM STATE PROBE: " + detected + " (zjištěno z aktuálního systému, nikoli z historie).");
                OptimizerAuditLog.WriteChange(id, custom.Name, action, true, operations,
                    enable ? "Vlastní ON CMD příkaz byl úspěšně spuštěn; stav se ověřuje kontrolními příkazy." : "Vlastní OFF CMD příkaz byl úspěšně spuštěn; stav se ověřuje kontrolními příkazy.");
                return;
            }

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
                    case "hags":
                        SetWorkspaceMachineDword(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, operations);
                        break;
                    case "game-mode":
                        SetWorkspaceDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, operations);
                        SetWorkspaceDword(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1, operations);
                        break;
                    case "game-dvr":
                        SetWorkspaceDword(@"System\GameConfigStore", "GameDVR_Enabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, operations);
                        break;
                    case "mouse-acceleration":
                        SetWorkspaceString(@"Control Panel\Mouse", "MouseSpeed", "0", operations);
                        SetWorkspaceString(@"Control Panel\Mouse", "MouseThreshold1", "0", operations);
                        SetWorkspaceString(@"Control Panel\Mouse", "MouseThreshold2", "0", operations);
                        break;
                    case "gpu-preference":
                        SetWorkspaceString(@"Software\Microsoft\DirectX\UserGpuPreferences", "FortniteClient-Win64-Shipping.exe", "GpuPreference=2;", operations);
                        break;
                    case "fullscreen-optimizations":
                    {
                        var executable = FindFortniteExecutablePath();
                        if (!File.Exists(executable))
                            throw new InvalidOperationException("Fortnite executable nebyl nalezen. Tento tweak je na této instalaci nepodporovaný.");
                        SetWorkspaceString(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", executable, "~ DISABLEDXMAXIMIZEDWINDOWEDMODE", operations);
                        break;
                    }
                    case "startup-delay":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", 0, operations);
                        break;
                    case "taskbar-animations":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0, operations);
                        break;
                    case "widgets":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0, operations);
                        break;
                    case "search-suggestions":
                        SetWorkspaceDword(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, operations);
                        break;
                    case "recommendations":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled", 0, operations);
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, operations);
                        break;
                    case "visual-effects":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2, operations);
                        break;
                    case "power-plan":
                        operations.Add("COMMAND: powercfg.exe /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                        operations.Add("COMMAND OUTPUT: " + RunWorkspacePowerCfg("/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c").Trim());
                        break;
                    case "transparency":
                        SetWorkspaceDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0, operations);
                        break;
                    case "fast-menus":
                        SetWorkspaceString(@"Control Panel\Desktop", "MenuShowDelay", "100", operations);
                        SetWorkspaceString(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", "0", operations);
                        break;
                    default:
                    {
                        var additional = GetAdditionalTweakDefinition(id);
                        foreach (var value in additional.Values)
                            SetWorkspaceDword(value.Path, value.Name, value.Value, operations);
                        break;
                    }
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
        if (id == "hags")
        {
            const string path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
            const string name = "HwSchMode";
            using var key = Registry.LocalMachine.OpenSubKey(path);
            var value = key?.GetValue(name);
            snapshot.RegistryValues.Add(new WorkspaceTweakRegistrySnapshot
            {
                Hive = "HKLM", Path = path, Name = name, Exists = value is not null,
                Kind = value is null ? RegistryValueKind.DWord.ToString() : key!.GetValueKind(name).ToString(),
                Data = value is null ? "" : SerializeWorkspaceRegistryValue(value)
            });
            return snapshot;
        }

        if (id == "fullscreen-optimizations")
        {
            var executable = FindFortniteExecutablePath();
            if (!File.Exists(executable))
                throw new InvalidOperationException("Fortnite executable nebyl nalezen.");
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers");
            var current = key?.GetValue(executable);
            snapshot.RegistryValues.Add(new WorkspaceTweakRegistrySnapshot
            {
                Path = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
                Name = executable,
                Exists = current is not null,
                Kind = current is null ? RegistryValueKind.String.ToString() : key!.GetValueKind(executable).ToString(),
                Data = current is null ? "" : SerializeWorkspaceRegistryValue(current)
            });
            return snapshot;
        }

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
            "mouse-acceleration" => new[]
            {
                (Path: @"Control Panel\Mouse", Name: "MouseSpeed"),
                (Path: @"Control Panel\Mouse", Name: "MouseThreshold1"),
                (Path: @"Control Panel\Mouse", Name: "MouseThreshold2")
            },
            "gpu-preference" => new[]
            {
                (Path: @"Software\Microsoft\DirectX\UserGpuPreferences", Name: "FortniteClient-Win64-Shipping.exe")
            },
            "fullscreen-optimizations" => throw new InvalidOperationException("Fullscreen optimization backup se musí zachytit podle konkrétní cesty k Fortnite."),
            "startup-delay" => new[]
            {
                (Path: @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", Name: "StartupDelayInMSec")
            },
            "taskbar-animations" => new[]
            {
                (Path: @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name: "TaskbarAnimations")
            },
            "widgets" => new[]
            {
                (Path: @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", Name: "TaskbarDa")
            },
            "search-suggestions" => new[]
            {
                (Path: @"Software\Policies\Microsoft\Windows\Explorer", Name: "DisableSearchBoxSuggestions")
            },
            "recommendations" => new[]
            {
                (Path: @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name: "SystemPaneSuggestionsEnabled"),
                (Path: @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name: "SoftLandingEnabled"),
                (Path: @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name: "SilentInstalledAppsEnabled"),
                (Path: @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name: "SubscribedContent-338388Enabled"),
                (Path: @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", Name: "SubscribedContent-353694Enabled"),
                (Path: @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", Name: "Enabled")
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
            _ => GetAdditionalTweakDefinition(id).Values.Select(value => (Path: value.Path, Name: value.Name)).ToArray()
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
            var fullName = $"{item.Hive}\\{item.Path}\\{item.Name}";
            if (item.Exists)
            {
                operations.Add($"REGISTRY RESTORE: {fullName}; Type={item.Kind}; OriginalValue={item.Data}");
                var root = string.Equals(item.Hive, "HKLM", StringComparison.OrdinalIgnoreCase)
                    ? Registry.LocalMachine : Registry.CurrentUser;
                using var key = root.CreateSubKey(item.Path)
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
                var root = string.Equals(item.Hive, "HKLM", StringComparison.OrdinalIgnoreCase)
                    ? Registry.LocalMachine : Registry.CurrentUser;
                using var key = root.OpenSubKey(item.Path, writable: true);
                key?.DeleteValue(item.Name, throwOnMissingValue: false);
            }
        }
    }

    private static void SetWorkspaceMachineDword(string path, string name, int value, List<string> operations)
    {
        operations.Add($"REGISTRY SET: HKLM\\{path}\\{name}; Type=REG_DWORD; Value={value}");
        using var key = Registry.LocalMachine.CreateSubKey(path) ?? throw new InvalidOperationException("Nepodařilo se otevřít HKLM\\" + path);
        key.SetValue(name, value, RegistryValueKind.DWord);
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
            operations.Add($"BACKUP REGISTRY: {item.Hive}\\{item.Path}\\{item.Name}; Exists={item.Exists}; Kind={item.Kind}; Data={item.Data}");
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

            if (GetCustomTweakDefinition(id) is { } custom)
                return ProbeCustomTweakState(custom);

            return id switch
            {
                "hags" => (Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers")?.GetValue("HwSchMode") is int hags && hags == 2)
                    ? "HAGS zapnutý (restart může být nutný)" : "HAGS výchozí/vypnutý",
                "game-mode" => Read(@"Software\Microsoft\GameBar", "AutoGameModeEnabled") is int mode && mode == 1
                    ? "Game Mode zapnutý" : "Game Mode výchozí/vypnutý",
                "game-dvr" => Read(@"System\GameConfigStore", "GameDVR_Enabled") is int capture && capture == 0 &&
                              Read(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") is int app && app == 0
                    ? "Game DVR vypnutý" : "Game DVR výchozí/zapnutý",
                "mouse-acceleration" => Read(@"Control Panel\Mouse", "MouseSpeed")?.ToString() == "0" &&
                                        Read(@"Control Panel\Mouse", "MouseThreshold1")?.ToString() == "0" &&
                                        Read(@"Control Panel\Mouse", "MouseThreshold2")?.ToString() == "0"
                    ? "akcelerace myši vypnutá" : "nastavení myši výchozí/jiné",
                "gpu-preference" => Read(@"Software\Microsoft\DirectX\UserGpuPreferences", "FortniteClient-Win64-Shipping.exe")?.ToString() == "GpuPreference=2;"
                    ? "Windows preferuje výkonnou GPU pro Fortnite" : "výchozí preference GPU",
                "fullscreen-optimizations" => Read(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", FindFortniteExecutablePath())?.ToString()?.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase) == true
                    ? "Fullscreen Optimizations vypnuté pro Fortnite" : "Fullscreen Optimizations ve výchozím stavu",
                "startup-delay" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec") is int delay && delay == 0
                    ? "odklad spuštění nastaven na 0 ms" : "odklad spuštění výchozí/jiný",
                "taskbar-animations" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations") is int animations && animations == 0
                    ? "animace hlavního panelu vypnuté" : "animace výchozí/zapnuté",
                "widgets" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa") is int widgets && widgets == 0
                    ? "tlačítko Widgets skryté" : "tlačítko Widgets výchozí/zobrazené",
                "search-suggestions" => Read(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") is int search && search == 1
                    ? "webové návrhy ve vyhledávání vypnuté" : "webové návrhy výchozí/zapnuté",
                "recommendations" => Read(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled") is int suggestions && suggestions == 0
                    ? "vybrané tipy a reklamní doporučení vypnuté" : "tipy a doporučení výchozí/jiné",
                "visual-effects" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting") is int visual && visual == 2
                    ? "režim nejlepšího výkonu" : "výchozí/jiný režim",
                "transparency" => Read(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency") is int transparency && transparency == 0
                    ? "průhlednost vypnutá" : "průhlednost výchozí/zapnutá",
                "fast-menus" => $"MenuShowDelay={Read(@"Control Panel\Desktop", "MenuShowDelay") ?? "default"}, MinAnimate={Read(@"Control Panel\Desktop\WindowMetrics", "MinAnimate") ?? "default"}",
                _ => ReadAdditionalTweakStatus(id)
            };
        }
        catch (Exception ex) { return "stav nelze přečíst: " + ex.Message; }
    }

    private void CheckWorkspaceDriftOnStartup()
    {
        _workspaceDriftedTweaks.Clear();
        Dictionary<string, WorkspaceTweakSnapshot> saved;
        try { saved = LoadWorkspaceState(); }
        catch (Exception ex)
        {
            MessageBox.Show("Nepodařilo se přečíst zálohy optimalizací. Žádná změna nebyla provedena.\n\n" + ex.Message,
                "Kontrola optimalizací", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        foreach (var id in saved.Keys)
        {
            // CMD tweaks do not expose a generic, reliable way to inspect their real-world state.
            if (id.StartsWith(CustomTweakIdPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsWorkspaceTweakAtOptimizedState(id))
                _workspaceDriftedTweaks.Add(id);
        }

        if (_workspaceDriftedTweaks.Count == 0)
            return;

        var lines = _workspaceDriftedTweaks.Select(id =>
            "• " + WorkspaceTweakName(id) + "\n  Poslední stav Gasbyn: ZAPNUTO\n  Aktuálně ve Windows: " + ReadWorkspaceActualStatus(id));
        DriftDifferences.Text = string.Join("\n\n", lines);
        DriftOverlay.Visibility = Visibility.Visible;
    }

    private static bool IsWorkspaceTweakAtOptimizedState(string id)
    {
        object? Read(string path, string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetValue(name);
        }

        return id switch
        {
            "hags" => Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers")?.GetValue("HwSchMode") is int hags && hags == 2,
            "game-mode" => Read(@"Software\Microsoft\GameBar", "AutoGameModeEnabled") is int gm && gm == 1 &&
                           Read(@"Software\Microsoft\GameBar", "AllowAutoGameMode") is int allow && allow == 1,
            "game-dvr" => Read(@"System\GameConfigStore", "GameDVR_Enabled") is int capture && capture == 0 &&
                          Read(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled") is int app && app == 0,
            "mouse-acceleration" => Read(@"Control Panel\Mouse", "MouseSpeed")?.ToString() == "0" &&
                                    Read(@"Control Panel\Mouse", "MouseThreshold1")?.ToString() == "0" &&
                                    Read(@"Control Panel\Mouse", "MouseThreshold2")?.ToString() == "0",
            "gpu-preference" => Read(@"Software\Microsoft\DirectX\UserGpuPreferences", "FortniteClient-Win64-Shipping.exe")?.ToString() == "GpuPreference=2;",
            "fullscreen-optimizations" => Read(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers", FindFortniteExecutablePath())?.ToString()?.Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE", StringComparison.OrdinalIgnoreCase) == true,
            "startup-delay" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec") is int delay && delay == 0,
            "taskbar-animations" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations") is int anim && anim == 0,
            "widgets" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa") is int widgets && widgets == 0,
            "search-suggestions" => Read(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") is int search && search == 1,
            "recommendations" => Read(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled") is int suggestions && suggestions == 0 &&
                                 Read(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SoftLandingEnabled") is int soft && soft == 0 &&
                                 Read(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled") is int silent && silent == 0 &&
                                 Read(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-338388Enabled") is int subA && subA == 0 &&
                                 Read(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SubscribedContent-353694Enabled") is int subB && subB == 0 &&
                                 Read(@"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") is int ads && ads == 0,
            "visual-effects" => Read(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting") is int visual && visual == 2,
            "transparency" => Read(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency") is int trans && trans == 0,
            "fast-menus" => Read(@"Control Panel\Desktop", "MenuShowDelay")?.ToString() == "100" &&
                            Read(@"Control Panel\Desktop\WindowMetrics", "MinAnimate")?.ToString() == "0",
            "power-plan" => RunWorkspacePowerCfg("/getactivescheme").Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase),
            _ => IsAdditionalTweakAtOptimizedState(id)
        };
    }

    private void KeepCurrentDrift_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saved = LoadWorkspaceState();
            foreach (var id in _workspaceDriftedTweaks)
            {
                saved.Remove(id);
                OptimizerAuditLog.WriteChange(id, WorkspaceTweakName(id), "KEEP_CURRENT", true,
                    new[] { "Startup drift: uživatel zvolil ponechat aktuální nastavení Windows." },
                    "Aktuální stav ponechán; původní návratová záloha pro tuto optimalizaci byla odpojena.");
            }
            SaveWorkspaceState(saved);
            DriftOverlay.Visibility = Visibility.Collapsed;
            ShowPage("Tweaks");
        }
        catch (Exception ex)
        {
            DriftDifferences.Text += "\n\nNepodařilo se uložit volbu: " + ex.Message;
        }
    }

    private void RestoreOriginalDrift_Click(object sender, RoutedEventArgs e)
    {
        var errors = new List<string>();
        foreach (var id in _workspaceDriftedTweaks.ToArray())
        {
            try { ApplyWorkspaceTweak(id, false); }
            catch (Exception ex) { errors.Add(WorkspaceTweakName(id) + ": " + ex.Message); }
        }

        DriftOverlay.Visibility = Visibility.Collapsed;
        if (errors.Count > 0)
            MessageBox.Show("Některá nastavení se nepodařilo obnovit:\n\n" + string.Join("\n", errors),
                "Obnovení optimalizací", MessageBoxButton.OK, MessageBoxImage.Warning);
        ShowPage("Tweaks");
    }

    private static string FindFortniteExecutablePath()
    {
        var candidates = new List<string>();
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(Path.Combine(programFiles, "Epic Games", "Fortnite", "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"));
        candidates.Add(Path.Combine(programFilesX86, "Epic Games", "Fortnite", "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"));
        candidates.Add(Path.Combine(local, "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"));
        foreach (var drive in new[] { "C:", "D:", "E:" })
            candidates.Add(Path.Combine(drive + Path.DirectorySeparatorChar, "Epic Games", "Fortnite", "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"));

        var manifestRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        try
        {
            if (Directory.Exists(manifestRoot))
            {
                foreach (var manifest in Directory.EnumerateFiles(manifestRoot, "*.item"))
                {
                    var json = File.ReadAllText(manifest);
                    if (!json.Contains("Fortnite", StringComparison.OrdinalIgnoreCase)) continue;
                    var match = System.Text.RegularExpressions.Regex.Match(json, @"""InstallLocation""\s*:\s*""(?<path>[^""]+)""");
                    if (match.Success)
                        candidates.Insert(0, Path.Combine(match.Groups["path"].Value.Replace("\\\\", "\\"), "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe"));
                }
            }
        }
        catch { }

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static void RunCustomTweakCommand(string command)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Arguments = "/d /s /c \"" + command + "\""
            }
        };
        if (!process.Start()) throw new InvalidOperationException("CMD příkaz se nepodařilo spustit.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("CMD příkaz běžel déle než 120 sekund a byl ukončen.");
        }
        Task.WaitAll(stdoutTask, stderrTask);
        var stdout = stdoutTask.Result.Trim();
        var stderr = stderrTask.Result.Trim();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"CMD skončil s kódem {process.ExitCode}. {stderr} {stdout}".Trim());
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
        public string Hive { get; set; } = "HKCU";
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

        var preparePresentMon = WorkspaceButton("Automaticky stáhnout / aktualizovat PresentMon");
        preparePresentMon.Margin = new Thickness(0, 10, 0, 0);
        preparePresentMon.Click += async (_, _) =>
        {
            preparePresentMon.IsEnabled = false;
            try
            {
                var progress = new Progress<string>(message =>
                {
                    if (_workspaceBenchmarkStatus is not null) _workspaceBenchmarkStatus.Text = message;
                });
                var path = await PresentMonInstaller.EnsureInstalledAsync(progress);
                if (_workspacePresentMonPath is not null) _workspacePresentMonPath.Text = path;
                SaveWorkspacePresentMonPath(path);
                if (_workspaceBenchmarkStatus is not null)
                    _workspaceBenchmarkStatus.Text = $"PresentMon je připraven: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                if (_workspaceBenchmarkStatus is not null)
                    _workspaceBenchmarkStatus.Text = "Automatické stažení PresentMon selhalo: " + ex.Message;
            }
            finally { preparePresentMon.IsEnabled = true; }
        };
        stack.Children.Add(preparePresentMon);

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
        if (string.IsNullOrWhiteSpace(processName) || processName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            status.Text = "Zadej platný název procesu, například FortniteClient-Win64-Shipping.exe.";
            return;
        }

        button.IsEnabled = false;
        status.Text = "Kontroluji PresentMon…";
        try
        {
            var autoManagedPath = Path.GetFullPath(PresentMonInstaller.InstalledPath);
            if (!File.Exists(exePath) ||
                string.Equals(Path.GetFullPath(exePath), autoManagedPath, StringComparison.OrdinalIgnoreCase))
            {
                var progress = new Progress<string>(message => status.Text = message);
                exePath = await PresentMonInstaller.EnsureInstalledAsync(progress);
                if (_workspacePresentMonPath is not null) _workspacePresentMonPath.Text = exePath;
            }
            if (!File.Exists(exePath))
                throw new FileNotFoundException("PresentMon.exe nebyl nalezen.");

            // Validate the target before starting the timer; an invalid process name or
            // a game that is still loading must not produce a misleading empty benchmark.
            var processBaseName = Path.GetFileNameWithoutExtension(processName.Trim());
            var gameProcesses = Process.GetProcessesByName(processBaseName);
            try
            {
                if (gameProcesses.Length == 0)
                    throw new InvalidOperationException($"Proces hry „{processBaseName}“ nebyl nalezen. Spusť Fortnite, počkej až bude načtený a zkontroluj název procesu.");
            }
            finally
            {
                foreach (var gameProcess in gameProcesses) gameProcess.Dispose();
            }

            SaveWorkspacePresentMonPath(exePath);
            status.Text = "Spouštím PresentMon s požadovanými oprávněními…";
            var folder = Path.Combine(WorkspaceDataDirectory, "FpsTests");
            Directory.CreateDirectory(folder);
            var output = Path.Combine(folder, $"presentmon-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            var startInfo = new ProcessStartInfo(exePath)
            {
                // Use Windows' normal UAC consent flow when PresentMon needs elevated ETW access.
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
            };
            startInfo.ArgumentList.Add("--process_name");
            startInfo.ArgumentList.Add(processBaseName);
            startInfo.ArgumentList.Add("--output_file");
            startInfo.ArgumentList.Add(output);
            using var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start()) throw new InvalidOperationException("PresentMon se nepodařilo spustit.");
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException("Spuštění PresentMonu bylo zrušeno v potvrzení Windows UAC. Povol spuštění, aby mohl test pokračovat.", ex);
            }

            for (var seconds = 60; seconds > 0; seconds--)
            {
                status.Text = $"Měření běží… zbývá {seconds} s. Nech Fortnite aktivní.";
                await Task.Delay(1000);
                if (process.HasExited) break;
            }
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            try { await process.WaitForExitAsync(); } catch { }
            await Task.Delay(1500);

            var frameTimes = ParseWorkspaceFrameTimes(output);
            if (frameTimes.Count < 30)
            {
                var csvExists = File.Exists(output);
                var csvSize = csvExists ? new FileInfo(output).Length : 0;
                throw new InvalidOperationException(
                    $"PresentMon zaznamenal jen {frameTimes.Count} platných snímků. CSV: {(csvExists ? $"{csvSize} B" : "nevytvořeno")}. " +
                    "Zkontroluj, že Fortnite běží během celého testu, není minimalizovaný a že PresentMonu povolíš UAC. " +
                    "Log měření zůstává ve složce FpsTests pro diagnostiku.");
            }

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
        try
        {
            var saved = File.Exists(WorkspacePresentMonPath) ? File.ReadAllText(WorkspacePresentMonPath).Trim() : "";
            if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;
            return PresentMonInstaller.IsInstalled ? PresentMonInstaller.InstalledPath : "";
        }
        catch { return PresentMonInstaller.IsInstalled ? PresentMonInstaller.InstalledPath : ""; }
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
