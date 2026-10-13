using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GasbynOptimizer.Services;

internal static class AppUpdater
{
    private const long MaximumArchiveSize = 300L * 1024 * 1024;
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static async Task PrepareAndLaunchAsync(
        string downloadUrl,
        string publishedSha256,
        string executablePath,
        int parentProcessId,
        Action<string>? reportProgress = null)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !uri.AbsolutePath.StartsWith(
                "/Gasbyn/GasbynOptimizer/releases/download/",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Odkaz ke stažení nevede na důvěryhodné vydání Gasbyn Optimizeru.");
        }

        var expectedHash = publishedSha256.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            ? publishedSha256["sha256:".Length..]
            : publishedSha256;

        if (!Regex.IsMatch(expectedHash, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("GitHub neposkytl platný SHA-256 kontrolní součet aktualizace.");

        var fullExecutablePath = Path.GetFullPath(executablePath);
        var targetDirectory = Path.GetDirectoryName(fullExecutablePath);
        if (string.IsNullOrWhiteSpace(targetDirectory) || !File.Exists(fullExecutablePath))
            throw new InvalidOperationException("Nepodařilo se určit složku právě spuštěného optimizeru.");

        CleanupOldUpdateDirectories();

        var workDirectory = Path.Combine(Path.GetTempPath(), "GasbynOptimizerUpdate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        var zipPath = Path.Combine(workDirectory, "GasbynOptimizer-win-x64.zip");
        var helperPath = Path.Combine(workDirectory, "ApplyUpdate.ps1");

        try
        {
            reportProgress?.Invoke("Stahuji aktualizaci…");
            using (var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                if (response.Content.Headers.ContentLength is long length && length > MaximumArchiveSize)
                    throw new InvalidOperationException("Stažený balíček je nečekaně velký; aktualizace byla zastavena.");

                await using var input = await response.Content.ReadAsStreamAsync();
                await using var output = new FileStream(
                    zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long bytesWritten = 0;
                var lastReportedPercent = -10;

                while (true)
                {
                    var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length));
                    if (read == 0)
                        break;

                    bytesWritten += read;
                    if (bytesWritten > MaximumArchiveSize)
                        throw new InvalidOperationException("Stažený balíček překročil bezpečnostní limit 300 MB.");

                    await output.WriteAsync(buffer.AsMemory(0, read));

                    if (response.Content.Headers.ContentLength is long total && total > 0)
                    {
                        var percent = (int)Math.Min(100, bytesWritten * 100 / total);
                        if (percent >= lastReportedPercent + 10)
                        {
                            lastReportedPercent = percent / 10 * 10;
                            reportProgress?.Invoke($"Stahuji aktualizaci… {percent}%");
                        }
                    }
                }
            }

            reportProgress?.Invoke("Ověřuji integritu staženého balíčku…");
            await using (var file = File.OpenRead(zipPath))
            {
                var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file));
                var expectedBytes = Encoding.ASCII.GetBytes(expectedHash.ToUpperInvariant());
                var actualBytes = Encoding.ASCII.GetBytes(actualHash);
                if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
                    throw new InvalidOperationException("Kontrolní součet nesouhlasí. Aktualizace nebyla nainstalována.");
            }

            await File.WriteAllTextAsync(helperPath, UpdateScript, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var powerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(powerShell))
                throw new FileNotFoundException("Nepodařilo se najít Windows PowerShell pro bezpečné nahrazení souborů.", powerShell);

            var startInfo = new ProcessStartInfo
            {
                FileName = powerShell,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(helperPath);
            startInfo.ArgumentList.Add("-ZipPath");
            startInfo.ArgumentList.Add(zipPath);
            startInfo.ArgumentList.Add("-TargetDirectory");
            startInfo.ArgumentList.Add(targetDirectory);
            startInfo.ArgumentList.Add("-ExecutablePath");
            startInfo.ArgumentList.Add(fullExecutablePath);
            startInfo.ArgumentList.Add("-ParentProcessId");
            startInfo.ArgumentList.Add(parentProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-WorkDirectory");
            startInfo.ArgumentList.Add(workDirectory);

            reportProgress?.Invoke("Připravuji instalaci. Optimizer se za chvíli restartuje…");
            if (Process.Start(startInfo) is null)
                throw new InvalidOperationException("Aktualizační proces se nepodařilo spustit.");
        }
        catch
        {
            try { Directory.Delete(workDirectory, recursive: true); } catch { /* Best-effort cleanup. */ }
            throw;
        }
    }

    private static void CleanupOldUpdateDirectories()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-14);
            foreach (var directory in Directory.EnumerateDirectories(Path.GetTempPath(), "GasbynOptimizerUpdate-*"))
            {
                try
                {
                    if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                        Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // Temporary cleanup must never prevent an update.
                }
            }
        }
        catch
        {
            // Temporary cleanup must never prevent an update.
        }
    }

    private const string UpdateScript = """
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [Parameter(Mandatory = $true)][string]$TargetDirectory,
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][int]$ParentProcessId,
        [Parameter(Mandatory = $true)][string]$WorkDirectory
    )

    $ErrorActionPreference = 'Stop'
    $stageDirectory = Join-Path $WorkDirectory 'stage'
    $backupDirectory = Join-Path $WorkDirectory 'backup'
    $backupReady = $false
    $originalFiles = @{}
    $copiedPaths = @()

    try {
        $parent = Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue
        if ($null -ne $parent) {
            Wait-Process -Id $ParentProcessId -Timeout 60 -ErrorAction Stop
        }
        if (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue) {
            throw 'Gasbyn Optimizer se nepodařilo bezpečně zavřít.'
        }

        if (Test-Path -LiteralPath $stageDirectory) {
            Remove-Item -LiteralPath $stageDirectory -Recurse -Force
        }
        New-Item -ItemType Directory -Path $stageDirectory -Force | Out-Null

        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        try {
            $stagePrefix = [IO.Path]::GetFullPath($stageDirectory).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
            foreach ($entry in $archive.Entries) {
                if ([string]::IsNullOrWhiteSpace($entry.FullName)) { continue }
                $destination = [IO.Path]::GetFullPath((Join-Path $stageDirectory $entry.FullName))
                if (-not $destination.StartsWith($stagePrefix, [StringComparison]::OrdinalIgnoreCase)) {
                    throw 'Balíček obsahuje neplatnou cestu. Aktualizace byla zastavena.'
                }
            }
        }
        finally {
            $archive.Dispose()
        }

        [System.IO.Compression.ZipFile]::ExtractToDirectory($ZipPath, $stageDirectory)

        $newExecutable = Join-Path $stageDirectory 'GasbynOptimizer.exe'
        if (Test-Path -LiteralPath $newExecutable) {
            $sourceDirectory = $stageDirectory
        }
        else {
            $foundExe = Get-ChildItem -LiteralPath $stageDirectory -Filter 'GasbynOptimizer.exe' -File -Recurse | Select-Object -First 1
            if ($null -eq $foundExe) {
                throw 'Stažený balíček neobsahuje GasbynOptimizer.exe.'
            }
            $sourceDirectory = $foundExe.DirectoryName
        }

        if (Test-Path -LiteralPath $backupDirectory) {
            Remove-Item -LiteralPath $backupDirectory -Recurse -Force
        }
        New-Item -ItemType Directory -Path $backupDirectory -Force | Out-Null

        $oldFiles = @(Get-ChildItem -LiteralPath $TargetDirectory -File -Recurse -Force)
        foreach ($file in $oldFiles) {
            $relative = [IO.Path]::GetRelativePath($TargetDirectory, $file.FullName)
            $originalFiles[$relative] = $true
            $backupPath = Join-Path $backupDirectory $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $backupPath) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $backupPath -Force
        }
        $backupReady = $true

        $newFiles = @(Get-ChildItem -LiteralPath $sourceDirectory -File -Recurse -Force)
        if ($newFiles.Count -eq 0) { throw 'Balíček aktualizace je prázdný.' }

        foreach ($file in $newFiles) {
            $relative = [IO.Path]::GetRelativePath($sourceDirectory, $file.FullName)
            $copiedPaths += $relative
            $destination = Join-Path $TargetDirectory $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }

        if (-not (Test-Path -LiteralPath $ExecutablePath)) {
            throw 'Po aktualizaci nebyl nalezen spustitelný soubor.'
        }

        $newProcess = Start-Process -FilePath $ExecutablePath -WorkingDirectory $TargetDirectory -PassThru
        Start-Sleep -Seconds 2
        if ($newProcess.HasExited) {
            throw 'Nová verze se ihned po spuštění ukončila.'
        }

        Remove-Item -LiteralPath $stageDirectory -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
    }
    catch {
        $failureMessage = $_.Exception.Message
        try {
            if ($backupReady -and (Test-Path -LiteralPath $backupDirectory)) {
                foreach ($relative in $copiedPaths) {
                    if (-not $originalFiles.ContainsKey($relative)) {
                        $newPath = Join-Path $TargetDirectory $relative
                        if (Test-Path -LiteralPath $newPath) {
                            Remove-Item -LiteralPath $newPath -Force -ErrorAction SilentlyContinue
                        }
                    }
                }

                $backupFiles = @(Get-ChildItem -LiteralPath $backupDirectory -File -Recurse -Force)
                foreach ($file in $backupFiles) {
                    $relative = [IO.Path]::GetRelativePath($backupDirectory, $file.FullName)
                    $destination = Join-Path $TargetDirectory $relative
                    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
                }
            }
        }
        catch {
            $failureMessage += ([Environment]::NewLine + 'Obnova původních souborů také narazila na chybu: ' + $_.Exception.Message)
        }

        if (-not (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue)) {
            try { Start-Process -FilePath $ExecutablePath -WorkingDirectory $TargetDirectory | Out-Null } catch { }
        }

        try {
            Add-Type -AssemblyName PresentationFramework
            [System.Windows.MessageBox]::Show(
                ('Aktualizaci se nepodařilo dokončit.' + [Environment]::NewLine + [Environment]::NewLine + $failureMessage + [Environment]::NewLine + [Environment]::NewLine + 'Pokud už začala výměna souborů, aktualizátor se pokusil obnovit zálohu.'),
                'Gasbyn Optimizer — aktualizace',
                [System.Windows.MessageBoxButton]::OK,
                [System.Windows.MessageBoxImage]::Error
            ) | Out-Null
        }
        catch { }
        exit 1
    }
""";
}