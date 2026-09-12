using System;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GasbynOptimizer;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
                if (!string.IsNullOrWhiteSpace(name))
                    return ShortenHardwareName(name);
            }
        }
        catch
        {
            // UI should still work if WMI is unavailable.
        }

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
                if (!string.IsNullOrWhiteSpace(name) &&
                    !name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                {
                    return ShortenHardwareName(name);
                }
            }
        }
        catch
        {
            // UI should still work if WMI is unavailable.
        }

        return "Unknown GPU";
    }

    private static int GetRamGb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                if (ulong.TryParse(item["TotalPhysicalMemory"]?.ToString(), out var bytes))
                    return Math.Max(1, (int)Math.Round(bytes / 1024d / 1024d / 1024d));
            }
        }
        catch
        {
            // UI should still work if WMI is unavailable.
        }

        return 0;
    }

    private static string ShortenHardwareName(string value)
    {
        if (value.Length <= 30)
            return value;

        return value.Replace("(R)", "", StringComparison.Ordinal)
                    .Replace("(TM)", "", StringComparison.Ordinal)
                    .Replace("CPU", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is null)
            return;

        ShowPage(button.Tag.ToString() ?? "Dashboard");
    }

    private void ShowPage(string page)
    {
        PageTitle.Text = page;
        PageSubtitle.Text = page switch
        {
            "Dashboard" => "Přehled systému a rychlé akce",
            "Tweaks" => "Bezpečné a vratné optimalizace Windows",
            "Cleanup" => "Najdi zbytečné soubory před jejich odstraněním",
            "Startup" => "Přehled aplikací spouštěných se systémem",
            "PC Info" => "Hardware a základní informace o počítači",
            "Settings" => "Nastavení aplikace",
            _ => "Gasbyn Optimizer"
        };

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
                AddTweakCard("Windows Game Mode", "Základní herní režim Windows.", "FREE", "Připraveno pro TweakEngine");
                AddTweakCard("Hardware Accelerated GPU Scheduling", "Stav plánování GPU ve Windows.", "FREE", "Detekce nastavení");
                AddTweakCard("High Performance Power Plan", "Přepnutí napájecího plánu bez mazání existujících plánů.", "PREMIUM", "Připraveno pro TweakEngine");
                AddTweakCard("Gaming Network Profile", "Budoucí bezpečné síťové nastavení pro hraní.", "PREMIUM", "Připraveno pro TweakEngine");
                break;

            case "Cleanup":
                AddInfoCard("SAFE CLEANUP", "Nejdříve pouze naskenujeme systém. Nic se automaticky nemaže.", "Scan system");
                AddTweakCard("User TEMP", "Dočasné soubory aktuálního uživatele.", "FREE", "Scan only");
                AddTweakCard("Windows Update Cache", "Stažené aktualizační soubory, které lze bezpečně znovu vytvořit.", "FREE", "Scan only");
                AddTweakCard("Delivery Optimization Cache", "Cache doručování aktualizací Windows.", "PREMIUM", "Scan only");
                break;

            case "Startup":
                AddInfoCard("STARTUP MANAGER", "První verze bude pouze zobrazovat startup položky. Zakázání přidáme až s backupem a potvrzením.", "View startup");
                AddTweakCard("Startup Registry Entries", "Přehled běžných Windows Run/RunOnce položek.", "FREE", "Read only");
                AddTweakCard("Startup Folder", "Přehled zástupců spouštěných při přihlášení.", "FREE", "Read only");
                break;

            case "PC Info":
                AddInfoCard("CPU", CpuValue.Text, "Refresh");
                AddInfoCard("GPU", GpuValue.Text, "Refresh");
                AddInfoCard("RAM", RamValue.Text, "Refresh");
                AddInfoCard("Windows", Environment.OSVersion.VersionString, "Detected");
                break;

            case "Settings":
                AddInfoCard("GENERAL", "Automatické kontroly, vzhled a chování aplikace.", "Coming soon");
                AddInfoCard("BACKUPS", "Budoucí správa záloh před aplikací systémových změn.", "Coming soon");
                AddInfoCard("LICENSE", "Free / Premium licence budou později ověřovány serverem.", "Coming soon");
                break;

            default:
                AddInfoCard("PAGE", "Tato stránka ještě není připravená.", "OK");
                break;
        }
    }

    private void AddPageHeader(string page)
    {
        var header = new TextBlock
        {
            Text = page switch
            {
                "Tweaks" => "OPTIMIZATION MODULES",
                "Cleanup" => "CLEANUP CENTER",
                "Startup" => "STARTUP MANAGER",
                "PC Info" => "SYSTEM INFORMATION",
                "Settings" => "APPLICATION SETTINGS",
                _ => "GASBYN OPTIMIZER"
            },
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Orange"),
            Margin = new Thickness(0, 0, 0, 14)
        };

        SecondaryContent.Children.Add(header);
    }

    private void AddTweakCard(string title, string description, string tier, string actionText)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 5, 0, 0) });

        var tierText = new TextBlock
        {
            Text = tier,
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = tier == "PREMIUM" ? (Brush)FindResource("Orange") : (Brush)FindResource("TextMuted"),
            Margin = new Thickness(0, 10, 0, 0)
        };
        stack.Children.Add(tierText);

        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var button = new Button
        {
            Content = actionText,
            Style = (Style)FindResource("ActionButton"),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = title
        };
        button.Click += PlaceholderAction_Click;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        border.Child = grid;
        SecondaryContent.Children.Add(border);
    }

    private void AddInfoCard(string title, string description, string actionText)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Orange") });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 13, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 6, 20, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var button = new Button { Content = actionText, Style = (Style)FindResource("NavButton"), VerticalAlignment = VerticalAlignment.Center };
        button.Click += PlaceholderAction_Click;
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        border.Child = grid;
        SecondaryContent.Children.Add(border);
    }

    private void PlaceholderAction_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Tahle funkce zatím pouze připravuje UI. Skutečné systémové změny přidáme až do bezpečného TweakEngine se zálohou a ověřením.",
            "Gasbyn Optimizer",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void QuickOptimize_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Quick Optimize zatím nic nemění. Nejprve vytvoříme bezpečný scan + backup systém a potom přidáme jednotlivé tweaky.",
            "Quick Optimize",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void QuickCleanup_Click(object sender, RoutedEventArgs e)
    {
        ShowPage("Cleanup");
    }
}
