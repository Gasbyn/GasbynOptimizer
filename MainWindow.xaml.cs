using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GasbynOptimizer.Core;
using GasbynOptimizer.Models;
using GasbynOptimizer.Tweaks.Gaming;

namespace GasbynOptimizer;

public partial class MainWindow : Window
{
    private readonly TweakEngine _tweakEngine = new();

    public MainWindow()
    {
        InitializeComponent();
        _tweakEngine.Register(GameMode.Create());
        LoadSystemInfo();
        ShowPage("Dashboard");
    }

    private void LoadSystemInfo()
    {
        CpuValue.Text = GetCpuName();
        GpuValue.Text = GetGpuName();
        RamValue.Text = $"{GetRamGb()} GB";
    }

    private static string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (ManagementObject item in searcher.Get())
            {
                var name = item["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name)) return ShortenHardwareName(name);
            }
        }
        catch { }
        return "Unknown CPU";
    }

    private static string GetGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            foreach (ManagementObject item in searcher.Get())
            {
                var name = item["Name"]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)) return ShortenHardwareName(name);
            }
        }
        catch { }
        return "Unknown GPU";
    }

    private static int GetRamGb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                if (ulong.TryParse(item["TotalPhysicalMemory"]?.ToString(), out var bytes)) return Math.Max(1, (int)Math.Round(bytes / 1024d / 1024d / 1024d));
            }
        }
        catch { }
        return 0;
    }

    private static string ShortenHardwareName(string value)
    {
        if (value.Length <= 30) return value;
        return value.Replace("(R)", "", StringComparison.Ordinal).Replace("(TM)", "", StringComparison.Ordinal).Replace("CPU", "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is not null) ShowPage(button.Tag.ToString() ?? "Dashboard");
    }

    private void SetActiveNav(string page)
    {
        foreach (var child in NavPanel.Children)
        {
            if (child is not Button button) continue;
            var active = string.Equals(button.Tag?.ToString(), page, StringComparison.OrdinalIgnoreCase);
            button.Background = active ? (Brush)FindResource("OrangeSoft") : Brushes.Transparent;
            button.Foreground = active ? Brushes.White : (Brush)FindResource("TextMuted");
            button.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    private void ShowPage(string page)
    {
        PageTitle.Text = page;
        PageSubtitle.Text = page switch
        {
            "Dashboard" => "Přehled systému a rychlé akce",
            "Tweaks" => "Bezpečné a vratné optimalizace Windows",
            "Cleanup" => "Rychlé a konzervativní čištění",
            "Deep Clean" => "Hloubkový scan cache a dočasných dat bez automatického mazání",
            "Startup" => "Přehled aplikací spouštěných se systémem",
            "Apps Manager" => "Kontrola, odinstalace a bezpečný scan zbytků",
            "Gaming" => "Herní profily a výkonové kontroly",
            "Network" => "Síťová diagnostika a bezpečné optimalizace",
            "Health Check" => "Rychlá kontrola stavu Windows a disku",
            "Benchmark" => "Lehký lokální test bez zatěžování systému na dlouhou dobu",
            "Backup" => "Historie a správa záloh před systémovými změnami",
            "PC Info" => "Hardware a základní informace o počítači",
            "Settings" => "Nastavení aplikace",
            _ => "Gasbyn Optimizer"
        };
        SetActiveNav(page);
        if (page == "Dashboard")
        {
            DashboardView.Visibility = Visibility.Visible;
            SecondaryView.Visibility = Visibility.Collapsed;
        }
        else
        {
            DashboardView.Visibility = Visibility.Collapsed;
            SecondaryView.Visibility = Visibility.Visible;
            BuildSecondaryPage(page);
        }
        ContentHost.BeginStoryboard((Storyboard)FindResource("PageEnter"));
    }

    private void BuildSecondaryPage(string page)
    {
        SecondaryContent.Children.Clear();
        AddPageHeader(page);
        switch (page)
        {
            case "Tweaks":
                var gameMode = _tweakEngine.Get("game-mode");
                var gameModeStatus = gameMode is null ? TweakStatus.Unknown : gameMode.GetStatus(new TweakContext());
                AddTweakCard("Windows Game Mode", $"Aktuální stav: {FormatStatus(gameModeStatus)}. Před změnou se uloží backup.", "FREE", "Manage Game Mode");
                AddTweakCard("Hardware Accelerated GPU Scheduling", "Bezpečná kontrola aktuálního HAGS nastavení.", "FREE", "Scan status");
                AddTweakCard("High Performance Power Plan", "Přepnutí napájecího plánu bez mazání existujících plánů.", "PREMIUM", "Ready");
                AddTweakCard("Windows Gaming Services", "Kontrola souvisejících herních komponent bez jejich slepého vypínání.", "PREMIUM", "Scan");
                AddTweakCard("Fullscreen Optimization Check", "Kontrola kompatibility fullscreen nastavení pro hry.", "PREMIUM", "Scan");
                break;

            case "Cleanup":
                AddInfoCard("SAFE CLEANUP", "Rychlý scan běžných dočasných složek. Nic se nemaže bez potvrzení.", "Scan");
                AddTweakCard("User TEMP", "Dočasné soubory aktuálního uživatele.", "FREE", "Scan");
                AddTweakCard("Windows Update Cache", "Stažené aktualizační soubory, které Windows umí znovu vytvořit.", "FREE", "Scan");
                AddTweakCard("Delivery Optimization Cache", "Cache doručování aktualizací Windows.", "PREMIUM", "Scan");
                AddTweakCard("Shader Cache", "Běžné cache grafických komponent a aplikací.", "PREMIUM", "Scan");
                break;

            case "Deep Clean":
                AddInfoCard("DEEP CLEAN", "Projde bezpečně známé cache/log/temp lokace. Výsledek pouze zobrazí; mazání bude vždy ruční a potvrzené.", "Start scan");
                AddTweakCard("Windows + User TEMP", "Dočasné soubory a souhrnná velikost dostupná k úklidu.", "FREE", "Scan");
                AddTweakCard("Browser Cache", "Chrome, Edge a Firefox cache v profilu uživatele.", "FREE", "Scan");
                AddTweakCard("Crash Dumps & Logs", "Starší uživatelské crash dumpy a běžné logy.", "PREMIUM", "Scan");
                AddTweakCard("GPU / Shader Cache", "Známé shader/cache lokace, které lze znovu vytvořit.", "PREMIUM", "Scan");
                AddTweakCard("Windows Update / Delivery Cache", "Cache aktualizací a doručování obsahu.", "PREMIUM", "Scan");
                AddTweakCard("Deep Scan Summary", "Souhrn kandidátů, které Deep Clean nalezl.", "PREMIUM", "Run Deep Scan");
                break;

            case "Startup":
                AddInfoCard("STARTUP MANAGER", "Zde později přidáme bezpečné Enable/Disable s backupem. Základní přehled je připraven.", "View startup");
                AddTweakCard("Startup Registry Entries", "Přehled běžných Windows Run/RunOnce položek.", "FREE", "Read only");
                AddTweakCard("Startup Folder", "Přehled zástupců spouštěných při přihlášení.", "FREE", "Read only");
                AddTweakCard("Scheduled Startup Tasks", "Kontrola běžných úloh, které se spouštějí při přihlášení.", "PREMIUM", "Scan");
                break;

            case "Apps Manager":
                AddInfoCard("APPLICATION MANAGER", "Plný Apps Manager má oficiální uninstall, App Checker a Deep Uninstall se selektivním výběrem zbytků.", "Open manager");
                AddTweakCard("Installed Applications", "Načte klasické desktop aplikace a dostupné uninstall informace.", "FREE", "Open");
                AddTweakCard("App Checker", "Heuristická kontrola účelu aplikace a potenciálního dopadu.", "FREE", "Open");
                AddTweakCard("Deep Uninstall", "Nejdřív oficiální odinstalátor, potom scan zbytků a ruční výběr.", "PREMIUM", "Open");
                break;

            case "Gaming":
                AddInfoCard("GAMING OPTIMIZER", "Herní sekce bude držet bezpečné, vratné změny. Žádné náhodné registry ani vypínání důležitých Windows služeb.", "Scan gaming");
                AddTweakCard("Windows Game Mode", "Zapnutí Game Mode s backupem a restore.", "FREE", "Manage Game Mode");
                AddTweakCard("GPU Scheduling", "Kontrola HAGS a souvisejících nastavení.", "FREE", "Scan status");
                AddTweakCard("Power Plan", "Kontrola aktuálního power plánu.", "PREMIUM", "Scan");
                AddTweakCard("Fortnite Profile", "Bezpečná kontrola známých Fortnite konfigurací a cache.", "PREMIUM", "Scan");
                AddTweakCard("NVIDIA Profile", "Připravený modul pro kontrolu NVIDIA nastavení bez slepého přepisování.", "PREMIUM", "Scan");
                break;

            case "Network":
                AddInfoCard("NETWORK OPTIMIZER", "Diagnostika bez agresivního vypínání síťových funkcí.", "Run network check");
                AddTweakCard("Connection Info", "Adapter, link speed, IPv4/IPv6 a základní stav připojení.", "FREE", "Check");
                AddTweakCard("DNS Check", "Zobrazí aktuální DNS konfiguraci a umožní později bezpečnou změnu.", "FREE", "Check");
                AddTweakCard("TCP/IP Health", "Základní diagnostika TCP/IP stacku.", "PREMIUM", "Check");
                AddTweakCard("Network Reset Tools", "Připravený modul pro řízený reset s jasným upozorněním.", "PREMIUM", "Ready");
                break;

            case "Health Check":
                AddInfoCard("SYSTEM HEALTH", "Rychlá kontrola Windows, volného místa a základních komponent.", "Run health check");
                AddTweakCard("System Drive", GetSystemDriveStatus(), "FREE", "Check");
                AddTweakCard("Windows Version", Environment.OSVersion.VersionString, "FREE", "Detected");
                AddTweakCard(".NET Runtime", Environment.Version.ToString(), "FREE", "Detected");
                AddTweakCard("Hardware Detection", "CPU / GPU / RAM jsou dostupné.", "FREE", "Refresh");
                break;

            case "Benchmark":
                AddInfoCard("LIGHTWEIGHT BENCHMARK", "Krátký lokální test CPU bez instalace dalších programů. Slouží hlavně pro Before/After porovnání.", "Run benchmark");
                AddTweakCard("CPU Quick Test", "Krátký deterministický výpočet a časování.", "FREE", "Run");
                AddTweakCard("Memory Snapshot", $"Installed RAM: {RamValue.Text}", "FREE", "Check");
                AddTweakCard("Disk Space", GetSystemDriveStatus(), "FREE", "Check");
                AddTweakCard("Before / After", "Historie benchmarků bude použita pro porovnání změn.", "PREMIUM", "Ready");
                break;

            case "Backup":
                AddInfoCard("BACKUP CENTER", "Centrální místo pro backupy vytvořené před změnami. Každý tweak by měl umět restore.", "Refresh");
                AddTweakCard("Tweak Backups", "Zálohy vytvořené TweakEngine před systémovými změnami.", "FREE", "View");
                AddTweakCard("Restore All", "Budoucí bezpečné vrácení podporovaných změn.", "PREMIUM", "Ready");
                AddTweakCard("Change History", "Přehled aplikovaných změn a jejich výsledků.", "FREE", "View");
                break;

            case "PC Info":
                AddInfoCard("CPU", CpuValue.Text, "Refresh");
                AddInfoCard("GPU", GpuValue.Text, "Refresh");
                AddInfoCard("RAM", RamValue.Text, "Refresh");
                AddInfoCard("Windows", Environment.OSVersion.VersionString, "Detected");
                AddInfoCard("System Drive", GetSystemDriveStatus(), "Check");
                break;

            case "Settings":
                AddInfoCard("GENERAL", "Automatické kontroly, vzhled a chování aplikace.", "Coming soon");
                AddInfoCard("BACKUPS", "Správa záloh před aplikací systémových změn.", "Coming soon");
                AddInfoCard("LICENSE", "Free / Premium licence budou ověřovány serverem.", "Coming soon");
                AddInfoCard("UPDATES", "Bezpečná kontrola nové verze aplikace.", "Coming soon");
                break;

            default:
                AddInfoCard("PAGE", "Tato stránka ještě není připravená.", "OK");
                break;
        }
    }

    private static string FormatStatus(TweakStatus status) => status switch
    {
        TweakStatus.Enabled => "ENABLED",
        TweakStatus.Disabled => "DISABLED",
        TweakStatus.NotConfigured => "NOT CONFIGURED",
        _ => "UNKNOWN"
    };

    private string GetSystemDriveStatus()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            var free = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            var total = drive.TotalSize / 1024d / 1024d / 1024d;
            return $"{free:0.0} GB free / {total:0.0} GB total";
        }
        catch { return "Disk information unavailable"; }
    }

    private void AddPageHeader(string page)
    {
        var header = new TextBlock
        {
            Text = page switch
            {
                "Tweaks" => "OPTIMIZATION MODULES", "Cleanup" => "CLEANUP CENTER", "Deep Clean" => "DEEP CLEAN CENTER",
                "Startup" => "STARTUP MANAGER", "Apps Manager" => "APPLICATION MANAGER", "Gaming" => "GAMING OPTIMIZER",
                "Network" => "NETWORK OPTIMIZER", "Health Check" => "SYSTEM HEALTH", "Benchmark" => "PERFORMANCE LAB",
                "Backup" => "BACKUP CENTER", "PC Info" => "SYSTEM INFORMATION", "Settings" => "APPLICATION SETTINGS", _ => "GASBYN OPTIMIZER"
            },
            FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Orange"), Margin = new Thickness(0, 0, 0, 14)
        };
        SecondaryContent.Children.Add(header);
    }

    private void AddTweakCard(string title, string description, string tier, string actionText)
    {
        var border = new Border { Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12), Effect = (System.Windows.Media.Effects.DropShadowEffect)FindResource("CardShadow") };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = tier, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = tier == "PREMIUM" ? (Brush)FindResource("Orange") : (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 10, 0, 0) });
        Grid.SetColumn(stack, 0); grid.Children.Add(stack);
        var button = new Button { Content = actionText, Style = (Style)FindResource("ActionButton"), VerticalAlignment = VerticalAlignment.Center, Tag = title };
        button.Click += title switch
        {
            "Windows Game Mode" => GameModeAction_Click,
            "Windows + User TEMP" => DeepScan_Click,
            "Browser Cache" => DeepScan_Click,
            "Crash Dumps & Logs" => DeepScan_Click,
            "GPU / Shader Cache" => DeepScan_Click,
            "Windows Update / Delivery Cache" => DeepScan_Click,
            "Deep Scan Summary" => DeepScan_Click,
            _ => PlaceholderAction_Click
        };
        Grid.SetColumn(button, 1); grid.Children.Add(button); border.Child = grid; SecondaryContent.Children.Add(border);
    }

    private void AddInfoCard(string title, string description, string actionText)
    {
        var border = new Border { Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Border"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12), Effect = (System.Windows.Media.Effects.DropShadowEffect)FindResource("CardShadow") };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Orange") });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 13, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 6, 20, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(stack, 0); grid.Children.Add(stack);
        var button = new Button { Content = actionText, Style = (Style)FindResource("NavButton"), VerticalAlignment = VerticalAlignment.Center, Tag = title };
        button.Click += title == "APPLICATION MANAGER" ? OpenAppsManager_Click : actionText == "Start scan" ? DeepScan_Click : PlaceholderAction_Click;
        Grid.SetColumn(button, 1); grid.Children.Add(button); border.Child = grid; SecondaryContent.Children.Add(border);
    }

    private void GameModeAction_Click(object sender, RoutedEventArgs e)
    {
        var tweak = _tweakEngine.Get("game-mode");
        if (tweak is null) { MessageBox.Show("Game Mode tweak nebyl nalezen.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        var status = tweak.GetStatus(new TweakContext());
        var enable = status != TweakStatus.Enabled;
        var question = enable ? "Zapnout Windows Game Mode? Původní nastavení se před změnou uloží do backupu." : "Game Mode je už zapnutý. Chceš obnovit původní nastavení z backupu?";
        if (MessageBox.Show(question, "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var context = new TweakContext(); var result = enable ? tweak.Apply(context) : tweak.Restore(context);
        MessageBox.Show(result.Message, "Gasbyn Optimizer", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        ShowPage("Tweaks");
    }

    private void OpenAppsManager_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new GasbynOptimizer.Apps.AppsManagerWindow { Owner = this };
            window.ShowDialog();
        }
        catch (Exception ex) { MessageBox.Show($"Apps Manager se nepodařilo otevřít:\n{ex.Message}", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void DeepScan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            var result = await Task.Run(ScanDeepClean);
            MessageBox.Show(result, "Deep Clean Scan", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Scan selhal:\n{ex.Message}", "Deep Clean", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { Mouse.OverrideCursor = null; }
    }

    private static string ScanDeepClean()
    {
        var candidates = new (string Name, string Path)[]
        {
            ("User TEMP", Environment.GetEnvironmentVariable("TEMP") ?? ""),
            ("Windows TEMP", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")),
            ("Chrome Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\User Data")),
            ("Edge Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Edge\\User Data")),
            ("Firefox Profiles", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla\\Firefox\\Profiles")),
            ("D3D Shader Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache")),
            ("NVIDIA DX Cache", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NVIDIA\\DXCache")),
            ("Crash Dumps", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps")),
            ("Windows Update Download", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution\\Download")),
            ("Delivery Optimization", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SoftwareDistribution\\DeliveryOptimization"))
        };
        long total = 0; int found = 0; var lines = new System.Text.StringBuilder();
        lines.AppendLine("DEEP CLEAN — SCAN ONLY"); lines.AppendLine("No files were deleted.\n");
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Path) || !Directory.Exists(candidate.Path)) continue;
            long size = GetDirectorySize(candidate.Path, 0, 50000);
            if (size > 0) { found++; total += size; lines.AppendLine($"• {candidate.Name}: {FormatBytes(size)}"); }
        }
        lines.AppendLine($"\nCandidates: {found}"); lines.AppendLine($"Potential data: {FormatBytes(total)}");
        lines.AppendLine("\nNext step: show exact items and let the user select what to remove.");
        return lines.ToString();
    }

    private static long GetDirectorySize(string path, int depth, int maxFiles)
    {
        if (depth > 5) return 0;
        long total = 0; int count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (++count > maxFiles) break;
                try { total += new FileInfo(file).Length; } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                if (++count > maxFiles) break;
                total += GetDirectorySize(dir, depth + 1, maxFiles - count);
            }
        }
        catch { }
        return total;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.0} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        return $"{bytes / 1024d / 1024d / 1024d:0.00} GB";
    }

    private void PlaceholderAction_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Tato funkce má připravené UI. Skutečné systémové změny budou přidávány přes bezpečný modul se zálohou, ověřením a možností Restore.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void QuickOptimize_Click(object sender, RoutedEventArgs e)
    {
        var choice = MessageBox.Show("Spustit bezpečný scan před Quick Optimize? Zatím se nic automaticky nemaže ani nevypíná.", "Quick Optimize", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (choice == MessageBoxResult.Yes) DeepScan_Click(sender, e);
    }

    private void QuickCleanup_Click(object sender, RoutedEventArgs e) => ShowPage("Cleanup");
}
