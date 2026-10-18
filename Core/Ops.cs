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
