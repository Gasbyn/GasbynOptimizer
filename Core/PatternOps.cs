using Microsoft.Win32;

namespace GasbynOptimizer.Core;

/// <summary>
/// Zakáže všechny služby, jejichž název začíná daným prefixem. Hodí se pro aktualizátory, které s každou verzí
/// vytvoří novou službu (např. GoogleUpdaterService159.0.8140.0) a starou smažou. Vždy se hledá aktuální stav.
/// </summary>
public sealed class ServicePatternOp : TweakOp
{
    private readonly string _prefix;

    public ServicePatternOp(string prefix) => _prefix = prefix;

    public override string Key => "SVCPAT:" + _prefix;

    private List<ServiceOp> Current()
    {
        var list = new List<ServiceOp>();
        try
        {
            using var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services", false);
            if (services == null) return list;
            foreach (var name in services.GetSubKeyNames())
                if (name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
                    list.Add(new ServiceOp(name, 4, name.EndsWith("m", StringComparison.OrdinalIgnoreCase) ? "demand" : "auto"));
        }
        catch { /* bez práv se nic nenajde */ }
        return list;
    }

    public override OpState Detect()
    {
        var ops = Current();
        if (ops.Count == 0) return OpState.Missing;
        return ops.All(o => o.Detect() == OpState.On) ? OpState.On : OpState.Off;
    }

    public override void Apply(TweakBackup backup)
    {
        foreach (var op in Current()) op.Apply(backup);
    }

    public override void Revert(TweakBackup backup)
    {
        foreach (var op in Current()) op.Revert(backup);
    }
}

/// <summary>Zakáže všechny naplánované úlohy ve složce, jejichž název začíná prefixem (aktualizátory s měnícími se názvy).</summary>
public sealed class TaskPatternOp : TweakOp
{
    private readonly string _folder;
    private readonly string _prefix;

    public TaskPatternOp(string folder, string prefix)
    {
        _folder = folder.Trim('\\');
        _prefix = prefix;
    }

    public override string Key => "TASKPAT:" + _folder + "\\" + _prefix;

    private List<TaskOp> Current()
    {
        var list = new List<TaskOp>();
        try
        {
            var dir = Path.Combine(Environment.SystemDirectory, "Tasks", _folder);
            if (!Directory.Exists(dir)) return list;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
                    list.Add(new TaskOp(_folder.Length == 0 ? name : _folder + "\\" + name));
            }
        }
        catch { /* bez práv se nic nenajde */ }
        return list;
    }

    public override OpState Detect()
    {
        var ops = Current();
        if (ops.Count == 0) return OpState.Missing;
        var states = ops.Select(o => o.Detect()).Where(s => s != OpState.Missing).ToList();
        if (states.Count == 0) return OpState.Missing;
        return states.All(s => s == OpState.On) ? OpState.On : OpState.Off;
    }

    public override void Apply(TweakBackup backup)
    {
        foreach (var op in Current()) op.Apply(backup);
    }

    public override void Revert(TweakBackup backup)
    {
        foreach (var op in Current()) op.Revert(backup);
    }
}
