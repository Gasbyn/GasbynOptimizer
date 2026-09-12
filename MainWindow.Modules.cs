using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void ScanHags()
    {
        const string path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        using var key = Registry.LocalMachine.OpenSubKey(path);
        var value = key?.GetValue("HwSchMode");
        var status = value is null ? "Windows default / not explicitly configured" : Convert.ToInt32(value) switch
        {
            2 => "ENABLED",
            1 => "DISABLED",
            _ => $"UNKNOWN ({value})"
        };
        MessageBox.Show($"Hardware-accelerated GPU scheduling\n\nStav: {status}\n\nTento modul pouze čte nastavení. Restart Windows může být po změně vyžadován.", "HAGS Check", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ManagePowerPlan()
    {
        var current = RunCommand("powercfg.exe", "/getactivescheme");
        var plans = RunCommand("powercfg.exe", "/list");
        var answer = MessageBox.Show(
            $"Aktivní power plan:\n{current}\n\nDostupné plány:\n{plans}\n\nChceš aktivovat vestavěný High Performance plán?\nExistující plány se nemažou.",
            "Power Plan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        var result = RunCommand("powercfg.exe", "/setactive SCHEME_MIN");
        MessageBox.Show($"Power plan command dokončen:\n\n{result}", "Power Plan", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanGamingServices()
    {
        var services = new[] { "GamingServices", "GamingServicesNet", "XblAuthManager", "XblGameSave", "XboxNetApiSvc" };
        var sb = new StringBuilder("Gaming Services\n\n");
        foreach (var name in services)
        {
            var output = RunCommand("sc.exe", $"query {name}");
            var state = output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase) ? "RUNNING" : output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase) ? "STOPPED" : "NOT INSTALLED / UNKNOWN";
            sb.AppendLine($"{name}: {state}");
        }
        MessageBox.Show(sb.ToString(), "Gaming Services", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanFullscreen()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore");
        var value = key?.GetValue("GameDVR_FSEBehaviorMode");
        var text = value is null ? "Není explicitně nastaveno." : $"GameDVR_FSEBehaviorMode = {value}";
        MessageBox.Show($"Fullscreen Optimization Check\n\n{text}\n\nTento modul nastavení pouze kontroluje a nic nepřepisuje.", "Fullscreen Check", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanFortniteProfile()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(local, "FortniteGame"),
            Path.Combine(local, "EpicGamesLauncher"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EpicGamesLauncher")
        };
        var sb = new StringBuilder("Fortnite Optimization Profile\n\n");
        foreach (var root in roots)
            sb.AppendLine($"{root}: {(Directory.Exists(root) ? "FOUND" : "not found")}");
        var config = Path.Combine(local, "FortniteGame", "Saved", "Config", "WindowsClient");
        if (Directory.Exists(config))
        {
            sb.AppendLine($"\nConfig folder: {config}");
            foreach (var file in Directory.EnumerateFiles(config, "*.ini", SearchOption.TopDirectoryOnly).Take(20)) sb.AppendLine($"  {Path.GetFileName(file)}");
        }
        MessageBox.Show(sb.ToString(), "Fortnite Profile", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanNvidiaProfile()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var paths = new[]
        {
            Path.Combine(local, "NVIDIA Corporation", "Drs"),
            Path.Combine(local, "NVIDIA", "DXCache"),
            Path.Combine(local, "NVIDIA", "GLCache")
        };
        var sb = new StringBuilder("NVIDIA Gaming Profile\n\n");
        foreach (var path in paths) sb.AppendLine($"{path}: {(Directory.Exists(path) ? "FOUND" : "not found")}");
        MessageBox.Show(sb.ToString(), "NVIDIA Profile", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string RunCommand(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process is null) return "Process se nepodařilo spustit.";
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            return string.IsNullOrWhiteSpace(stderr) ? stdout.Trim() : $"{stdout.Trim()}\n{stderr.Trim()}";
        }
        catch (Exception ex) { return $"Command error: {ex.Message}"; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RecycleBinInfo
    {
        public long ItemCount;
        public long TotalSize;
        public long Reserved;
    }

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref RecycleBinInfo pSHQueryRBInfo);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private void ShowRecycleBinInfoReal()
    {
        var info = new RecycleBinInfo();
        var hr = SHQueryRecycleBin(null, ref info);
        if (hr != 0)
        {
            MessageBox.Show("Windows nedovolil načíst stav Koše.", "Recycle Bin", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var answer = MessageBox.Show(
            $"Koš obsahuje {info.ItemCount} položek, přibližně {FormatBytes(info.TotalSize)}.\n\nChceš Koš úplně vysypat? Tato akce je nevratná.",
            "Recycle Bin",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        var result = SHEmptyRecycleBin(IntPtr.Zero, null, 0);
        MessageBox.Show(result == 0 ? "Koš byl vysypán." : $"Windows vrátilo chybu: 0x{result:X8}", "Recycle Bin", MessageBoxButton.OK, result == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
