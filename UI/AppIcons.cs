using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>Načítání loga aplikací: z DisplayIcon v registru, ze souboru exe v její složce nebo z balíčku z Obchodu.</summary>
internal static class AppIcons
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);

    private static readonly string[] SkipNames = { "unins", "uninst", "uninstall", "update", "updater", "crash", "helper", "setup", "install" };

    private static ImageSource? FromIconFile(string file, int index)
    {
        var large = new IntPtr[1];
        try
        {
            if (ExtractIconEx(file, index, large, null, 1) == 0 || large[0] == IntPtr.Zero) return null;
            var source = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch { return null; }
        finally
        {
            if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
        }
    }

    private static ImageSource? FromPng(string file)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 48;
            image.UriSource = new Uri(file, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private static ImageSource? FromFile(string file, int index)
    {
        if (file.Length == 0 || !File.Exists(file)) return null;
        var ext = Path.GetExtension(file);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            return FromPng(file);
        return FromIconFile(file, index);
    }

    /// <summary>Najde v instalační složce exe, které nejspíš patří hlavnímu programu.</summary>
    private static string? GuessExe(string location, string appName)
    {
        try
        {
            if (location.Length == 0 || !Directory.Exists(location)) return null;
            var exes = Directory.GetFiles(location, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(f => !SkipNames.Any(n => Path.GetFileNameWithoutExtension(f).StartsWith(n, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (exes.Count == 0) return null;

            var compact = Regex.Replace(appName, @"[^A-Za-z0-9]", "").ToLowerInvariant();
            var match = exes.FirstOrDefault(f =>
            {
                var n = Regex.Replace(Path.GetFileNameWithoutExtension(f), @"[^A-Za-z0-9]", "").ToLowerInvariant();
                return n.Length >= 3 && (compact.Contains(n) || n.Contains(compact));
            });
            return match ?? exes[0];
        }
        catch { return null; }
    }

    private static string? ExeFromCommand(string command)
    {
        var match = Regex.Match(command, "^\\s*\"?([^\"]+?\\.exe)", RegexOptions.IgnoreCase);
        return match.Success ? Environment.ExpandEnvironmentVariables(match.Groups[1].Value) : null;
    }

    public static ImageSource? Load(InstalledApp app)
    {
        var icon = FromFile(app.IconPath, app.IconIndex);
        if (icon != null || app.IsStore) return icon;

        var guess = GuessExe(app.InstallLocation, app.Name);
        if (guess != null && (icon = FromFile(guess, 0)) != null) return icon;

        var fromCommand = ExeFromCommand(app.UninstallString);
        if (fromCommand != null && !Path.GetFileName(fromCommand).StartsWith("msiexec", StringComparison.OrdinalIgnoreCase))
            return FromFile(fromCommand, 0);
        return null;
    }

    /// <summary>Načte loga postupně na pozadí, aby seznam naskočil hned a okno nezamrzlo.</summary>
    public static async Task LoadAllAsync(IEnumerable<InstalledApp> apps, System.Windows.Threading.Dispatcher dispatcher)
    {
        foreach (var app in apps.ToList())
        {
            try
            {
                await dispatcher.InvokeAsync(() => app.Icon = Load(app), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch { /* bez loga zůstane prázdné místo */ }
        }
    }
}
