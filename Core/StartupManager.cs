using Microsoft.Win32;

namespace GasbynOptimizer.Core;

public enum StartupSource { RunUser, RunMachine, RunMachine32, FolderUser, FolderCommon }

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
        StartupSource.RunMachine32 => "Registr (32bitové aplikace)",
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
        StartupSource.RunMachine32 => "Run32",
        _ => "StartupFolder"
    };

    private static bool IsMachine(StartupSource s) => s is StartupSource.RunMachine or StartupSource.RunMachine32 or StartupSource.FolderCommon;

    public static List<StartupEntry> List()
    {
        var result = new List<StartupEntry>();

        foreach (var (source, hklm, sub) in new[]
                 {
                     (StartupSource.RunUser, false, RunKey),
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

        return result.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static (bool Ok, string Message) SetEnabled(StartupEntry entry, bool enabled)
    {
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
