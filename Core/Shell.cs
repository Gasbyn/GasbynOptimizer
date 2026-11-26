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

    private const string ScriptMarker = "@@PS@@";

    /// <summary>
    /// Argumenty, kterými se powershell.exe spustí se skriptem předaným přes standardní vstup. Příkazový řádek je čitelný
    /// a nenese kódovaný (base64) obsah ani „-ExecutionPolicy Bypass“, což antiviry (např. AVG, hláška „Command line detection“)
    /// považují za podezřelé.
    /// </summary>
    public static string PowerShellScript(string script) => ScriptMarker + script;

    public static Result RunPowerShell(string script, int timeoutMs = 60000) =>
        Run(PowerShellExe, PowerShellScript(script), timeoutMs);

    public static Result Run(string fileName, string arguments, int timeoutMs = 60000)
    {
        string? input = null;
        if (arguments.StartsWith(ScriptMarker, StringComparison.Ordinal))
        {
            input = arguments[ScriptMarker.Length..];
            arguments = "-NoProfile -NonInteractive -Command -";
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input != null
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            if (input != null)
            {
                try
                {
                    process.StandardInput.Write(input);
                    process.StandardInput.WriteLine();
                    process.StandardInput.Close();
                }
                catch (IOException) { /* proces skončil dřív, než přečetl celý skript */ }
            }

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
