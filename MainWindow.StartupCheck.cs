using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
            StartupScanDetails.Text =
                "Verze: " + GetDisplayVersion() + Environment.NewLine +
                "Správce (Admin): NE ✗" + Environment.NewLine + Environment.NewLine +
                "Gasbyn Optimizer potřebuje oprávnění správce pro systémovou diagnostiku a změny. Aplikace zatím neumožní pokračovat. Klikni na „SPUSTIT JAKO SPRÁVCE“ a potvrď dialog Windows.";
            CompleteStartupCheck(false);
            StartupScanAdminButton.Visibility = Visibility.Visible;
            StartupScanContinue.IsEnabled = false;
            return;
        }
        StartupScanContinue.Visibility = Visibility.Visible;
        StartupScanSpinner.Visibility = Visibility.Visible;
        StartupScanCompleteIcon.Visibility = Visibility.Collapsed;
        StartupScanProgress.Visibility = Visibility.Visible;
        StartupScanStatus.Text = "1/3 · Admin OK · Skenuji procesor, grafiku, RAM, desku a disky…";
        StartupScanDetails.Text = "Admin: ANO · Verze: " + GetDisplayVersion() + Environment.NewLine + "Načítám hardwarový profil. Kontrola je pouze čtecí.";

        var notes = new StringBuilder();
        try
        {
            _hardwareInventory = await Task.Run(HardwareInventoryService.Scan);
            CpuValue.Text = _hardwareInventory.Cpu;
            GpuValue.Text = _hardwareInventory.Gpu;
            RamValue.Text = _hardwareInventory.Memory;

            notes.AppendLine("APLIKACE");
            notes.AppendLine("Verze: " + GetDisplayVersion());
            notes.AppendLine("Správce (Admin): ANO ✓  — aplikace běží se zvýšenými oprávněními");
            notes.AppendLine();
            notes.AppendLine("HARDWARE");
            notes.AppendLine("CPU: " + _hardwareInventory.Cpu);
            notes.AppendLine("GPU: " + _hardwareInventory.Gpu);
            notes.AppendLine("RAM: " + _hardwareInventory.Memory);
            notes.AppendLine("Základní deska: " + _hardwareInventory.Motherboard);
            notes.AppendLine("Disky: " + _hardwareInventory.Disks);
            notes.AppendLine("Windows: " + _hardwareInventory.Windows);

            StartupScanStatus.Text = "2/3 · Kontroluji Windows nastavení relevantní pro hry…";
            StartupScanDetails.Text = notes.ToString();

            await Task.Delay(200);

            notes.AppendLine();
            notes.AppendLine("WINDOWS / HRANÍ");
            try
            {
                using var gameBar = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
                var allow = gameBar?.GetValue("AllowAutoGameMode");
                notes.AppendLine("Game Mode: " + (allow is int a && a == 1 ? "zapnutý" : "výchozí / vypnutý"));
            }
            catch { notes.AppendLine("Game Mode: nelze přečíst"); }

            try
            {
                using var hags = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
                var mode = hags?.GetValue("HwSchMode");
                notes.AppendLine("HAGS: " + (mode is int m ? (m == 2 ? "zapnuté" : $"hodnota {m}") : "výchozí"));
            }
            catch { notes.AppendLine("HAGS: nelze přečíst"); }

            StartupScanStatus.Text = "3/3 · Kontroluji dostupnost aktualizace…";
            StartupScanDetails.Text = notes.ToString();

            try
            {
                var release = await CheckForLatestReleaseAsync();
                if (release is not null)
                {
                    _latestRelease = release;
                    UpdateAvailablePanel.Visibility = Visibility.Visible;
                    UpdateAvailableText.Text = $"Verze {release.Value.Tag} je dostupná. Aktuální verze: {GetDisplayVersion()}.";
                    notes.AppendLine();
                    notes.AppendLine("AKTUALIZACE: dostupná " + release.Value.Tag);
                }
                else
                {
                    notes.AppendLine();
                    notes.AppendLine("AKTUALIZACE: jsi na aktuální veřejné verzi (nebo GitHub neodpověděl).");
                }
            }
            catch (Exception ex)
            {
                notes.AppendLine();
                notes.AppendLine("AKTUALIZACE: kontrola selhala — " + ex.Message);
            }

            StartupScanDetails.Text = notes.ToString();
            StartupScanStatus.Text = "Kontrola dokončena · " + GetDisplayVersion() + " · Admin OK";
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
        typeof(MainWindow).Assembly.GetName().Version ?? new Version(2, 0, 19);

    /// <summary>UI/release label in the form v2.0.B19 (build number = third Version component).</summary>
    private static string GetDisplayVersion()
    {
        var v = GetCurrentAppVersion();
        return $"v{v.Major}.{v.Minor}.B{v.Build}";
    }

    private static bool TryParseReleaseVersion(string tag, out Version version)
    {
        var normalizedTag = tag.Trim();
        if (normalizedTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalizedTag = normalizedTag[1..];

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
            parsedUrl.Scheme != Uri.UriSchemeHttps)
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
                var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (!string.Equals(name, "GasbynOptimizer-win-x64.zip", StringComparison.OrdinalIgnoreCase))
                    continue;
                downloadUrl = asset.TryGetProperty("browser_download_url", out var dl) ? dl.GetString() : null;
                if (asset.TryGetProperty("digest", out var digestEl))
                {
                    var digest = digestEl.GetString();
                    if (!string.IsNullOrWhiteSpace(digest))
                        sha256 = digest;
                }
                break;
            }
        }

        return (tag!, url!, downloadUrl, sha256);
    }

    private async void UpdateLatestRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_latestRelease is null)
            return;

        if (string.IsNullOrWhiteSpace(_latestRelease.Value.DownloadUrl) ||
            string.IsNullOrWhiteSpace(_latestRelease.Value.Sha256))
        {
            Process.Start(new ProcessStartInfo(_latestRelease.Value.Url) { UseShellExecute = true });
            return;
        }

        try
        {
            UpdateLatestReleaseButton.IsEnabled = false;
            UpdateLatestReleaseButton.Content = "STAHUJI…";
            await AppUpdater.PrepareAndLaunchAsync(
                _latestRelease.Value.DownloadUrl!,
                _latestRelease.Value.Sha256!,
                Environment.ProcessPath ?? throw new InvalidOperationException("Chybí cesta k exe."),
                Environment.ProcessId,
                msg => UpdateAvailableText.Text = msg);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Aktualizace", MessageBoxButton.OK, MessageBoxImage.Warning);
            UpdateLatestReleaseButton.IsEnabled = true;
            UpdateLatestReleaseButton.Content = "ZOBRAZIT";
        }
    }

    private void StartupScanContinue_Click(object sender, RoutedEventArgs e)
    {
        StartupScanOverlay.Visibility = Visibility.Collapsed;
    }
}
