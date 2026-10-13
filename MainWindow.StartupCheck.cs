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
    private (string Tag, string Url, string? DownloadUrl, string? Sha256)? _latestRelease;

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
        _latestRelease = null;
        UpdateLatestReleaseButton.Content = "ZOBRAZIT";
        UpdateLatestReleaseButton.IsEnabled = true;
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
                    _latestRelease = release;
                    var hasVerifiedPackage =
                        !string.IsNullOrWhiteSpace(release.Value.DownloadUrl) &&
                        !string.IsNullOrWhiteSpace(release.Value.Sha256);
                    UpdateLatestReleaseButton.Content = hasVerifiedPackage ? "AKTUALIZOVAT" : "ZOBRAZIT";
                    UpdateAvailableText.Text = $"Verze {release.Value.Tag} je dostupná. Aktuální verze: {GetCurrentAppVersion().ToString(3)}.";
                    UpdateAvailablePanel.Visibility = Visibility.Visible;
                    notes.AppendLine();
                    notes.AppendLine("AKTUALIZACE");
                    notes.AppendLine($"Dostupná verze: {release.Value.Tag}");
                    notes.AppendLine(hasVerifiedPackage
                        ? "Vydání obsahuje ověřený instalační balíček; lze aktualizovat přímo v optimizeru."
                        : "Ověřený automatický balíček není dostupný; otevře se stránka vydání.");
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

    private static async Task<(string Tag, string Url, string? DownloadUrl, string? Sha256)?> CheckForLatestReleaseAsync()
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

        if (latestVersion <= GetCurrentAppVersion())
            return null;

        string? downloadUrl = null;
        string? sha256 = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var assetName = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                if (!string.Equals(assetName, "GasbynOptimizer-win-x64.zip", StringComparison.Ordinal))
                    continue;

                downloadUrl = asset.TryGetProperty("browser_download_url", out var downloadElement)
                    ? downloadElement.GetString()
                    : null;
                sha256 = asset.TryGetProperty("digest", out var digestElement)
                    ? digestElement.GetString()
                    : null;
                break;
            }
        }

        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var packageUri) ||
            packageUri.Scheme != Uri.UriSchemeHttps ||
            !packageUri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !packageUri.AbsolutePath.StartsWith(
                "/Gasbyn/GasbynOptimizer/releases/download/",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(sha256) ||
            !Regex.IsMatch(
                sha256.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? sha256["sha256:".Length..] : sha256,
                "^[0-9a-fA-F]{64}$",
                RegexOptions.CultureInvariant))
        {
            downloadUrl = null;
            sha256 = null;
        }

        return (tag.Trim(), parsedUrl.AbsoluteUri, downloadUrl, sha256);
    }

    private async void UpdateLatestRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_latestRelease is not { } release)
            return;

        // Without a trusted ZIP asset and its GitHub SHA-256 digest, fall back to the release page.
        if (string.IsNullOrWhiteSpace(release.DownloadUrl) || string.IsNullOrWhiteSpace(release.Sha256))
        {
            try
            {
                Process.Start(new ProcessStartInfo(release.Url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StartupScanDetails.Text += Environment.NewLine + "Odkaz na aktualizaci se nepodařilo otevřít: " + ex.Message;
            }
            return;
        }

        var confirm = MessageBox.Show(
            $"Stáhnout a nainstalovat verzi {release.Tag}? Gasbyn Optimizer se zavře a po aktualizaci se znovu spustí. Nastavení uložená v uživatelském profilu zůstanou zachována.",
            "Aktualizace Gasbyn Optimizeru",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        UpdateLatestReleaseButton.IsEnabled = false;
        UpdateLatestReleaseButton.Content = "STAHUJI…";
        try
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("Nepodařilo se zjistit cestu ke spuštěné aplikaci.");

            await AppUpdater.PrepareAndLaunchAsync(
                release.DownloadUrl,
                release.Sha256,
                executablePath,
                Environment.ProcessId,
                status => UpdateAvailableText.Text = $"Verze {release.Tag}: {status}");

            StartupScanStatus.Text = "Aktualizace je připravena. Optimizer se nyní restartuje…";
            Application.Current.Shutdown();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            UpdateLatestReleaseButton.IsEnabled = true;
            UpdateLatestReleaseButton.Content = "ZKUSIT ZNOVU";
            UpdateAvailableText.Text = $"ZIP verze {release.Tag} teď vrací 404.";
            StartupScanDetails.Text += Environment.NewLine + Environment.NewLine +
                "AKTUALIZACE SE NEPOVEDLA" + Environment.NewLine +
                "GitHub vrátil 404 i po opakovaných pokusech. Otevírám stránku vydání, kde lze zkontrolovat dostupnost ZIPu.";

            try
            {
                Process.Start(new ProcessStartInfo(release.Url) { UseShellExecute = true });
            }
            catch
            {
                // The update failure is already reported; opening the browser is best-effort.
            }

            MessageBox.Show(
                $"GitHub vrátil 404 při stahování ZIPu verze {release.Tag}, a to i po opakovaných pokusech.\n\nOtevřel jsem stránku vydání v prohlížeči. Optimizer zůstává spuštěný.",
                "Gasbyn Optimizer — aktualizace",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            UpdateLatestReleaseButton.IsEnabled = true;
            UpdateLatestReleaseButton.Content = "ZKUSIT ZNOVU";
            UpdateAvailableText.Text = $"Aktualizace {release.Tag} se nepodařila.";
            StartupScanDetails.Text += Environment.NewLine + Environment.NewLine +
                "AKTUALIZACE SE NEPOVEDLA" + Environment.NewLine + ex.Message;
            MessageBox.Show(
                "Aktualizace nebyla zahájena. Optimizer zůstává spuštěný." + Environment.NewLine + Environment.NewLine + ex.Message,
                "Gasbyn Optimizer",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
        CheckWorkspaceDriftOnStartup();
    }
}
