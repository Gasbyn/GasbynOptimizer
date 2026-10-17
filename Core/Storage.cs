using System.Text;
using System.Text.Json;

namespace GasbynOptimizer.Core;

public static class AppPaths
{
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer");

    public static string LogPath => Path.Combine(DataDir, "log.txt");
    public static string StatePath => Path.Combine(DataDir, "state.json");
    public static string SettingsPath => Path.Combine(DataDir, "settings.json");

    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}

public static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                AppPaths.EnsureDataDir();
                File.AppendAllText(AppPaths.LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }
        catch
        {
            // Logování nesmí nikdy shodit aplikaci.
        }
    }
}

/// <summary>Původní hodnota jedné změněné položky (registr, služba, plán napájení...).</summary>
public sealed class OpBackup
{
    public bool Existed { get; set; }
    public string Kind { get; set; } = "";
    public string Data { get; set; } = "";
}

public sealed class TweakBackup
{
    public Dictionary<string, OpBackup> Entries { get; set; } = new();
}

/// <summary>Záloha původního stavu pro každou optimalizaci. Díky ní jde vždy vrátit původní nastavení.</summary>
public sealed class BackupStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private Dictionary<string, TweakBackup> _data = new(StringComparer.OrdinalIgnoreCase);

    public static BackupStore Load()
    {
        var store = new BackupStore();
        try
        {
            if (File.Exists(AppPaths.StatePath))
            {
                var text = File.ReadAllText(AppPaths.StatePath, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, TweakBackup>>(text);
                if (loaded != null)
                    store._data = new Dictionary<string, TweakBackup>(loaded, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            Log.Write("Soubor se zálohou je poškozený, ponechávám kopii .bad: " + ex.Message);
            try { File.Move(AppPaths.StatePath, AppPaths.StatePath + ".bad", true); } catch { }
        }
        return store;
    }

    public TweakBackup Get(string tweakId)
    {
        if (!_data.TryGetValue(tweakId, out var backup))
        {
            backup = new TweakBackup();
            _data[tweakId] = backup;
        }
        return backup;
    }

    public void Remove(string tweakId) => _data.Remove(tweakId);

    public void Save()
    {
        AppPaths.EnsureDataDir();
        var temp = AppPaths.StatePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_data, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, AppPaths.StatePath, true);
    }
}

public sealed class AppSettings
{
    public bool AutoRestorePoint { get; set; } = true;

    /// <summary>Poslední pokus o aktualizaci (verze|čas UTC). Brání nekonečné smyčce, kdyby instalace selhala.</summary>
    public string LastUpdateAttempt { get; set; } = "";

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsPath, Encoding.UTF8));
                if (loaded != null) return loaded;
            }
        }
        catch
        {
            // Poškozené nastavení = výchozí hodnoty.
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            AppPaths.EnsureDataDir();
            File.WriteAllText(AppPaths.SettingsPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            Log.Write("Nastavení se nepodařilo uložit: " + ex.Message);
        }
    }
}
