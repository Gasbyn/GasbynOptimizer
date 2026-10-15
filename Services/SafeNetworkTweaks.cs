using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace GasbynOptimizer.Services;

/// <summary>
/// Conservative network tweaks inspired by common gaming optimizers
/// (Gasbyn CMD 2.0, Chris Titus-style TCP hygiene). No Windows Update,
/// Defender, print spooler, or mass service disables.
/// </summary>
public static class SafeNetworkTweaks
{
    public static string ApplyLite()
    {
        var log = new StringBuilder();
        var commands = new (string FileName, string Args, string Label)[]
        {
            ("netsh", "int tcp set heuristics disabled", "TCP heuristics OFF"),
            ("netsh", "int tcp set global timestamps=disabled", "TCP timestamps OFF"),
            ("netsh", "int tcp set global rss=enabled", "RSS ON"),
            ("netsh", "int tcp set global autotuninglevel=normal", "Autotuning NORMAL"),
            ("netsh", "int tcp set global ecncapability=disabled", "ECN OFF"),
            ("netsh", "interface teredo set state disabled", "Teredo OFF"),
            ("ipconfig", "/flushdns", "Flush DNS"),
        };

        foreach (var (fileName, args, label) in commands)
        {
            try
            {
                var output = Run(fileName, args);
                log.AppendLine($"OK  {label}");
                if (!string.IsNullOrWhiteSpace(output))
                    log.AppendLine("    " + output.Replace("\r", " ").Replace("\n", " ").Trim());
            }
            catch (Exception ex)
            {
                log.AppendLine($"FAIL {label}: {ex.Message}");
            }
        }

        OptimizerAuditLog.WriteChange(
            "network-lite",
            "Safe network lite",
            "ENABLE",
            true,
            new[] { log.ToString() },
            "Applied conservative TCP/DNS network tweaks.");

        return log.ToString();
    }

    private static string Run(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Nelze spustit {fileName}.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(15000);
        return (stdout + stderr).Trim();
    }
}
