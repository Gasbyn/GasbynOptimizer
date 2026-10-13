using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using GasbynOptimizer.Services;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private HardwareInventory? _hardwareInventory;
    private static readonly HttpClient UpdateHttpClient = CreateUpdateHttpClient();
    private string? _latestReleaseUrl;

    private static HttpClient CreateUpdateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GasbynOptimizer-UpdateChecker/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private async Task RunStartupCheckAsync()
    {
        StartupScanOverlay.Visibility = Visibility.Visible;
        UpdateAvailablePanel.Visibility = Visibility.Collapsed;
        _latestReleaseUrl = null;
        StartupScanAdminButton.Visibility = Visibility.Collapsed;
        StartupScanContinue.IsEnabled = false;
        if (!IsRunningAsAdministrator())
        {
            StartupScanSpinner.Visibility = Visibility.Collapsed;
            StartupScanProgress.Visibility = Visibility.Collapsed;
            StartupScanStatus.Text = "SPUŠTĚNÍ JAKO SPRÁVCE JE POVINNÉ";
            StartupScanDetails.Text = "Gasbyn Optimizer potřebuje oprávnění správce pro systémovou diagnostiku a změny. Aplikace zatím neumožní pokračovat. Klikni na „SPUSTIT JAKO SPRÁVCE“ a potvrď dialog Windows.";
            CompleteStartupCheck(false);
            StartupScanAdminButton.Visibility = Visibility.Visible;
            StartupScanContinue.IsEnabled = false;
            return;
        }
        StartupScanContinue.Visibility = Visibility.Visible;
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

            StartupScanStatus.Text = "4/4 · Kontroluji veřejné vydání Gasbyn Optimizeru…";
            StartupScanDetails.Text = notes.ToString();
            try
            {
                var release = await CheckForLatestReleaseAsync();
                if (release is not null)
                {
                    _latestReleaseUrl = release.Value.Url;
                    UpdateAvailableText.Text = $"Verze {release.Value.Tag} je dostupná. Aktuální verze: {GetCurrentAppVersion().ToString(3)}.";
                    UpdateAvailablePanel.Visibility = Visibility.Visible;
                    notes.AppendLine();
                    notes.AppendLine("AKTUALIZACE");
                    notes.AppendLine($"Dostupná verze: {release.Value.Tag}");
                    notes.AppendLine("Před stažením ověř, že odkaz vede na oficiální vydání Gasbyn Optimizeru.");
                }
                else
                {
                    notes.AppendLine();
                    notes.AppendLine("AKTUALIZACE");
                    notes.AppendLine("Nebyla nalezena novější veřejná stabilní verze (nebo nejsou veřejná vydání dostupná).");
                }
            }
            catch (Exception updateException)
            {
                // Update checks must never prevent the optimizer from opening when offline or GitHub is unavailable.
                notes.AppendLine();
                notes.AppendLine("Kontrola aktualizací není dostupná: " + updateException.Message);
            }

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

    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void RelaunchAsAdministrator_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("Nepodařilo se zjistit cestu ke spuštěné aplikaci.");

            Process.Start(new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            Application.Current.Shutdown();
        }
        catch (Win32Exception)
        {
            StartupScanStatus.Text = "Zvýšení oprávnění bylo zrušeno.";
            StartupScanDetails.Text = "Pro použití Gasbyn Optimizeru spusť aplikaci jako správce a potvrď UAC dialog Windows.";
        }
        catch (Exception ex)
        {
            StartupScanStatus.Text = "Aplikaci se nepodařilo spustit jako správce.";
            StartupScanDetails.Text = ex.Message;
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

    private static Version GetCurrentAppVersion() =>
        typeof(MainWindow).Assembly.GetName().Version ?? new Version(2, 0, 3);

    private static bool TryParseReleaseVersion(string tag, out Version version)
    {
        var normalizedTag = tag.Trim();
        if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalizedTag = normalizedTag[1..];

        // Gasbyn's release sequence uses tags such as v2.0.B2, v2.0.B3.
        var betaMatch = Regex.Match(normalizedTag, @"^(?<major>\d+)\.(?<minor>\d+)\.B(?<build>\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (betaMatch.Success)
        {
            version = new Version(
                int.Parse(betaMatch.Groups["major"].Value),
                int.Parse(betaMatch.Groups["minor"].Value),
                int.Parse(betaMatch.Groups["build"].Value));
            return true;
        }

        var versionPart = normalizedTag.Split('-', '+')[0];
        return Version.TryParse(versionPart, out version!);
    }

    private static async Task<(string Tag, string Url)?> CheckForLatestReleaseAsync()
    {
        // GitHub's latest-release endpoint only exposes published releases. Drafts and prereleases are ignored.
        // A private repository returns 404 to unauthenticated clients, which is treated as no public update.
        using var response = await UpdateHttpClient.GetAsync(
            "https://api.github.com/repos/Gasbyn/GasbynOptimizer/releases/latest");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;
        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            return null;
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True)
            return null;

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        var url = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsedUrl) ||
            parsedUrl.Scheme != Uri.UriSchemeHttps ||
            !parsedUrl.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!TryParseReleaseVersion(tag, out var latestVersion))
            return null;

        return latestVersion > GetCurrentAppVersion()
            ? (tag.Trim(), parsedUrl.AbsoluteUri)
            : null;
    }

    private void OpenLatestRelease_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_latestReleaseUrl))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(_latestReleaseUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StartupScanDetails.Text += Environment.NewLine + "Odkaz na aktualizaci se nepodařilo otevřít: " + ex.Message;
        }
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
    }
}
