using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using GasbynOptimizer.Models;

namespace GasbynOptimizer.Services;

public sealed record UninstallCandidate(string Type, string Path, string Risk, bool SelectedByDefault);

public sealed class ApplicationUninstaller
{
    public bool LaunchOfficialUninstaller(InstalledApplication app, out string message)
    {
        var command = !string.IsNullOrWhiteSpace(app.QuietUninstallCommand)
            ? app.QuietUninstallCommand
            : app.UninstallCommand;

        if (string.IsNullOrWhiteSpace(command))
        {
            message = "Pro tuto aplikaci nebyl nalezen registrovaný oficiální odinstalátor.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = true
            });
            message = "Oficiální odinstalátor byl spuštěn. Po jeho dokončení spusť znovu Deep Scan.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Oficiální odinstalátor se nepodařilo spustit: {ex.Message}";
            return false;
        }
    }

    public IReadOnlyList<UninstallCandidate> ScanLeftovers(InstalledApplication app)
    {
        var candidates = new List<UninstallCandidate>();

        if (!string.IsNullOrWhiteSpace(app.InstallLocation) && Directory.Exists(app.InstallLocation))
        {
            if (IsSafeCandidatePath(app.InstallLocation))
                candidates.Add(new UninstallCandidate("Install folder", app.InstallLocation, "MEDIUM — zkontroluj, zda jde opravdu o tuto aplikaci", false));
        }

        var appDataRoots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        foreach (var root in appDataRoots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (LooksRelated(name, app.Name) && IsSafeCandidatePath(dir))
                        candidates.Add(new UninstallCandidate("AppData folder", dir, "MEDIUM — název odpovídá aplikaci", false));
                }
            }
            catch
            {
                // Access-denied directories are ignored.
            }
        }

        foreach (var registryCandidate in FindRegistryCandidates(app))
            candidates.Add(registryCandidate);

        return candidates
            .GroupBy(x => $"{x.Type}|{x.Path}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public bool RemoveCandidate(UninstallCandidate candidate, out string message)
    {
        try
        {
            if (candidate.Type.Contains("Registry", StringComparison.OrdinalIgnoreCase))
            {
                if (!candidate.Path.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase) &&
                    !candidate.Path.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
                {
                    message = "Registry cesta není v povoleném rozsahu.";
                    return false;
                }

                var split = candidate.Path.IndexOf('\\');
                var hiveName = candidate.Path[..split];
                var subPath = candidate.Path[(split + 1)..];
                var hive = hiveName.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
                    ? Registry.CurrentUser
                    : Registry.LocalMachine;

                hive.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
                message = "Vybraná registry položka byla odstraněna.";
                return true;
            }

            if (!IsSafeCandidatePath(candidate.Path))
            {
                message = "Cesta je mimo bezpečný rozsah Deep Uninstall.";
                return false;
            }

            if (Directory.Exists(candidate.Path))
            {
                Directory.Delete(candidate.Path, recursive: true);
                message = "Vybraná složka byla odstraněna.";
                return true;
            }

            if (File.Exists(candidate.Path))
            {
                File.Delete(candidate.Path);
                message = "Vybraný soubor byl odstraněn.";
                return true;
            }

            message = "Položka už neexistuje.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Položku se nepodařilo odstranit: {ex.Message}";
            return false;
        }
    }

    private static IEnumerable<UninstallCandidate> FindRegistryCandidates(InstalledApplication app)
    {
        foreach (var hive in new[] { ("HKCU", Registry.CurrentUser), ("HKLM", Registry.LocalMachine) })
        {
            foreach (var path in new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            })
            {
                try
                {
                    using var root = hive.Item2.OpenSubKey(path, writable: false);
                    if (root is null) continue;
                    foreach (var name in root.GetSubKeyNames())
                    {
                        using var key = root.OpenSubKey(name, writable: false);
                        var display = key?.GetValue("DisplayName")?.ToString() ?? string.Empty;
                        if (string.Equals(display, app.Name, StringComparison.OrdinalIgnoreCase))
                            yield return new UninstallCandidate("Registry uninstall entry", $"{hive.Item1}\\{path}\\{name}", "MEDIUM — pouze pokud oficiální uninstall záznam zůstal", false);
                    }
                }
                catch { }
            }
        }
    }

    private static bool LooksRelated(string candidate, string appName)
    {
        var normalizedCandidate = Normalize(candidate);
        var normalizedApp = Normalize(appName);
        if (normalizedCandidate.Length < 4 || normalizedApp.Length < 4)
            return false;

        return normalizedCandidate.Contains(normalizedApp, StringComparison.OrdinalIgnoreCase) ||
               normalizedApp.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).ToArray());
    }

    private static bool IsSafeCandidatePath(string path)
    {
        try
        {
            var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
            var windows = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).TrimEnd(Path.DirectorySeparatorChar);
            var programFiles = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)).TrimEnd(Path.DirectorySeparatorChar);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var programFilesX86Full = string.IsNullOrWhiteSpace(programFilesX86) ? string.Empty : Path.GetFullPath(programFilesX86).TrimEnd(Path.DirectorySeparatorChar);
            var userProfile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)).TrimEnd(Path.DirectorySeparatorChar);

            if (full.Equals(windows, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(windows + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                full.Equals(programFiles, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(programFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(programFilesX86Full) && (full.Equals(programFilesX86Full, StringComparison.OrdinalIgnoreCase) || full.StartsWith(programFilesX86Full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                )
                return false;

            return full.StartsWith(userProfile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                   full.StartsWith(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
