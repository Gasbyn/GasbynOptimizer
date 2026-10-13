using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GasbynOptimizer.Services;

internal static class PresentMonManager
{
    private const string LatestReleaseApi =
        "https://api.github.com/repos/GameTechDev/PresentMon/releases/latest";
    private const long MaximumExecutableSize = 16L * 1024 * 1024;

    private static readonly HttpClient Http = CreateHttpClient();

    public sealed record InstallResult(string ExecutablePath, string Version, bool AlreadyInstalled);

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GasbynOptimizer-PresentMon-Manager/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static async Task<InstallResult> DownloadLatestAsync(Action<string>? reportStatus = null)
    {
        reportStatus?.Invoke("Zjišťuji nejnovější oficiální vydání PresentMonu…");

        using var releaseResponse = await Http.GetAsync(LatestReleaseApi);
        releaseResponse.EnsureSuccessStatusCode();
        await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync();
        using var releaseJson = await JsonDocument.ParseAsync(releaseStream);
        var root = releaseJson.RootElement;

        if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException("Oficiální vydání PresentMonu je označeno jako draft.");
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException("GitHub vrátil předběžnou verzi PresentMonu; ta nebude automaticky instalována.");

        var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(tag) ||
            !Regex.IsMatch(tag, "^v?[0-9]+(?:\\.[0-9]+){1,3}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Oficiální vydání vrátilo neplatné číslo verze.");

        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Oficiální vydání neobsahuje seznam souborů.");

        JsonElement selectedAsset = default;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) ||
                !name.StartsWith("PresentMon-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith("-x64.exe", StringComparison.OrdinalIgnoreCase))
                continue;

            selectedAsset = asset;
            break;
        }

        if (selectedAsset.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("Oficiální vydání neobsahuje samostatný 64bitový PresentMon.exe.");

        var assetName = selectedAsset.GetProperty("name").GetString()!;
        var downloadUrl = selectedAsset.TryGetProperty("browser_download_url", out var downloadElement)
            ? downloadElement.GetString()
            : null;
        var digest = selectedAsset.TryGetProperty("digest", out var digestElement)
            ? digestElement.GetString()
            : null;

        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(
                $"/GameTechDev/PresentMon/releases/download/{tag}/",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Odkaz ke stažení nevede na oficiální vydání PresentMonu.");

        var expectedHash = digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true
            ? digest["sha256:".Length..]
            : digest;

        if (string.IsNullOrWhiteSpace(expectedHash) ||
            !Regex.IsMatch(expectedHash, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("GitHub neposkytl platný SHA-256 kontrolní součet PresentMonu.");

        var safeTag = tag.TrimStart('v', 'V');
        var installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GasbynOptimizer", "Tools", "PresentMon",
            $"{safeTag}-{expectedHash[..12].ToLowerInvariant()}");
        Directory.CreateDirectory(installDirectory);

        var finalPath = Path.Combine(installDirectory, assetName);
        if (File.Exists(finalPath))
        {
            reportStatus?.Invoke($"Ověřuji uložený PresentMon {safeTag}…");
            var existingHash = await CalculateSha256Async(finalPath);
            if (existingHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                return new InstallResult(finalPath, safeTag, true);

            // Never overwrite a file that may still be in use by a running capture.
            installDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GasbynOptimizer", "Tools", "PresentMon",
                $"{safeTag}-{expectedHash[..12].ToLowerInvariant()}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(installDirectory);
            finalPath = Path.Combine(installDirectory, assetName);
        }

        var temporaryPath = Path.Combine(installDirectory, assetName + ".download");
        try
        {
            reportStatus?.Invoke($"Stahuji PresentMon {safeTag}…");
            using (var downloadResponse = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
            {
                downloadResponse.EnsureSuccessStatusCode();
                if (downloadResponse.Content.Headers.ContentLength is long contentLength &&
                    contentLength > MaximumExecutableSize)
                    throw new InvalidOperationException("Stažený soubor překračuje bezpečnostní limit 16 MB.");

                await using var input = await downloadResponse.Content.ReadAsStreamAsync();
                await using var output = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long written = 0;
                var lastReportedPercent = -10;
                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (read == 0) break;

                    written += read;
                    if (written > MaximumExecutableSize)
                        throw new InvalidOperationException("Stažený soubor překročil bezpečnostní limit 16 MB.");

                    await output.WriteAsync(buffer.AsMemory(0, read));

                    if (downloadResponse.Content.Headers.ContentLength is long total && total > 0)
                    {
                        var percent = (int)Math.Min(100, written * 100 / total);
                        if (percent >= lastReportedPercent + 10)
                        {
                            lastReportedPercent = percent / 10 * 10;
                            reportStatus?.Invoke($"Stahuji PresentMon {safeTag}… {percent}%");
                        }
                    }
                }
            }

            reportStatus?.Invoke("Ověřuji SHA-256 staženého PresentMonu…");
            var downloadedHash = await CalculateSha256Async(temporaryPath);
            var expectedBytes = Encoding.ASCII.GetBytes(expectedHash.ToUpperInvariant());
            var actualBytes = Encoding.ASCII.GetBytes(downloadedHash);
            if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                throw new InvalidOperationException("Kontrolní součet nesouhlasí. Soubor nebyl použit.");

            File.Move(temporaryPath, finalPath, overwrite: false);
            reportStatus?.Invoke($"PresentMon {safeTag} je připraven.");
            return new InstallResult(finalPath, safeTag, false);
        }
        catch
        {
            try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { /* Best-effort cleanup. */ }
            throw;
        }
    }

    private static async Task<string> CalculateSha256Async(string path)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(file));
    }
}
