using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>Stav jedné dílčí změny. Missing = cíl na tomto systému neexistuje (např. chybějící služba).</summary>
public enum OpState { On, Off, Missing }

/// <summary>Jedna dílčí změna uvnitř optimalizace. Umí zjistit stav, zapnout a vrátit zpět.</summary>
public abstract class TweakOp
{
    /// <summary>Unikátní klíč v rámci optimalizace, pod kterým se ukládá záloha původní hodnoty.</summary>
    public abstract string Key { get; }

    public abstract OpState Detect();

    public abstract void Apply(TweakBackup backup);

    public abstract void Revert(TweakBackup backup);
}

internal static class Reg
{
    public static RegistryKey Base(bool hklm) =>
        RegistryKey.OpenBaseKey(hklm ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);
}

/// <summary>Hodnota v registru. DWORD se zadává jako uint, řetězec jako string.</summary>
public sealed class RegistryOp : TweakOp
{
    private readonly bool _hklm;
    private readonly string _subKey;
    private readonly string _name;
    private readonly RegistryValueKind _kind;
    private readonly object _on;
    private readonly object? _off;
    private readonly string? _deleteTreeOnRevert;

    /// <param name="off">Výchozí hodnota pro vrácení, pokud chybí záloha. null = hodnotu smazat.</param>
    /// <param name="deleteTreeOnRevert">Pokud je zadáno, při vrácení se smaže celý tento klíč.</param>
    public RegistryOp(bool hklm, string subKey, string name, RegistryValueKind kind, object on, object? off,
        string? deleteTreeOnRevert = null)
    {
        _hklm = hklm;
        _subKey = subKey;
        _name = name;
        _kind = kind;
        _on = on;
        _off = off;
        _deleteTreeOnRevert = deleteTreeOnRevert;
    }

    public override string Key => (_hklm ? "HKLM" : "HKCU") + "\\" + _subKey + "\\" + _name;

    private object? ReadCurrent(out RegistryValueKind kind)
    {
        kind = RegistryValueKind.None;
        using var root = Reg.Base(_hklm);
        using var key = root.OpenSubKey(_subKey, false);
        if (key == null) return null;

        var value = key.GetValue(_name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (value == null) return null;

        kind = key.GetValueKind(_name);
        return value;
    }

    private bool Matches(object? current)
    {
        if (current == null) return false;

        if (_kind == RegistryValueKind.DWord)
            return current is int i && unchecked((uint)i) == Convert.ToUInt32(_on, CultureInfo.InvariantCulture);

        if (_kind == RegistryValueKind.String)
            return current is string s && string.Equals(s, (string)_on, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public override OpState Detect() => Matches(ReadCurrent(out _)) ? OpState.On : OpState.Off;

    public override void Apply(TweakBackup backup)
    {
        var current = ReadCurrent(out var currentKind);
        if (Matches(current)) return;

        // Původní hodnotu zálohujeme jen jednou, při prvním zapnutí.
        if (!backup.Entries.ContainsKey(Key))
            backup.Entries[Key] = MakeBackup(current, currentKind);

        WriteRaw(_kind, ToRaw(_on));
    }

    public override void Revert(TweakBackup backup)
    {
        if (_deleteTreeOnRevert != null)
        {
            using var root = Reg.Base(_hklm);
            root.DeleteSubKeyTree(_deleteTreeOnRevert, false);
            return;
        }

        if (backup.Entries.TryGetValue(Key, out var saved))
        {
            if (!saved.Existed) DeleteValue();
            else WriteBackup(saved);
            return;
        }

        if (_off == null) DeleteValue();
        else WriteRaw(_kind, ToRaw(_off));
    }

    private static object ToRaw(object value) => value is uint u ? unchecked((int)u) : value;

    private static OpBackup MakeBackup(object? current, RegistryValueKind kind)
    {
        if (current == null) return new OpBackup { Existed = false };

        var backup = new OpBackup { Existed = true, Kind = kind.ToString() };
        switch (kind)
        {
            case RegistryValueKind.DWord:
                backup.Data = ((int)current).ToString(CultureInfo.InvariantCulture);
                break;
            case RegistryValueKind.QWord:
                backup.Data = ((long)current).ToString(CultureInfo.InvariantCulture);
                break;
            case RegistryValueKind.String:
            case RegistryValueKind.ExpandString:
                backup.Data = (string)current;
                break;
            case RegistryValueKind.Binary:
                backup.Data = Convert.ToBase64String((byte[])current);
                break;
            default:
                throw new NotSupportedException("Nepodporovaný typ hodnoty v registru, nic jsem neměnil: " + kind);
        }
        return backup;
    }

    private void WriteBackup(OpBackup saved)
    {
        switch (saved.Kind)
        {
            case "DWord":
                WriteRaw(RegistryValueKind.DWord, int.Parse(saved.Data, CultureInfo.InvariantCulture));
                break;
            case "QWord":
                WriteRaw(RegistryValueKind.QWord, long.Parse(saved.Data, CultureInfo.InvariantCulture));
                break;
            case "String":
                WriteRaw(RegistryValueKind.String, saved.Data);
                break;
            case "ExpandString":
                WriteRaw(RegistryValueKind.ExpandString, saved.Data);
                break;
            case "Binary":
                WriteRaw(RegistryValueKind.Binary, Convert.FromBase64String(saved.Data));
                break;
            default:
                throw new NotSupportedException("Neznámý typ zálohy: " + saved.Kind);
        }
    }

    private void WriteRaw(RegistryValueKind kind, object value)
    {
        using var root = Reg.Base(_hklm);
        using var key = root.CreateSubKey(_subKey, true)
            ?? throw new InvalidOperationException("Nelze otevřít klíč registru: " + _subKey);
        key.SetValue(_name, value, kind);
    }

    private void DeleteValue()
    {
        using var root = Reg.Base(_hklm);
        using var key = root.OpenSubKey(_subKey, true);
        key?.DeleteValue(_name, false);
    }
}

/// <summary>Typ spouštění služby (zapnuto = cílový typ, typicky zakázána).</summary>
public sealed class ServiceOp : TweakOp
{
    private readonly string _name;
    private readonly int _targetStart;
    private readonly string _defaultWord;

    /// <param name="targetStart">Cílová hodnota Start v registru (4 = zakázáno, 3 = ručně).</param>
    /// <param name="defaultWord">Typ spouštění pro vrácení, pokud chybí záloha (auto, demand...).</param>
    public ServiceOp(string name, int targetStart = 4, string defaultWord = "demand")
    {
        _name = name;
        _targetStart = targetStart;
        _defaultWord = defaultWord;
    }

    public override string Key => "SVC:" + _name;

    private int? ReadStart(out bool delayed)
    {
        delayed = false;
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + _name, false);
        if (key == null) return null;

        delayed = key.GetValue("DelayedAutostart") is int d && d == 1;
        return key.GetValue("Start") is int s ? s : null;
    }

    public override OpState Detect()
    {
        var start = ReadStart(out _);
        if (start == null) return OpState.Missing;
        return start == _targetStart ? OpState.On : OpState.Off;
    }

    public override void Apply(TweakBackup backup)
    {
        var start = ReadStart(out var delayed);
        if (start == null || start == _targetStart) return;

        if (!backup.Entries.ContainsKey(Key))
            backup.Entries[Key] = new OpBackup { Existed = true, Kind = "svc", Data = ToWord(start.Value, delayed) };

        SetStart(ToWord(_targetStart, false));

        if (_targetStart == 4)
            Shell.Run(Shell.System32("sc.exe"), "stop \"" + _name + "\"", 20000); // best-effort, chyba nevadí
    }

    public override void Revert(TweakBackup backup)
    {
        if (ReadStart(out _) == null) return;

        var word = backup.Entries.TryGetValue(Key, out var saved) ? saved.Data : _defaultWord;
        SetStart(word);
    }

    private void SetStart(string word)
    {
        // Název služby se musí dát do uvozovek, jinak služby s mezerou v názvu (např. „AMD Crash Defender Service“) selžou.
        var result = Shell.Run(Shell.System32("sc.exe"), "config \"" + _name + "\" start= " + word, 30000);
        if (result.ExitCode == 0) return;

        // Záloha: některé služby odmítnou sc.exe, ale jdou přepnout přímo v registru (správce má právo zápisu).
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + _name, true);
            if (key == null) throw new InvalidOperationException("Služba v registru neexistuje.");

            var (start, delayed) = word switch
            {
                "boot" => (0, false),
                "system" => (1, false),
                "auto" => (2, false),
                "delayed-auto" => (2, true),
                "demand" => (3, false),
                _ => (4, false)
            };

            key.SetValue("Start", start, RegistryValueKind.DWord);
            if (delayed) key.SetValue("DelayedAutostart", 1, RegistryValueKind.DWord);
            else key.DeleteValue("DelayedAutostart", false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Službu „" + _name + "“ se nepodařilo upravit. sc.exe: " + result.Output.Trim() +
                                                " | registr: " + ex.Message);
        }
    }

    private static string ToWord(int start, bool delayed) => start switch
    {
        0 => "boot",
        1 => "system",
        2 => delayed ? "delayed-auto" : "auto",
        3 => "demand",
        _ => "disabled"
    };
}

/// <summary>Naplánovaná úloha Windows (zapnuto = úloha je zakázaná).</summary>
public sealed class TaskOp : TweakOp
{
    private readonly string _path;

    private static readonly Lazy<bool> TasksFolderReadable = new(() =>
    {
        try { return Directory.EnumerateFileSystemEntries(Path.Combine(Environment.SystemDirectory, "Tasks")).Any(); }
        catch { return false; }
    });

    public TaskOp(string path) => _path = path;

    public override string Key => "TASK:" + _path;

    public override OpState Detect()
    {
        // Rychlá cesta: definice úlohy je soubor v System32\Tasks (bez spouštění schtasks).
        try
        {
            var file = Path.Combine(Environment.SystemDirectory, "Tasks", _path.Trim('\\').Replace('/', '\\'));
            if (!File.Exists(file) && TasksFolderReadable.Value) return OpState.Missing;
            if (File.Exists(file))
            {
                var xml = File.ReadAllText(file);
                var block = Regex.Match(xml, "<Settings>.*?</Settings>", RegexOptions.Singleline);
                if (!block.Success) return OpState.Off;
                return Regex.IsMatch(block.Value, @"<Enabled>\s*false\s*</Enabled>", RegexOptions.IgnoreCase)
                    ? OpState.On : OpState.Off;
            }
        }
        catch { /* pokračuje se přes schtasks */ }

        var result = Shell.Run(Shell.System32("schtasks.exe"), "/Query /TN \"" + _path + "\" /XML", 30000);
        if (result.ExitCode != 0) return OpState.Missing;

        // Stav se čte z XML, protože textový výstup schtasks je lokalizovaný.
        var settings = Regex.Match(result.Output, "<Settings>.*?</Settings>", RegexOptions.Singleline);
        if (!settings.Success) return OpState.Off;

        return Regex.IsMatch(settings.Value, @"<Enabled>\s*false\s*</Enabled>", RegexOptions.IgnoreCase)
            ? OpState.On
            : OpState.Off;
    }

    public override void Apply(TweakBackup backup)
    {
        var state = Detect();
        if (state != OpState.Off) return;

        if (!backup.Entries.ContainsKey(Key))
            backup.Entries[Key] = new OpBackup { Existed = true, Kind = "task", Data = "enabled" };

        Change("/DISABLE");
    }

    public override void Revert(TweakBackup backup)
    {
        if (Detect() == OpState.Missing) return;

        if (backup.Entries.TryGetValue(Key, out var saved) && saved.Data == "disabled")
            return; // úloha byla zakázaná už před námi

        Change("/ENABLE");
    }

    private void Change(string flag)
    {
        var result = Shell.Run(Shell.System32("schtasks.exe"), "/Change /TN \"" + _path + "\" " + flag, 30000);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Úlohu " + _path + " se nepodařilo upravit (kód " + result.ExitCode + "): " + result.Output);
    }
}

/// <summary>
/// Vlastní plán napájení vložený v aplikaci jako soubor .pow. Zapnutí ho naimportuje (pod pevným GUID)
/// a nastaví jako aktivní. Původní aktivní plán zůstává ve Windows, jde na něj kdykoli přepnout.
/// </summary>
public sealed class PowerPlanImportOp : TweakOp
{
    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static readonly Regex GuidRegex =
        new("[0-9a-fA-F]{8}-([0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}", RegexOptions.Compiled);

    private readonly string _resource;
    private readonly string _guid;

    public PowerPlanImportOp(string resourceName, string planGuid)
    {
        _resource = resourceName;
        _guid = planGuid.ToLowerInvariant();
    }

    private static string PowerCfg => Shell.System32("powercfg.exe");

    public override string Key => "POWERPLAN:" + _guid;

    private static string? ActivePlan()
    {
        var result = Shell.Run(PowerCfg, "/getactivescheme", 20000);
        var match = GuidRegex.Match(result.Output);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    private static bool PlanExists(string guid) =>
        Shell.Run(PowerCfg, "/list", 20000).Output.ToLowerInvariant().Contains(guid);

    public override OpState Detect()
    {
        var active = ActivePlan();
        if (active == null) return OpState.Missing;
        return active == _guid ? OpState.On : OpState.Off;
    }

    private string ExtractResource()
    {
        var dir = Path.Combine(AppPaths.DataDir, "plans");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, _guid + ".pow");

        using var stream = typeof(PowerPlanImportOp).Assembly.GetManifestResourceStream(_resource)
            ?? throw new InvalidOperationException("Soubor plánu napájení není součástí aplikace: " + _resource);
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    public override void Apply(TweakBackup backup)
    {
        var active = ActivePlan();
        if (active == _guid) return;

        if (active != null && !backup.Entries.ContainsKey(Key))
            backup.Entries[Key] = new OpBackup { Existed = true, Kind = "powerplan", Data = active };
        Log.Write("Plán napájení: dosavadní aktivní plán " + (active ?? "neznámý") + ", aplikuji " + _guid);

        var path = ExtractResource();

        // Stejný plán už mohl být naimportován dřív. Smažeme ho a naimportujeme znovu, aby odpovídal souboru v aplikaci.
        if (PlanExists(_guid))
            Shell.Run(PowerCfg, "/delete " + _guid, 20000);

        var import = Shell.Run(PowerCfg, "/import \"" + path + "\" " + _guid, 30000);
        if (import.ExitCode != 0)
            throw new InvalidOperationException("Plán napájení se nepodařilo naimportovat (kód " + import.ExitCode + "): " + import.Output.Trim());

        var set = Shell.Run(PowerCfg, "/setactive " + _guid, 20000);
        if (set.ExitCode != 0)
            throw new InvalidOperationException("Plán napájení se nepodařilo aktivovat: " + set.Output.Trim());
    }

    public override void Revert(TweakBackup backup)
    {
        var target = backup.Entries.TryGetValue(Key, out var saved) ? saved.Data : Balanced;
        if (!PlanExists(target)) target = Balanced;

        var set = Shell.Run(PowerCfg, "/setactive " + target, 20000);
        if (set.ExitCode != 0)
            throw new InvalidOperationException("Původní plán napájení se nepodařilo obnovit: " + set.Output.Trim());
    }
}

/// <summary>Obecná změna přes příkaz (např. powercfg /hibernate). Stav určuje vlastní funkce.</summary>
public sealed class CommandOp : TweakOp
{
    private readonly string _key;
    private readonly Func<OpState> _detect;
    private readonly string _applyExe;
    private readonly string _applyArgs;
    private readonly string _revertExe;
    private readonly string _revertArgs;

    public CommandOp(string key, Func<OpState> detect, string applyExe, string applyArgs, string revertExe, string revertArgs)
    {
        _key = key;
        _detect = detect;
        _applyExe = applyExe;
        _applyArgs = applyArgs;
        _revertExe = revertExe;
        _revertArgs = revertArgs;
    }

    public override string Key => "CMD:" + _key;

    public override OpState Detect() => _detect();

    public override void Apply(TweakBackup backup)
    {
        if (Detect() == OpState.On) return;
        Execute(_applyExe, _applyArgs);
    }

    public override void Revert(TweakBackup backup) => Execute(_revertExe, _revertArgs);

    private static void Execute(string exe, string args)
    {
        var result = Shell.Run(exe, args, 60000);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(Path.GetFileName(exe) + " " + args + " selhalo (kód " + result.ExitCode + "): " + result.Output);
    }
}

/// <summary>
/// Automatické spouštění programu se systémem (položka v klíči Run). Vypíná se stejně jako ve Správci úloh,
/// přes příznak ve StartupApproved, takže se položka nemaže a jde vrátit zpět.
/// </summary>
public sealed class StartupEntryOp : TweakOp
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKey32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private readonly string _name;

    public StartupEntryOp(string valueName) => _name = valueName;

    public override string Key => "RUN:" + _name;

    /// <summary>Najde položku Run a vrátí, zda je v HKLM.</summary>
    private bool? FindEntry()
    {
        foreach (var (hklm, sub) in new[] { (false, RunKey), (true, RunKey), (true, RunKey32) })
        {
            using var root = Reg.Base(hklm);
            using var key = root.OpenSubKey(sub, false);
            if (key?.GetValue(_name) != null) return hklm;
        }
        return null;
    }

    private static string ApprovedSub(bool hklm) => ApprovedKey;

    public override OpState Detect()
    {
        var hklm = FindEntry();
        if (hklm == null) return OpState.Missing;

        using var root = Reg.Base(hklm.Value);
        using var key = root.OpenSubKey(ApprovedSub(hklm.Value), false);
        return key?.GetValue(_name) is byte[] { Length: > 0 } data && (data[0] & 1) == 1 ? OpState.On : OpState.Off;
    }

    public override void Apply(TweakBackup backup)
    {
        var hklm = FindEntry();
        if (hklm == null) return;

        using var root = Reg.Base(hklm.Value);
        using var key = root.CreateSubKey(ApprovedSub(hklm.Value), true);

        if (!backup.Entries.ContainsKey(Key))
        {
            var current = key.GetValue(_name) as byte[];
            backup.Entries[Key] = new OpBackup
            {
                Existed = current != null,
                Kind = "bytes",
                Data = current == null ? "" : Convert.ToHexString(current)
            };
        }

        var disabled = new byte[12];
        disabled[0] = 3;
        BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(disabled, 4);
        key.SetValue(_name, disabled, RegistryValueKind.Binary);
    }

    public override void Revert(TweakBackup backup)
    {
        var hklm = FindEntry();
        if (hklm == null) return;

        using var root = Reg.Base(hklm.Value);
        using var key = root.CreateSubKey(ApprovedSub(hklm.Value), true);

        if (backup.Entries.TryGetValue(Key, out var saved) && saved.Existed && saved.Data.Length > 0)
            key.SetValue(_name, Convert.FromHexString(saved.Data), RegistryValueKind.Binary);
        else
        {
            var enabled = new byte[12];
            enabled[0] = 2;
            key.SetValue(_name, enabled, RegistryValueKind.Binary);
        }
    }
}

/// <summary>
/// Stejná hodnota v registru u více podklíčů najednou (např. u všech síťových karet nebo USB zařízení).
/// Podklíče se hledají při každém použití, takže se vezmou i nově připojená zařízení.
/// </summary>
public sealed class MultiKeyOp : TweakOp
{
    private readonly string _id;
    private readonly Func<IEnumerable<string>> _paths;
    private readonly string _valueName;
    private readonly RegistryValueKind _kind;
    private readonly object _on;
    private readonly object? _offDefault;
    private readonly bool _requirePresent;
    private readonly Func<bool>? _gate;

    /// <param name="paths">Cesty podklíčů v HKLM.</param>
    /// <param name="requirePresent">Upraví jen podklíče, které danou hodnotu už mají (ostatní karty ji nepodporují).</param>
    /// <param name="gate">Když vrátí false, optimalizace je na tomto PC nedostupná.</param>
    public MultiKeyOp(string id, Func<IEnumerable<string>> paths, string valueName, RegistryValueKind kind,
        object on, object? offDefault, bool requirePresent, Func<bool>? gate = null)
    {
        _id = id;
        _paths = paths;
        _valueName = valueName;
        _kind = kind;
        _on = on;
        _offDefault = offDefault;
        _requirePresent = requirePresent;
        _gate = gate;
    }

    public override string Key => "MULTI:" + _id + ":" + _valueName;

    private static string Text(object? value) => value is null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    private List<string> Targets()
    {
        var result = new List<string>();
        using var root = Reg.Base(true);
        foreach (var path in _paths())
        {
            using var key = TryOpen(root, path);
            if (key == null) continue;
            if (_requirePresent && key.GetValue(_valueName) == null) continue;
            result.Add(path);
        }
        return result;
    }

    public override OpState Detect()
    {
        if (_gate != null && !_gate()) return OpState.Missing;

        var targets = Targets();
        if (targets.Count == 0) return OpState.Missing;

        using var root = Reg.Base(true);
        foreach (var path in targets)
        {
            using var key = TryOpen(root, path);
            if (!Text(key?.GetValue(_valueName)).Equals(Text(_on), StringComparison.OrdinalIgnoreCase))
                return OpState.Off;
        }
        return OpState.On;
    }

    private object Coerce(object value) => _kind == RegistryValueKind.DWord ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : value;

    public override void Apply(TweakBackup backup)
    {
        if (_gate != null && !_gate()) return;

        var targets = Targets();
        if (targets.Count == 0) return;

        var lines = new List<string>();
        var failed = 0;
        using var root = Reg.Base(true);
        foreach (var path in targets)
        {
            try
            {
                using var key = root.OpenSubKey(path, true);
                if (key == null) { failed++; continue; }

                var current = key.GetValue(_valueName);
                if (!Text(current).Equals(Text(_on), StringComparison.OrdinalIgnoreCase))
                    lines.Add(path + "\t" + (current == null ? "<none>" : Text(current)));

                key.SetValue(_valueName, Coerce(_on), _kind);
            }
            catch
            {
                failed++; // zařízení může zápis odmítnout, ostatní se upraví
            }
        }

        if (failed == targets.Count)
            throw new InvalidOperationException("Hodnotu „" + _valueName + "“ se nepodařilo zapsat (chybí oprávnění).");

        if (!backup.Entries.ContainsKey(Key))
            backup.Entries[Key] = new OpBackup { Existed = true, Kind = "multi", Data = string.Join("\n", lines) };
    }

    public override void Revert(TweakBackup backup)
    {
        using var root = Reg.Base(true);

        var saved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (backup.Entries.TryGetValue(Key, out var entry))
            foreach (var line in entry.Data.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t', 2);
                if (parts.Length == 2) saved[parts[0]] = parts[1];
            }

        foreach (var path in Targets())
        {
            try
            {
                using var key = root.OpenSubKey(path, true);
                if (key == null) continue;

                if (saved.TryGetValue(path, out var old))
                {
                    if (old == "<none>") key.DeleteValue(_valueName, false);
                    else key.SetValue(_valueName, _kind == RegistryValueKind.DWord ? Convert.ToInt32(old, CultureInfo.InvariantCulture) : old, _kind);
                }
                else if (_offDefault == null) key.DeleteValue(_valueName, false);
                else key.SetValue(_valueName, Coerce(_offDefault), _kind);
            }
            catch
            {
                // zařízení může zápis odmítnout
            }
        }
    }

    // --- výčty podklíčů ---------------------------------------------------------------------

    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\";

    private static RegistryKey? TryOpen(RegistryKey parent, string name)
    {
        try { return parent.OpenSubKey(name, false); }
        catch { return null; } // zařízení bez oprávnění ke čtení se přeskočí
    }

    /// <summary>Podklíče třídy zařízení (0000, 0001...), které mají zadanou hodnotu (poznávací znamení skutečného zařízení).</summary>
    public static IEnumerable<string> ClassKeys(string classGuid, string mustHaveValue)
    {
        using var root = Reg.Base(true);
        using var cls = root.OpenSubKey(ClassRoot + classGuid, false);
        if (cls == null) yield break;

        foreach (var name in cls.GetSubKeyNames())
        {
            if (!name.All(char.IsDigit)) continue;
            using var sub = TryOpen(cls, name);
            if (sub?.GetValue(mustHaveValue) != null) yield return ClassRoot + classGuid + "\\" + name;
        }
    }

    /// <summary>Skutečné síťové karty (mají NetCfgInstanceId a nejsou to virtuální adaptéry s příznakem *IfType 0).</summary>
    public static IEnumerable<string> NetworkAdapters() =>
        ClassKeys("{4d36e972-e325-11ce-bfc1-08002be10318}", "NetCfgInstanceId");

    /// <summary>Grafické karty zadaného výrobce (podle názvu ovladače nebo poskytovatele).</summary>
    public static IEnumerable<string> GpuAdapters(params string[] vendors)
    {
        const string display = "{4d36e968-e325-11ce-bfc1-08002be10318}";
        using var root = Reg.Base(true);
        using var cls = TryOpen(root, ClassRoot + display);
        if (cls == null) yield break;

        foreach (var name in cls.GetSubKeyNames())
        {
            if (!name.All(char.IsDigit)) continue;
            using var sub = TryOpen(cls, name);
            if (sub == null) continue;

            var text = (sub.GetValue("DriverDesc") as string ?? "") + " " + (sub.GetValue("ProviderName") as string ?? "");
            if (vendors.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase)))
                yield return ClassRoot + display + "\\" + name;
        }
    }

    public static IEnumerable<string> BluetoothAdapters() =>
        ClassKeys("{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}", "DriverDesc");

    public static IEnumerable<string> NetBtInterfaces()
    {
        const string path = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
        using var root = Reg.Base(true);
        using var key = root.OpenSubKey(path, false);
        if (key == null) yield break;
        foreach (var name in key.GetSubKeyNames())
            if (name.StartsWith("Tcpip_", StringComparison.OrdinalIgnoreCase)) yield return path + "\\" + name;
    }

    /// <summary>USB zařízení: Enum\USB\VID_...\sériové číslo\Device Parameters.</summary>
    public static IEnumerable<string> UsbDeviceParameters()
    {
        const string path = @"SYSTEM\CurrentControlSet\Enum\USB";
        using var root = Reg.Base(true);
        using var usb = root.OpenSubKey(path, false);
        if (usb == null) yield break;

        foreach (var hw in usb.GetSubKeyNames())
        {
            using var hwKey = TryOpen(usb, hw);
            if (hwKey == null) continue;
            foreach (var instance in hwKey.GetSubKeyNames())
            {
                var full = path + "\\" + hw + "\\" + instance + "\\Device Parameters";
                using var test = TryOpen(root, full);
                if (test != null) yield return full;
            }
        }
    }

    /// <summary>Bluetooth je k dispozici, když existuje adaptér a služba Bluetooth není zakázaná.</summary>
    public static bool BluetoothUsable()
    {
        using var svc = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\bthserv", false);
        if (svc == null || svc.GetValue("Start") is int { } start && start == 4) return false;
        return BluetoothAdapters().Any();
    }
}

/// <summary>
/// Aplikace z Obchodu / předinstalovaná aplikace Windows (balíček AppX). Stav „Vypnuto“ = aplikace je nainstalovaná,
/// „Zapnuto“ = odinstalovaná. Odinstalace je nevratná (aplikaci jde znovu nainstalovat z Microsoft Store).
/// </summary>
public sealed class StoreAppOp : TweakOp
{
    private readonly string[] _packages;

    public StoreAppOp(params string[] packageNames) => _packages = packageNames;

    public override string Key => "APPX:" + string.Join("+", _packages);

    private static bool HasPackage(string name)
    {
        foreach (var (hklm, sub) in new[]
                 {
                     (false, @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"),
                     (true, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\Applications")
                 })
        {
            try
            {
                using var root = Reg.Base(hklm);
                using var key = root.OpenSubKey(sub, false);
                if (key == null) continue;
                foreach (var child in key.GetSubKeyNames())
                    if (child.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { /* klíč nejde číst, bere se jako nenalezeno */ }
        }
        return false;
    }

    public override OpState Detect() => _packages.Any(HasPackage) ? OpState.Off : OpState.On;

    public override void Apply(TweakBackup backup)
    {
        var names = string.Join(",", _packages.Select(p => "'" + p + "'"));
        var script = "$ErrorActionPreference='SilentlyContinue'; foreach($n in @(" + names + ")){ " +
                     "Get-AppxPackage -AllUsers -Name $n | Remove-AppxPackage -AllUsers; " +
                     "Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $n } | Remove-AppxProvisionedPackage -Online | Out-Null }";
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var result = Shell.Run(Shell.PowerShellExe, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, 180000);
        if (Detect() != OpState.On)
            throw new InvalidOperationException("Aplikaci se nepodařilo odinstalovat (kód " + result.ExitCode + "): " + result.Output);
    }

    /// <summary>Odinstalaci nejde vrátit tlačítkem, aplikaci lze znovu nainstalovat z Microsoft Store.</summary>
    public override void Revert(TweakBackup backup) { }
}
