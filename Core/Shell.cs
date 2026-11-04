using System.ComponentModel;
using System.Diagnostics;

namespace GasbynOptimizer.Core;

/// <summary>Spouštění systémových nástrojů (sc, schtasks, powercfg, powershell) bez okna.</summary>
public static class Shell
{
    public sealed record Result(int ExitCode, string Output);

    public static string System32(string fileName) => Path.Combine(Environment.SystemDirectory, fileName);

    public static string PowerShellExe =>
        Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");

    public static Result Run(string fileName, string arguments, int timeoutMs = 60000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            // Oba proudy se čtou souběžně, aby se proces nezablokoval plným bufferem.
            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(true); } catch { /* proces už mohl skončit */ }
                return new Result(-1, "Vypršel časový limit: " + Path.GetFileName(fileName));
            }

            process.WaitForExit();
            var text = (outTask.Result + Environment.NewLine + errTask.Result).Trim();
            return new Result(process.ExitCode, text);
        }
        catch (Win32Exception ex)
        {
            return new Result(-2, "Nelze spustit " + Path.GetFileName(fileName) + ": " + ex.Message);
        }
    }
}
