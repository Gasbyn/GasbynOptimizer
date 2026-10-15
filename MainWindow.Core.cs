using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using GasbynOptimizer.Core;
using GasbynOptimizer.Models;
using GasbynOptimizer.Tweaks.Gaming;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private readonly TweakEngine _tweakEngine = new();
    private readonly Stack<string> _navHistory = new();
    private string? _currentPage;
    private bool _navigatingBack;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Gasbyn Optimizer " + GetDisplayVersion();
        _tweakEngine.Register(GameMode.Create());
        Loaded += async (_, _) =>
        {
            ApplyDisplayVersionToUi();
            ShowPage("Dashboard");
            await RunStartupCheckAsync();
            CheckWorkspaceDriftOnStartup();
        };
    }

    private void NavigateBack()
    {
        if (_navHistory.Count == 0)
        {
            ShowPage("Dashboard");
            return;
        }
        _navigatingBack = true;
        var prev = _navHistory.Pop();
        ShowPage(prev);
        _navigatingBack = false;
    }

    private void ApplyDisplayVersionToUi()
    {
        var display = GetDisplayVersion();
        Title = "Gasbyn Optimizer " + display;
        UpdateVersionTextBlocks(this, display);
    }

    private static void UpdateVersionTextBlocks(DependencyObject root, string display)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                if (Regex.IsMatch(tb.Text, @"v?\d+\.\d+\.B\d+", RegexOptions.IgnoreCase))
                    tb.Text = Regex.Replace(tb.Text, @"v?\d+\.\d+\.B\d+", display, RegexOptions.IgnoreCase);
                else if (tb.Text.Contains("Gasbyn Optimizer", StringComparison.OrdinalIgnoreCase) &&
                         tb.Text.Contains("v2.", StringComparison.Ordinal))
                    tb.Text = Regex.Replace(tb.Text, @"v?\d+\.\d+\.B\d+", display, RegexOptions.IgnoreCase);
            }
            UpdateVersionTextBlocks(child, display);
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
            ShowPage(page);
    }

    private void UpdateNavigationSelection(string page)
    {
        foreach (var button in NavPanel.Children.OfType<Button>())
        {
            var selected = string.Equals(button.Tag as string, page, StringComparison.OrdinalIgnoreCase);
            button.Background = selected
                ? (Brush)FindResource("OrangeSoft")
                : Brushes.Transparent;
            button.Foreground = selected
                ? (Brush)FindResource("OrangeLight")
                : new SolidColorBrush(Color.FromRgb(144, 149, 159));
            button.BorderBrush = selected
                ? new SolidColorBrush(Color.FromRgb(74, 40, 20))
                : Brushes.Transparent;
            button.BorderThickness = selected ? new Thickness(1) : new Thickness(0);
            button.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;

            button.ApplyTemplate();
            if (button.Template.FindName("NavActiveIndicator", button) is FrameworkElement indicator)
                indicator.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ShowPage(string page)
    {
        if (!_navigatingBack && !string.IsNullOrEmpty(_currentPage) &&
            !string.Equals(_currentPage, page, StringComparison.OrdinalIgnoreCase))
            _navHistory.Push(_currentPage);
        _currentPage = page;

        UpdateNavigationSelection(page);
        PageTitle.Text = page;
        PageSubtitle.Text = page switch
        {
            "Dashboard" => "Přehled systému a rychlé akce",
            "Tweaks" => "Reverzibilní nastavení s kontrolou stavu",
            "Cleanup" => "Vyčisti dočasné soubory bez zásahů do systémových služeb",
            "Deep Clean" => "Kontrola místa a bezpečné úklidové nástroje",
            "Startup" => "Kontrola aplikací spouštěných se systémem",
            "Apps Manager" => "Přehled nainstalovaných aplikací",
            "Gaming" => "Herní nastavení a diagnostika",
            "Network" => "Diagnostika připojení bez náhodných síťových tweaků",
            "Health Check" => "Základní kontrola stavu Windows",
            "Benchmark" => "Základní CPU test a FPS nástroje",
            "Backup" => "Zálohy a historie změn",
            "Restore Point" => "Systémové body obnovení a napájení",
            "PC Info" => "Detekce hardwaru a systémových informací",
            "Settings" => "Nastavení aplikace",
            _ => "Gasbyn Optimizer"
        };

        var dashboard = string.Equals(page, "Dashboard", StringComparison.OrdinalIgnoreCase);
        DashboardView.Visibility = dashboard ? Visibility.Visible : Visibility.Collapsed;
        SecondaryView.Visibility = dashboard ? Visibility.Collapsed : Visibility.Visible;
        SecondaryContent.Children.Clear();
        if (dashboard) { AnimateContent(); return; }

        AddPageHeader(page);
        switch (page)
        {
            case "Tweaks":
                BuildTweaksWorkspace();
                break;
            case "Cleanup":
                AddInfoCard("User TEMP", "Zkontroluje a nabídne odstranění dočasných souborů aktuálního uživatele.", "User TEMP");
                AddInfoCard("Windows + User TEMP", "Zkontroluje dostupné TEMP složky a nabídne bezpečný úklid.", "Windows + User TEMP");
                AddInfoCard("Shader Cache", "Zobrazí velikost dostupných shader cache; po odstranění se mohou znovu vytvořit.", "Shader Cache");
                AddInfoCard("Browser Cache", "Zkontroluje běžné cache prohlížečů.", "Browser Cache");
                AddInfoCard("Recycle Bin", "Zobrazí informace o Koši; vysypání vyžaduje další potvrzení.", "Recycle Bin");
                break;
            case "Deep Clean":
                AddInfoCard("Deep Scan Summary", "Provede kontrolu dostupných míst na disku a zobrazí souhrn.", "Deep Scan Summary");
                AddInfoCard("Large Files", "Vyhledá velké soubory pro ruční kontrolu; nic nemaže automaticky.", "Large Files");
                AddInfoCard("Crash Dumps & Logs", "Zkontroluje soubory výpisů a logů.", "Crash Dumps & Logs");
                break;
            case "Startup":
                AddInfoCard("Startup Manager", "Prohlédni si aplikace, které se spouštějí po přihlášení.", "View startup");
                AddInfoCard("Scheduled Startup Tasks", "Zobrazí naplánované úlohy související se spouštěním.", "Scheduled Startup Tasks");
                AddInfoCard("Startup Registry Entries", "Zobrazí běžné Run položky registru.", "Startup Registry Entries");
                break;
            case "Apps Manager":
                SecondaryContent.Children.Add(new Apps.AppsManagerWindow());
                break;
            case "Gaming":
                BuildTweaksWorkspace(gamingOnly: true);
                AddInfoCard("GAMING AUDIT", "Zkontroluje Game Mode, HAGS, plán napájení a Fortnite. Audit nic nemění.", "GAMING OPTIMIZER");
                break;
            case "Network":
                AddInfoCard("Connection Info", "Zobrazí aktivní síťové adaptéry, rychlost linky a IPv4 adresy.", "Connection Info");
                AddInfoCard("DNS Check", "Zobrazí aktuální DNS servery.", "DNS Check");
                AddInfoCard("TCP/IP Health", "Otestuje základní dostupnost a DNS rozlišení.", "TCP/IP Health");
                break;
            case "Health Check":
                AddInfoCard("Run health check", "Spustí základní kontrolu Windows a zobrazí případná upozornění.", "Run health check");
                break;
            case "Benchmark":
                BuildBenchmarkWorkspace();
                break;
            case "Backup":
                BuildBackupWorkspace();
                break;
            case "Restore Point":
                EnsureTweakDeckHost();
                AddWorkspaceTweakCard(
                    "High Performance power plan",
                    "Aktivuje vestavěný High Performance plán. OFF vrátí plán uložený při zapnutí.",
                    "power-plan",
                    "ON: powercfg High Performance. OFF: původní scheme.", "VÝKON", "NAPÁJENÍ");
                break;
            case "PC Info":
                LoadSystemInfo();
                AddInfoCard("Hardware Detection", "Obnoví informace o procesoru, grafice a paměti.", "Hardware Detection");
                AddInfoCard("Disk Space", "Zobrazí obsazení systémového disku.", "Disk Space");
                break;
            case "Settings":
                AddSettingsCard();
                AddAdvancedModeCard();
                AddInfoCard("Safe-by-default", "ON = aplikuje změnu (registry/cmd). OFF = vrátí výchozí/původní hodnoty ze zálohy.", "About");
                break;
            default:
                AddInfoCard("Open Tweaks", "Vratné systémové optimalizace a jejich stav.", "Open Tweaks");
                break;
        }
        AnimateContent();
    }

    private void AnimateContent()
    {
        ContentHost.Opacity = 0;
        ContentHost.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

        if (ContentHost.RenderTransform is TranslateTransform transform)
        {
            transform.Y = 9;
            transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }

    private void LoadSystemInfo()
    {
        if (_hardwareInventory is not null)
        {
            CpuValue.Text = _hardwareInventory.Cpu;
            GpuValue.Text = _hardwareInventory.Gpu;
            RamValue.Text = _hardwareInventory.Memory;
            return;
        }

        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            CpuValue.Text = cpu.Get().Cast<ManagementObject>().Select(x => x["Name"]?.ToString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Unknown CPU";
        }
        catch { CpuValue.Text = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "CPU unavailable"; }

        try
        {
            using var gpu = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            GpuValue.Text = gpu.Get().Cast<ManagementObject>().Select(x => x["Name"]?.ToString()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Unknown GPU";
        }
        catch { GpuValue.Text = "GPU unavailable"; }

        try
        {
            using var memory = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            var bytes = Convert.ToDouble(memory.Get().Cast<ManagementObject>().First()["TotalPhysicalMemory"]);
            RamValue.Text = $"{bytes / 1024d / 1024d / 1024d:0} GB";
        }
        catch { RamValue.Text = "RAM unavailable"; }
    }
}
