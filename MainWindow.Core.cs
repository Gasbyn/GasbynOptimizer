using System;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using GasbynOptimizer.Core;
using GasbynOptimizer.Models;
using GasbynOptimizer.Tweaks.Gaming;
using GasbynOptimizer.Optimization;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private readonly TweakEngine _tweakEngine = new();

    public MainWindow()
    {
        InitializeComponent();
        _tweakEngine.Register(GameMode.Create());
        Loaded += (_, _) =>
        {
            LoadSystemInfo();
            ShowPage("Dashboard");
            var scan = new StartupScanWindow { Owner = this };
            scan.ShowDialog();
        };
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string page })
            ShowPage(page);
    }

    private void ShowPage(string page)
    {
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
            "PC Info" => "Detekce hardwaru a systémových informací",
            "Settings" => "Nastavení aplikace",
            _ => "Gasbyn Optimizer"
        };

        var dashboard = string.Equals(page, "Dashboard", StringComparison.OrdinalIgnoreCase);
        DashboardView.Visibility = dashboard ? Visibility.Visible : Visibility.Collapsed;
        SecondaryView.Visibility = dashboard ? Visibility.Collapsed : Visibility.Visible;
        SecondaryContent.Children.Clear();
        if (dashboard) return;

        AddPageHeader(page);
        switch (page)
        {
            case "Tweaks":
                AddTweakCard("Windows Game Mode", "Zapnutí herního režimu Windows. Původní stav se ukládá před změnou.", "FREE", "Manage Game Mode");
                AddInfoCard("GAMING OPTIMIZER", "Zobrazí audit Game Mode, HAGS, power planu a Fortnite konfigurace bez automatické změny.", "GAMING OPTIMIZER");
                AddInfoCard("High Performance Power Plan", "Zkontroluj a případně přepni plán napájení. Použij Full Optimizer pro reverzibilní přepínač.", "Power Plan");
                AddInfoCard("Hardware Accelerated GPU Scheduling", "Zkontroluj aktuální stav HAGS.", "GPU Scheduling");
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
                AddInfoCard("Installed Applications", "Zobrazí nainstalované aplikace a dostupné akce správce aplikací.", "Installed Applications");
                break;
            case "Gaming":
                AddInfoCard("GAMING OPTIMIZER", "Zkontroluje nastavení herního režimu, HAGS, power planu a Fortnite.", "GAMING OPTIMIZER");
                AddInfoCard("Windows Gaming Services", "Zobrazí stav souvisejících herních služeb; nic nevypíná.", "Windows Gaming Services");
                AddInfoCard("Fullscreen Optimization Check", "Zobrazí aktuální stav relevantního nastavení.", "Fullscreen Optimization Check");
                AddInfoCard("Fortnite Optimization Profile", "Zkontroluje, zda existuje konfigurace Fortnite.", "Fortnite Optimization Profile");
                AddInfoCard("NVIDIA Gaming Profile", "Zkontroluje dostupné lokální NVIDIA profilové soubory.", "NVIDIA Gaming Profile");
                AddInfoCard("Full Optimizer", "Reverzibilní přepínače, bod obnovení a FPS testy.", "Open Full Optimizer");
                break;
            case "Network":
                AddInfoCard("Connection Info", "Zobrazí aktivní síťové adaptéry, rychlost linky a IPv4 adresy.", "Connection Info");
                AddInfoCard("DNS Check", "Zobrazí aktuální DNS servery.", "DNS Check");
                AddInfoCard("TCP/IP Health", "Otestuje základní dostupnost a DNS rozlišení.", "TCP/IP Health");
                AddInfoCard("Network Reset Tools", "Reset sítě může odpojit internet; zobrazí se pouze vysvětlení rizik.", "Network Reset Tools");
                break;
            case "Health Check":
                AddInfoCard("Run health check", "Spustí základní kontrolu Windows a zobrazí případná upozornění.", "Run health check");
                break;
            case "Benchmark":
                AddInfoCard("CPU Quick Test", "Krátký výpočetní test CPU pro srovnání ve stejných podmínkách.", "CPU Quick Test");
                AddInfoCard("Before / After", "Zobrazí tipy pro porovnání před a po změně.", "Before / After");
                AddInfoCard("FPS Tester", "PresentMon a historie posledních 15 testů jsou ve Full Optimizeru.", "Open Full Optimizer");
                break;
            case "Backup":
                AddInfoCard("Tweak Backups", "Otevře seznam dostupných lokálních záloh.", "Tweak Backups");
                AddInfoCard("Restore All", "Zobrazí rozsah a bezpečný postup obnovení.", "Restore All");
                AddInfoCard("Change History", "Zobrazí historii zaznamenaných změn.", "Change History");
                AddInfoCard("System Restore Point", "Vytvoř bod obnovení ve Full Optimizeru před změnou nastavení.", "Open Full Optimizer");
                break;
            case "PC Info":
                LoadSystemInfo();
                AddInfoCard("Hardware Detection", "Obnoví informace o procesoru, grafice a paměti.", "Hardware Detection");
                AddInfoCard("Disk Space", "Zobrazí obsazení systémového disku.", "Disk Space");
                AddInfoCard("Refresh", "Znovu načte systémové informace.", "Refresh");
                break;
            case "Settings":
                AddInfoCard("Safe-by-default", "Optimizer nepřepíná automaticky služby, Defender, bootovací časovače ani IPv6. Každý tweak musí mít jasný přínos, kontrolu stavu a návratovou cestu.", "About");
                AddInfoCard("Open Full Optimizer", "Otevře přepínače, bod obnovení a FPS historii.", "Open Full Optimizer");
                break;
            default:
                AddInfoCard("Open Full Optimizer", "Reverzibilní optimalizace a FPS testy.", "Open Full Optimizer");
                break;
        }
    }

    private void LoadSystemInfo()
    {
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
