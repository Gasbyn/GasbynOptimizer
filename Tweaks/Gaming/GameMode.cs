using System.Text.Json;
using GasbynOptimizer.Models;
using Microsoft.Win32;

namespace GasbynOptimizer.Tweaks.Gaming;

public static class GameMode
{
    private const string RegistryPath = @"Software\Microsoft\GameBar";
    private const string ValueName = "AllowAutoGameMode";

    public static Tweak Create() => new()
    {
        Id = "game-mode",
        Name = "Windows Game Mode",
        Description = "Bezpečné zapnutí Game Mode s uložením původního nastavení pro Restore.",
        Category = "Gaming",
        Tier = Tier.Free,
        Apply = Apply,
        Restore = Restore,
        GetStatus = _ => GetCurrentStatus()
    };

    private static TweakResult Apply(TweakContext context)
    {
        try
        {
            var backupDirectory = EnsureBackupDirectory(context);
            var backupPath = Path.Combine(backupDirectory, "game-mode.json");

            if (!File.Exists(backupPath))
            {
                var original = ReadCurrentValue();
                File.WriteAllText(backupPath, JsonSerializer.Serialize(original));
            }

            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            if (key is null)
                return TweakResult.Fail("Nepodařilo se otevřít nastavení Game Mode.");

            key.SetValue(ValueName, 1, RegistryValueKind.DWord);

            return GetCurrentStatus() == TweakStatus.Enabled
                ? TweakResult.Ok("Windows Game Mode byl zapnut. Původní stav je uložen v backupu.")
                : TweakResult.Fail("Game Mode se nepodařilo ověřit po změně.");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Nepodařilo se zapnout Game Mode: {ex.Message}");
        }
    }

    private static TweakResult Restore(TweakContext context)
    {
        try
        {
            var backupPath = Path.Combine(EnsureBackupDirectory(context), "game-mode.json");
            if (!File.Exists(backupPath))
                return TweakResult.Fail("Backup Game Mode nebyl nalezen.");

            var original = JsonSerializer.Deserialize<BackupValue>(File.ReadAllText(backupPath));
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            if (key is null)
                return TweakResult.Fail("Nepodařilo se otevřít nastavení Game Mode.");

            if (original?.Value is null)
                key.DeleteValue(ValueName, false);
            else
                key.SetValue(ValueName, original.Value.Value, RegistryValueKind.DWord);

            return TweakResult.Ok("Původní nastavení Game Mode bylo obnoveno.");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Nepodařilo se obnovit Game Mode: {ex.Message}");
        }
    }

    private static TweakStatus GetCurrentStatus()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
            var value = key?.GetValue(ValueName);

            if (value is int number)
                return number == 1 ? TweakStatus.Enabled : TweakStatus.Disabled;

            return TweakStatus.NotConfigured;
        }
        catch
        {
            return TweakStatus.Unknown;
        }
    }

    private static BackupValue ReadCurrentValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
        var value = key?.GetValue(ValueName);
        return value is int number ? new BackupValue(number) : new BackupValue(null);
    }

    private static string EnsureBackupDirectory(TweakContext context)
    {
        var directory = string.IsNullOrWhiteSpace(context.BackupDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer", "Backups")
            : context.BackupDirectory;

        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record BackupValue(int? Value);
}
