using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using GasbynOptimizer.Models;

namespace GasbynOptimizer.Services;

public static class ApplicationManager
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public static IReadOnlyList<InstalledApplication> GetInstalledApplications()
    {
        var apps = new Dictionary<string, InstalledApplication>(StringComparer.OrdinalIgnoreCase);
        ReadUninstallRoot(RegistryHive.LocalMachine, RegistryView.Registry64, apps);
        ReadUninstallRoot(RegistryHive.LocalMachine, RegistryView.Registry32, apps);
        ReadUninstallRoot(RegistryHive.CurrentUser, RegistryView.Default, apps);

        return apps.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> FindSafeLeftovers(InstalledApplication app)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(app.InstallLocation))
            return results;

        try
        {
            var installPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(app.InstallLocation.Trim('"')));
            if (Directory.Exists(installPath))
                results.Add(installPath);
        }
        catch { }

        return results;
    }

    public static bool TryLaunchUninstaller(InstalledApplication app, out string message)
    {
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(app.UninstallCommand))
        {
            message = "Aplikace nemá v registru dostupný uninstall příkaz.";
            return false;
        }

        try
        {
            if (TryGetMsiProductCode(app.UninstallCommand, out var productCode))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "msiexec.exe",
                    Arguments = $"/x {productCode}",
                    UseShellExecute = true
                });
                message = "Spustil jsem oficiální MSI odinstalaci aplikace.";
                return true;
            }

            if (!TryParseExecutable(app.UninstallCommand, out var executable, out var arguments))
            {
                message = "Uninstall příkaz nebylo možné bezpečně rozpoznat. Ruční odinstalaci doporučuji přes Nastavení Windows.";
                return false;
            }

            if (!File.Exists(executable))
            {
                message = "Odinstalační program nebyl nalezen na očekávané cestě.";
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory,
                UseShellExecute = true
            });

            message = "Spustil jsem odinstalační program dodaný aplikací.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Odinstalaci se nepodařilo spustit: {ex.Message}";
            return false;
        }
    }

    private static void ReadUninstallRoot(RegistryHive hive, RegistryView view, Dictionary<string, InstalledApplication> apps)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(UninstallPath, false);
            if (root is null)
                return;

            foreach (var keyName in root.GetSubKeyNames())
            {
                try
                {
                    using var key = root.OpenSubKey(keyName, false);
                    var name = key?.GetValue("DisplayName")?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var systemComponent = ConvertToBool(key?.GetValue("SystemComponent"));
                    var releaseType = key?.GetValue("ReleaseType")?.ToString();
                    if (systemComponent || string.Equals(releaseType, "Security Update", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var app = new InstalledApplication
                    {
                        Name = name,
                        Version = key?.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "Unknown",
                        Publisher = key?.GetValue("Publisher")?.ToString()?.Trim() ?? "Unknown",
                        InstallLocation = key?.GetValue("InstallLocation")?.ToString()?.Trim() ?? string.Empty,
                        UninstallCommand = key?.GetValue("UninstallString")?.ToString()?.Trim() ?? string.Empty,
                        QuietUninstallCommand = key?.GetValue("QuietUninstallString")?.ToString()?.Trim() ?? string.Empty,
                        RegistryKey = $"{hive}\\{view}\\{UninstallPath}\\{keyName}",
                        EstimatedSizeKb = ConvertToLong(key?.GetValue("EstimatedSize")),
                        IsSystemComponent = systemComponent
                    };

                    var identity = $"{app.Name}|{app.Publisher}|{app.Version}";
                    if (!apps.ContainsKey(identity))
                        apps[identity] = app;
                }
                catch { }
            }
        }
        catch { }
    }

    private static bool ConvertToBool(object? value) => value switch
    {
        int number => number != 0,
        string text when int.TryParse(text, out var number) => number != 0,
        _ => false
    };

    private static long ConvertToLong(object? value) => value switch
    {
        int number => Math.Max(0, number),
        long number => Math.Max(0, number),
        string text when long.TryParse(text, out var number) => Math.Max(0, number),
        _ => 0
    };

    private static bool TryGetMsiProductCode(string command, out string productCode)
    {
        productCode = string.Empty;
        var text = command.Trim();
        var marker = text.IndexOf("{", StringComparison.Ordinal);
        if (marker < 0)
            return false;

        var end = text.IndexOf("}", marker + 1);
        if (end < 0)
            return false;

        var candidate = text.Substring(marker, end - marker + 1);
        if (candidate.Length == 38 && candidate.Count(c => c == '-') == 4)
        {
            productCode = candidate;
            return true;
        }

        return false;
    }

    private static bool TryParseExecutable(string command, out string executable, out string arguments)
    {
        executable = string.Empty;
        arguments = string.Empty;
        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.StartsWith('"'))
        {
            var closing = text.IndexOf('"', 1);
            if (closing < 0)
                return false;

            executable = text.Substring(1, closing - 1);
            arguments = text[(closing + 1)..].Trim();
        }
        else
        {
            var exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                exeIndex += 4;
                executable = text[..exeIndex].Trim();
                arguments = text[exeIndex..].Trim();
            }
            else
            {
                var firstSpace = text.IndexOf(' ');
                executable = firstSpace < 0 ? text : text[..firstSpace];
                arguments = firstSpace < 0 ? string.Empty : text[(firstSpace + 1)..].Trim();
            }
        }

        executable = executable.Trim('"');
        return executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && Path.IsPathFullyQualified(executable);
    }
}
