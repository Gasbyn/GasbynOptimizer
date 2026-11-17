using Microsoft.Win32;

namespace GasbynOptimizer.Core;

public enum StartupSource { RunUser, RunUser32, RunMachine, RunMachine32, FolderUser, FolderCommon, TaskLogon }

public sealed class StartupEntry
{
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required StartupSource Source { get; init; }
    public required bool Enabled { get; set; }
    /// <summary>U položek ve složce Po spuštění cesta k souboru.</summary>
    public string FilePath { get; init; } = "";

    public string Where => Source switch
    {
        StartupSource.RunUser => "Registr (jen tvůj účet)",
        StartupSource.RunMachine => "Registr (všichni uživatelé)",
        StartupSource.RunMachine32 or StartupSource.RunUser32 => "Registr (32bitové aplikace)",
        StartupSource.TaskLogon => "Naplánovaná úloha při přihlášení",
        StartupSource.FolderUser => "Složka Po spuštění (tvůj účet)",
        _ => "Složka Po spuštění (všichni uživatelé)"
    };
}

/// <summary>
/// Správce aplikací spouštěných s Windows. Zapnutí a vypnutí používá stejný záznam jako Správce úloh
/// (StartupApproved), takže nic nemaže a jde kdykoli vrátit zpět.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKey32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string Approved = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    private static bool IsEnabledData(object? data) => data is not byte[] { Length: > 0 } b || (b[0] & 1) == 0;

    private static string ApprovedSub(StartupSource source) => Approved + source switch
    {
        StartupSource.RunUser or StartupSource.RunMachine => "Run",
        StartupSource.RunMachine32 or StartupSource.RunUser32 => "Run32",
        _ => "StartupFolder"
    };

    private static bool IsMachine(StartupSource s) => s is StartupSource.RunMachine or StartupSource.RunMachine32 or StartupSource.FolderCommon;

    public static List<StartupEntry> List()
    {
        var result = new List<StartupEntry>();

        foreach (var (source, hklm, sub) in new[]
                 {
                     (StartupSource.RunUser, false, RunKey),
                     (StartupSource.RunUser32, false, RunKey32),
                     (StartupSource.RunMachine, true, RunKey),
                     (StartupSource.RunMachine32, true, RunKey32)
                 })
        {
            try
            {
                using var root = Reg.Base(hklm);
                using var run = root.OpenSubKey(sub, false);
                if (run == null) continue;
                using var approved = root.OpenSubKey(ApprovedSub(source), false);

                foreach (var name in run.GetValueNames())
                {
                    if (name.Length == 0) continue;
                    var command = Convert.ToString(run.GetValue(name)) ?? "";
                    result.Add(new StartupEntry
                    {
                        Name = name,
                        Command = command,
                        Source = source,
                        Enabled = IsEnabledData(approved?.GetValue(name))
                    });
                }
            }
            catch (Exception ex) { Log.Write("Spouštěné aplikace: čtení " + sub + " selhalo: " + ex.Message); }
        }

        foreach (var (source, folder) in new[]
                 {
                     (StartupSource.FolderUser, Environment.GetFolderPath(Environment.SpecialFolder.Startup)),
                     (StartupSource.FolderCommon, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup))
                 })
        {
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
                using var root = Reg.Base(IsMachine(source));
                using var approved = root.OpenSubKey(ApprovedSub(source), false);

                foreach (var file in Directory.GetFiles(folder))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    result.Add(new StartupEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Command = file,
                        Source = source,
                        Enabled = IsEnabledData(approved?.GetValue(fileName)),
                        FilePath = file
                    });
                }
            }
            catch (Exception ex) { Log.Write("Spouštěné aplikace: čtení složky selhalo: " + ex.Message); }
        }

        try { result.AddRange(ListLogonTasks()); }
        catch (Exception ex) { Log.Write("Spouštěné aplikace: čtení naplánovaných úloh selhalo: " + ex.Message); }

        return result.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>Naplánované úlohy spouštěné při přihlášení (bez systémových úloh Microsoftu).</summary>
    private static List<StartupEntry> ListLogonTasks()
    {
        const string script = @"
$ErrorActionPreference = 'SilentlyContinue'
Get-ScheduledTask | Where-Object { $_.TaskPath -notlike '\Microsoft\*' -and ($_.Triggers | Where-Object { $_.CimClass.CimClassName -eq 'MSFT_TaskLogonTrigger' }) } | ForEach-Object {
    $a = $_.Actions | Select-Object -First 1
    'TASK|' + $_.TaskPath + '|' + $_.TaskName + '|' + $_.State + '|' + (($a.Execute + ' ' + $a.Arguments).Trim())
}";
        var tasks = new List<StartupEntry>();
        var result = Shell.RunPowerShell(script, 60000);
        foreach (var raw in result.Output.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("TASK|", StringComparison.Ordinal)) continue;
            var parts = line.Split('|');
            if (parts.Length < 5 || parts[2].Length == 0) continue;

            tasks.Add(new StartupEntry
            {
                Name = parts[2],
                Command = parts[4],
                Source = StartupSource.TaskLogon,
                Enabled = !parts[3].Equals("Disabled", StringComparison.OrdinalIgnoreCase),
                FilePath = parts[1]
            });
        }
        return tasks;
    }

    public static (bool Ok, string Message) SetEnabled(StartupEntry entry, bool enabled)
    {
        if (entry.Source == StartupSource.TaskLogon)
        {
            try
            {
                var path = entry.FilePath.Replace("'", "''");
                var name = entry.Name.Replace("'", "''");
                var verb = enabled ? "Enable-ScheduledTask" : "Disable-ScheduledTask";
                var run = Shell.RunPowerShell(
                    "try { " + verb + " -TaskPath '" + path + "' -TaskName '" + name + "' -ErrorAction Stop | Out-Null; exit 0 } catch { Write-Output $_.Exception.Message; exit 1 }", 30000);
                if (run.ExitCode != 0) return (false, run.Output.Trim());
                entry.Enabled = enabled;
                Log.Write("Naplánovaná úloha „" + entry.Name + "“ " + (enabled ? "povolena" : "zakázána"));
                return (true, enabled ? "Úloha je zapnutá." : "Úloha je vypnutá.");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        try
        {
            var valueName = entry.Source is StartupSource.FolderUser or StartupSource.FolderCommon
                ? Path.GetFileName(entry.FilePath)
                : entry.Name;

            using var root = Reg.Base(IsMachine(entry.Source));
            using var key = root.CreateSubKey(ApprovedSub(entry.Source), true);

            var data = new byte[12];
            if (enabled)
            {
                data[0] = 2;
            }
            else
            {
                data[0] = 3;
                BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            }
            key.SetValue(valueName, data, RegistryValueKind.Binary);
            entry.Enabled = enabled;
            Log.Write("Spouštěná aplikace „" + entry.Name + "“ " + (enabled ? "povolena" : "zakázána"));
            return (true, enabled ? "Spouští se s Windows." : "Už se se spouštěním Windows nespustí.");
        }
        catch (Exception ex)
        {
            Log.Write("Spouštěná aplikace „" + entry.Name + "“: změna selhala: " + ex.Message);
            return (false, ex.Message);
        }
    }
}
