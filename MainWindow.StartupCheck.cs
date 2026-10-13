using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using GasbynOptimizer.Services;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private HardwareInventory? _hardwareInventory;

    private async Task RunStartupCheckAsync()
    {
        AppSurface.Effect = new BlurEffect { Radius = 12, KernelType = KernelType.Gaussian };
        StartupScanOverlay.Visibility = Visibility.Visible;
        StartupScanContinue.IsEnabled = false;
        StartupScanSpinner.Visibility = Visibility.Visible;
        StartupScanCompleteIcon.Visibility = Visibility.Collapsed;
        StartupScanProgress.Visibility = Visibility.Visible;
        StartupScanStatus.Text = "1/3 · Skenuji procesor, grafiku, RAM, desku a disky…";
        StartupScanDetails.Text = "Načítám hardwarový profil. Kontrola je pouze čtecí.";

        var notes = new StringBuilder();
        try
        {
            _hardwareInventory = await Task.Run(HardwareInventoryService.Scan);
            CpuValue.Text = _hardwareInventory.Cpu;
            GpuValue.Text = _hardwareInventory.Gpu;
            RamValue.Text = _hardwareInventory.Memory;

            notes.AppendLine("HARDWARE");
            notes.AppendLine("CPU: " + _hardwareInventory.Cpu);
            notes.AppendLine("GPU: " + _hardwareInventory.Gpu);
            notes.AppendLine("RAM: " + _hardwareInventory.Memory);
            notes.AppendLine("Základní deska: " + _hardwareInventory.Motherboard);
            notes.AppendLine("Disky: " + _hardwareInventory.Disks);
            notes.AppendLine("Windows: " + _hardwareInventory.Windows);

            StartupScanStatus.Text = "2/3 · Kontroluji nastavení Windows a zálohy…";
            StartupScanDetails.Text = notes.ToString();
            await Task.Yield();

            var settings = await Task.Run(ReadStartupSettings);
            notes.AppendLine();
            notes.AppendLine("SYSTÉMOVÁ NASTAVENÍ");
            notes.AppendLine(settings);

            StartupScanStatus.Text = "3/3 · Připravuji doporučení podle detekovaného hardwaru…";
            StartupScanDetails.Text = notes.ToString();
            await Task.Yield();

            notes.AppendLine();
            notes.AppendLine("DOPORUČENÍ");
            notes.AppendLine(HardwareInventoryServiceForSettings(_hardwareInventory));
            notes.AppendLine("Před změnou se uloží původní stav. Přínos FPS není garantovaný.");
            StartupScanDetails.Text = notes.ToString();
            StartupScanStatus.Text = "✓ Kontrola dokončena · žádné systémové změny nebyly provedeny.";
            CompleteStartupCheck(true);
            StartupScanContinue.IsEnabled = true;
        }
        catch (Exception ex)
        {
            notes.AppendLine();
            notes.AppendLine("Kontrola skončila s upozorněním: " + ex.Message);
            StartupScanDetails.Text = notes.ToString();
            StartupScanStatus.Text = "Kontrola dokončena s upozorněním.";
            CompleteStartupCheck(false);
            StartupScanContinue.IsEnabled = true;
        }
    }

    private void CompleteStartupCheck(bool success)
    {
        StartupScanSpinner.Visibility = Visibility.Collapsed;
        StartupScanProgress.Visibility = Visibility.Collapsed;
        StartupScanCompleteIcon.Visibility = Visibility.Visible;
        StartupScanCompleteIcon.Text = success ? "✓" : "!";
        StartupScanCompleteIcon.Foreground = success
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(72, 213, 151))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 154, 82));
    }

    private static string HardwareInventoryServiceForSettings(HardwareInventory inventory) =>
        inventory.BuildRecommendations(OptimizerPreferencesStore.Load().AdvancedRecommendations);

    private static string ReadStartupSettings()
    {
        var lines = new List<string>();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            var value = key?.GetValue("AutoGameModeEnabled");
            lines.Add("Windows Game Mode: " + (value is null ? "výchozí / nenastaveno" : Convert.ToInt32(value) == 1 ? "zapnuto" : "vypnuto"));
        }
        catch (Exception ex) { lines.Add("Windows Game Mode: nedostupné (" + ex.Message + ")"); }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            var value = key?.GetValue("VisualFXSetting");
            lines.Add("Vizuální efekty: " + (value is null ? "výchozí Windows" : Convert.ToInt32(value) == 2 ? "nastaven režim nejlepšího výkonu" : "režim " + value));
        }
        catch (Exception ex) { lines.Add("Vizuální efekty: nedostupné (" + ex.Message + ")"); }

        try
        {
            using var process = Process.Start(new ProcessStartInfo("powercfg.exe", "/getactivescheme")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            });
            if (process is null) lines.Add("Plán napájení: příkaz se nepodařilo spustit");
            else
            {
                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);
                var match = Regex.Match(stdout, @"([0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12})");
                lines.Add("Aktivní plán napájení: " + (match.Success ? match.Value : (stdout + " " + stderr).Trim()));
            }
        }
        catch (Exception ex) { lines.Add("Plán napájení: nedostupný (" + ex.Message + ")"); }

        var statePath = Path.Combine(OptimizerPreferencesStore.DataDirectory, "optimization-state.json");
        lines.Add("Záloha optimalizací: " + (File.Exists(statePath) ? "soubor se stavem nalezen" : "zatím žádné zálohy"));
        var preferences = OptimizerPreferencesStore.Load();
        lines.Add("Detailní logování optimalizací: " + (preferences.LoggingEnabled ? "zapnuto" : "vypnuto"));
        return string.Join(Environment.NewLine, lines);
    }

    private void StartupScanContinue_Click(object sender, RoutedEventArgs e)
    {
        StartupScanOverlay.Visibility = Visibility.Collapsed;
        AppSurface.Effect = null;
    }
}
