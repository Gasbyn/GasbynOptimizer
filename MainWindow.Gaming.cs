using System;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void RunGamingAudit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("GASBYN GAMING AUDIT");
        sb.AppendLine(new string('=', 32));
        sb.AppendLine();

        CheckGameMode(sb);
        CheckHags(sb);
        CheckPowerPlan(sb);
        CheckFortnite(sb);
        CheckNvidia(sb);

        sb.AppendLine();
        sb.AppendLine("No settings were changed.");
        sb.AppendLine("Use the individual modules below to apply supported changes.");

        MessageBox.Show(sb.ToString(), "Gaming Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static void CheckGameMode(StringBuilder sb)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar");
            var value = key?.GetValue("AutoGameModeEnabled");
            var state = value is null ? "NOT CONFIGURED" : Convert.ToInt32(value) == 1 ? "ENABLED" : "DISABLED";
            sb.AppendLine($"Game Mode: {state}");
        }
        catch (Exception ex) { sb.AppendLine($"Game Mode: CHECK FAILED ({ex.Message})"); }
    }

    private static void CheckHags(StringBuilder sb)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers");
            var value = key?.GetValue("HwSchMode");
            var state = value is null ? "WINDOWS DEFAULT" : Convert.ToInt32(value) switch
            {
                2 => "ENABLED",
                1 => "DISABLED",
                _ => $"UNKNOWN ({value})"
            };
            sb.AppendLine($"HAGS: {state}");
        }
        catch (Exception ex) { sb.AppendLine($"HAGS: CHECK FAILED ({ex.Message})"); }
    }

    private static void CheckPowerPlan(StringBuilder sb)
    {
        var output = RunCommand("powercfg.exe", "/getactivescheme");
        var line = output.Replace(Environment.NewLine, " ").Trim();
        sb.AppendLine($"Power Plan: {line}");
    }

    private static void CheckFortnite(StringBuilder sb)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var config = Path.Combine(local, "FortniteGame", "Saved", "Config", "WindowsClient");
        sb.AppendLine($"Fortnite config: {(Directory.Exists(config) ? "FOUND" : "NOT FOUND")}");
    }

    private static void CheckNvidia(StringBuilder sb)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var drs = Path.Combine(local, "NVIDIA Corporation", "Drs");
        var dx = Path.Combine(local, "NVIDIA", "DXCache");
        sb.AppendLine($"NVIDIA profile data: {(Directory.Exists(drs) ? "FOUND" : "NOT FOUND")}");
        sb.AppendLine($"NVIDIA DX cache: {(Directory.Exists(dx) ? "FOUND" : "NOT FOUND")}");
    }
}
