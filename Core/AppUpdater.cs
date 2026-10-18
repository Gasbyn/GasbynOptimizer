using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GasbynOptimizer.Core;

public sealed record UpdateInfo(Version Version, string Label, string DownloadUrl, string FileName, string? Sha256);

/// <summary>
/// Kontrola a instalace nových verzí z GitHub Releases. Aktualizace se bere jen z vydání,
/// které má přiložený instalátor GasbynOptimizer-Setup-*.exe (předběžná a rozpracovaná vydání se přeskakují).
/// </summary>
public static class AppUpdater
{
    private const string ReleasesApi = "https://api.github.com/repos/Gasbyn/GasbynOptimizer/releases?per_page=30";
    private const string AssetPrefix = "GasbynOptimizer-Setup";

    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All, ConnectTimeout = TimeSpan.FromSeconds(10) }) { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("GasbynOptimizer-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static Version Current => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0);

    /// <summary>Z tagu typu v2.0.b1.5 vyrobí verzi 2.0.1.5 (stejné pravidlo používá i build).</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;

        var numbers = Regex.Matches(tag, @"\d+")
            .Select(m => m.Value)
            .Take(4)
            .ToList();
        if (numbers.Count < 2) return null;

        return Version.TryParse(string.Join('.', numbers), out var version) ? version : null;
    }

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>Vrátí nejvyšší dostupnou verzi, pokud je novější než ta běžící. Jinak null.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct)
    {
        using var response = await Client.GetAsync(ReleasesApi, ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        UpdateInfo? best = null;

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (Flag(release, "draft") || Flag(release, "prerelease")) continue;

            var tag = release.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var version = ParseVersion(tag);
            if (version == null || tag == null) continue;
            if (best != null && version <= best.Version) continue;
            if (!release.TryGetProperty("assets", out var assets)) continue;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                if (!name.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase) ||
                    !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                // Stahujeme jen z GitHubu přes HTTPS.
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                    uri.Scheme != Uri.UriSchemeHttps ||
                    !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)) continue;

                string? sha = null;
                if (asset.TryGetProperty("digest", out var digestElement))
                {
                    var digest = digestElement.GetString();
                    if (digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        sha = digest.Substring("sha256:".Length);
                }

                best = new UpdateInfo(version, tag.TrimStart('v', 'V'), url, Path.GetFileName(name), sha);
                break;
            }
        }

        return best != null && best.Version > Current ? best : null;
    }

    /// <summary>Stáhne instalátor do dočasné složky a ověří kontrolní součet (pokud ho GitHub uvádí).</summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, IProgress<double> progress, CancellationToken ct)
    {
        var dir = Path.Combine(Path.GetTempPath(), "GasbynOptimizer-update");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, info.FileName);

        using var response = await Client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1;

        await using (var source = await response.Content.ReadAsStreamAsync(ct))
        await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, true))
        {
            var buffer = new byte[1 << 20];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        if (info.Sha256 != null)
        {
            string actual;
            await using (var check = File.OpenRead(path))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(check, ct));

            if (!actual.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(path); } catch { }
                throw new InvalidOperationException("Kontrolní součet staženého instalátoru nesouhlasí.");
            }
        }
        else
        {
            Log.Write("GitHub neuvedl kontrolní součet instalátoru, ověření přeskočeno.");
        }

        progress.Report(1);
        return path;
    }

    /// <summary>Spustí tichý instalátor. Ten aplikaci zavře, přepíše a znovu spustí.</summary>
    public static void StartInstaller(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true
        });
    }
}
