using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace GasbynOptimizer.Services;

internal static class PresentMonInstaller
{
    private const string LatestReleaseApi = "https://api.github.com/repos/GameTechDev/PresentMon/releases/latest";
    private static readonly string InstallDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GasbynOptimizer", "Tools", "PresentMon");
    private static readonly string ExecutablePath = Path.Combine(InstallDirectory, "PresentMon.exe");
    private static readonly string VersionPath = Path.Combine(InstallDirectory, "installed-version.txt");

    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GasbynOptimizer/2.x");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static bool IsInstalled => File.Exists(ExecutablePath);
    public static string InstalledPath => ExecutablePath;

    public static async Task<string> EnsureInstalledAsync(IProgress<string>? progress = null)
    {
        Directory.CreateDirectory(InstallDirectory);
        progress?.Report("Zjišťuji oficiální verzi PresentMon…");

        using var releaseResponse = await Client.GetAsync(LatestReleaseApi, HttpCompletionOption.ResponseHeadersRead);
        releaseResponse.EnsureSuccessStatusCode();
        await using var releaseStream = await releaseResponse.Content.ReadAsStreamAsync();
        using var release = await JsonDocument.ParseAsync(releaseStream);

        var root = release.RootElement;
        var version = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() : null;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean())
            throw new InvalidOperationException("GitHub vrátil neveřejnou vývojovou verzi PresentMon.");
        if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.GetBoolean())
            throw new InvalidOperationException("Poslední verze PresentMon je předběžná; z bezpečnostních důvodů ji automaticky neinstaluji.");

        JsonElement selected = default;
        var found = false;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(name) ||
                !name.StartsWith("PresentMon-", StringComparison.OrdinalIgnoreCase) ||
                !name.EndsWith("-x64.exe", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!asset.TryGetProperty("digest", out var digestElement)) continue;
            var digest = digestElement.GetString();
            if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                continue;
            selected = asset;
            found = true;
            break;
        }

        if (!found)
            throw new InvalidOperationException("V oficiálním vydání PresentMon nebyl nalezen x64 EXE soubor s SHA-256 kontrolním součtem.");

        if (File.Exists(ExecutablePath) && File.Exists(VersionPath))
        {
            try
            {
                if (string.Equals(File.ReadAllText(VersionPath).Trim(), version, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report($"PresentMon {version ?? ""} už je aktuální.");
                    return ExecutablePath;
                }
            }
            catch { }
        }

        var assetName = selected.GetProperty("name").GetString()!;
        var downloadUrl = selected.GetProperty("browser_download_url").GetString();
        var expectedDigest = selected.GetProperty("digest").GetString()!["sha256:".Length..];
        if (string.IsNullOrWhiteSpace(downloadUrl) ||
            !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Oficiální vydání obsahuje neplatný odkaz ke stažení.");

        progress?.Report($"Stahuji PresentMon {version ?? ""}…");
        var tempPath = Path.Combine(InstallDirectory, "PresentMon.download.tmp");
        try
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync())
            await using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                await source.CopyToAsync(destination);

            progress?.Report("Ověřuji SHA-256 staženého souboru…");
            await using var verifyStream = File.OpenRead(tempPath);
            var actualBytes = await SHA256.HashDataAsync(verifyStream);
            var actualDigest = Convert.ToHexString(actualBytes);
            if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SHA-256 nesouhlasí. Stažený soubor nebyl nainstalován.");

            var finalPath = Path.Combine(InstallDirectory, assetName);
            File.Move(tempPath, finalPath, overwrite: true);
            if (!string.Equals(finalPath, ExecutablePath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(finalPath, ExecutablePath, overwrite: true);
                File.Delete(finalPath);
            }

            File.WriteAllText(VersionPath, version ?? "unknown");
            progress?.Report($"PresentMon {version ?? ""} je připraven.");
            return ExecutablePath;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }
}
