using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using GasbynOptimizer.Models;

namespace GasbynOptimizer.Services;

public sealed class ApplicationInventory
{
    private static readonly string[] UninstallPaths =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    };

    public IReadOnlyList<InstalledApplication> Scan()
    {
        var result = new Dictionary<string, InstalledApplication>(StringComparer.OrdinalIgnoreCase);
        ScanHive(RegistryHive.CurrentUser, result);
        ScanHive(RegistryHive.LocalMachine, result);
        return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ScanHive(RegistryHive hive, Dictionary<string, InstalledApplication> result)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                foreach (var path in UninstallPaths)
                {
                    using var uninstall = baseKey.OpenSubKey(path, writable: false);
                    if (uninstall is null)
                        continue;

                    foreach (var name in uninstall.GetSubKeyNames())
                    {
                        try
                        {
                            using var appKey = uninstall.OpenSubKey(name, writable: false);
                            if (appKey is null)
                                continue;

                            var displayName = appKey.GetValue("DisplayName")?.ToString()?.Trim();
                            if (string.IsNullOrWhiteSpace(displayName))
                                continue;

                            var systemComponent = Convert.ToInt32(appKey.GetValue("SystemComponent", 0)) == 1;
                            var installLocation = appKey.GetValue("InstallLocation")?.ToString()?.Trim() ?? string.Empty;
                            var uninstallCommand = appKey.GetValue("UninstallString")?.ToString()?.Trim() ?? string.Empty;
                            var quietCommand = appKey.GetValue("QuietUninstallString")?.ToString()?.Trim() ?? string.Empty;
                            var publisher = appKey.GetValue("Publisher")?.ToString()?.Trim() ?? "Unknown";
                            var version = appKey.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "Unknown";
                            var size = ToLong(appKey.GetValue("EstimatedSize"));
                            var registryKey = $"{hive}\\{view}\\{path}\\{name}";

                            var app = new InstalledApplication
                            {
                                Name = displayName,
                                Version = string.IsNullOrWhiteSpace(version) ? "Unknown" : version,
                                Publisher = string.IsNullOrWhiteSpace(publisher) ? "Unknown" : publisher,
                                InstallLocation = installLocation,
                                UninstallCommand = uninstallCommand,
                                QuietUninstallCommand = quietCommand,
                                RegistryKey = registryKey,
                                EstimatedSizeKb = size,
                                IsSystemComponent = systemComponent
                            };

                            var key = $"{displayName}|{publisher}";
                            if (!result.ContainsKey(key) || (!result[key].IsSystemComponent && systemComponent))
                                result[key] = app;
                        }
                        catch
                        {
                            // A broken uninstall entry should not stop the complete inventory scan.
                        }
                    }
                }
            }
            catch
            {
                // Some registry hives/views may be inaccessible; continue with the others.
            }
        }
    }

    private static long ToLong(object? value)
    {
        return value switch
        {
            int i => Math.Max(0, i),
            long l => Math.Max(0, l),
            uint ui => ui,
            ulong ul => ul > long.MaxValue ? 0 : (long)ul,
            _ when long.TryParse(value?.ToString(), out var parsed) => Math.Max(0, parsed),
            _ => 0
        };
    }
}
