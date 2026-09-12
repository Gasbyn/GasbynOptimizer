using System;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Windows;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void RunHealthCheck()
    {
        var sb = new StringBuilder();
        var good = 0;
        var warn = 0;
        var fail = 0;

        sb.AppendLine("GASBYN SYSTEM HEALTH CHECK");
        sb.AppendLine(new string('=', 30));
        sb.AppendLine();

        CheckDisk(sb, ref good, ref warn, ref fail);
        CheckWindowsServices(sb, ref good, ref warn, ref fail);
        CheckMemory(sb, ref good, ref warn, ref fail);
        CheckWindowsVersion(sb, ref good, ref warn, ref fail);
        CheckSystemFiles(sb, ref good, ref warn, ref fail);

        sb.AppendLine();
        sb.AppendLine(new string('-', 30));
        sb.AppendLine($"RESULT: {good} OK | {warn} WARNING | {fail} PROBLEM");
        sb.AppendLine();
        sb.AppendLine("Health Check nic nemaže ani nemění. Jde pouze o diagnostiku.");

        MessageBox.Show(sb.ToString(), "Gasbyn Optimizer — Health Check", MessageBoxButton.OK,
            fail > 0 ? MessageBoxImage.Warning : warn > 0 ? MessageBoxImage.Information : MessageBoxImage.Information);
    }

    private static void CheckDisk(StringBuilder sb, ref int good, ref int warn, ref int fail)
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(root);
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            var totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
            var freePercent = drive.TotalSize == 0 ? 0 : drive.AvailableFreeSpace * 100d / drive.TotalSize;

            if (freePercent >= 15)
            {
                good++;
                sb.AppendLine($"[OK] System drive: {freeGb:0.0} GB free / {totalGb:0.0} GB ({freePercent:0}% free)");
            }
            else if (freePercent >= 7)
            {
                warn++;
                sb.AppendLine($"[WARN] System drive is getting full: {freeGb:0.0} GB free ({freePercent:0}% free)");
            }
            else
            {
                fail++;
                sb.AppendLine($"[PROBLEM] System drive is critically low: {freeGb:0.0} GB free ({freePercent:0}% free)");
            }
        }
        catch (Exception ex)
        {
            warn++;
            sb.AppendLine($"[WARN] Disk check unavailable: {ex.Message}");
        }
    }

    private static void CheckWindowsServices(StringBuilder sb, ref int good, ref int warn, ref int fail)
    {
        CheckService("wuauserv", "Windows Update", sb, ref good, ref warn, ref fail);
        CheckService("WinDefend", "Microsoft Defender", sb, ref good, ref warn, ref fail);
    }

    private static void CheckService(string serviceName, string displayName, StringBuilder sb, ref int good, ref int warn, ref int fail)
    {
        try
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Running)
            {
                good++;
                sb.AppendLine($"[OK] {displayName}: Running");
            }
            else
            {
                warn++;
                sb.AppendLine($"[WARN] {displayName}: {service.Status}");
            }
        }
        catch
        {
            warn++;
            sb.AppendLine($"[WARN] {displayName}: service status unavailable");
        }
    }

    private static void CheckMemory(StringBuilder sb, ref int good, ref int warn, ref int fail)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject item in searcher.Get())
            {
                var total = Convert.ToDouble(item["TotalVisibleMemorySize"] ?? 0);
                var free = Convert.ToDouble(item["FreePhysicalMemory"] ?? 0);
                var usedPercent = total <= 0 ? 0 : (total - free) * 100d / total;

                if (usedPercent < 90)
                {
                    good++;
                    sb.AppendLine($"[OK] RAM usage: {usedPercent:0}%");
                }
                else
                {
                    warn++;
                    sb.AppendLine($"[WARN] RAM usage is high: {usedPercent:0}%");
                }
                return;
            }
        }
        catch (Exception ex)
        {
            warn++;
            sb.AppendLine($"[WARN] RAM check unavailable: {ex.Message}");
        }
    }

    private static void CheckWindowsVersion(StringBuilder sb, ref int good, ref int warn, ref int fail)
    {
        try
        {
            var version = Environment.OSVersion.Version;
            good++;
            sb.AppendLine($"[OK] Windows: {version}");
        }
        catch
        {
            warn++;
            sb.AppendLine("[WARN] Windows version could not be detected.");
        }
    }

    private static void CheckSystemFiles(StringBuilder sb, ref int good, ref int warn, ref int fail)
    {
        try
        {
            var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var critical = new[]
            {
                Path.Combine(systemRoot, "System32", "kernel32.dll"),
                Path.Combine(systemRoot, "System32", "ntdll.dll"),
                Path.Combine(systemRoot, "System32", "user32.dll")
            };

            var missing = critical.Where(path => !File.Exists(path)).ToArray();
            if (missing.Length == 0)
            {
                good++;
                sb.AppendLine("[OK] Critical Windows system files: present");
            }
            else
            {
                fail++;
                sb.AppendLine($"[PROBLEM] Missing critical system files: {missing.Length}");
            }
        }
        catch
        {
            warn++;
            sb.AppendLine("[WARN] Windows system-file check unavailable.");
        }
    }
}
