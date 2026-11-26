using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>
/// Kontrola aktualizací za běhu aplikace, každou minutu.
/// Drobná verze (změní se jen poslední číslo, 2.x.x.Y) se nainstaluje sama na pozadí.
/// Větší verze (změní se číslo X v 2.X.X.Y) se zeptá, jestli aktualizovat hned, nebo později.
/// Při „Později“ se aplikace aktualizuje sama při příštím spuštění.
/// </summary>
public partial class OptimizerWindow
{
    private readonly DispatcherTimer _updateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private bool _updateChecking;
    private string _declinedUpdateKey = "";

    [StructLayout(LayoutKind.Sequential)]
    private struct FlashInfo
    {
        public uint Size;
        public IntPtr Window;
        public uint Flags;
        public uint Count;
        public uint Timeout;
    }

    [DllImport("user32.dll")]
    private static extern bool FlashWindowEx(ref FlashInfo info);

    private void StartAutoUpdate()
    {
        _updateTimer.Tick += async (_, _) => await AutoUpdateTickAsync();
        _updateTimer.Start();
    }

    private bool IsBusyNow() =>
        BusyOverlay.Visibility == Visibility.Visible || _uninstalling || _selfCheckRunning;

    private async Task AutoUpdateTickAsync()
    {
        if (_updateChecking) return;
        _updateChecking = true;
        try
        {
            UpdateInfo? info;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                info = await AppUpdater.CheckAsync(cts.Token);
            if (info == null) return;

            // Stejnou verzi, kterou se instalace nedávno nepodařila, nezkoušíme dokola.
            var parts = _settings.LastUpdateAttempt.Split('|');
            if (parts.Length == 2 && parts[0] == info.Label &&
                DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var when) &&
                DateTime.UtcNow - when < TimeSpan.FromHours(6))
                return;

            if (IsBusyNow()) return; // počká se na další minutu, až aplikace nic nedělá

            if (AppUpdater.IsMajorChange(info.Version, AppUpdater.Current))
            {
                var key = info.Version.Major + "." + info.Version.Minor + "." + info.Version.Build;
                if (key == _declinedUpdateKey) return;

                if (await AskMajorUpdateAsync(info))
                    await InstallUpdateAsync(info);
                else
                    _declinedUpdateKey = key;
            }
            else
            {
                await InstallUpdateAsync(info);
            }
        }
        catch (Exception ex)
        {
            // Bez internetu se jen počká na další pokus.
            Log.Write("Průběžná kontrola aktualizací selhala: " + ex.Message);
        }
        finally
        {
            _updateChecking = false;
        }
    }

    private static string CleanNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return "";
        var text = Regex.Replace(notes, @"[#*`]+", "").Trim();
        text = Regex.Replace(text, @"\r?\n{3,}", "\n\n");
        return text.Length > 800 ? text[..800] + "…" : text;
    }

    private void BringToFront()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
        Topmost = true;

        var info = new FlashInfo
        {
            Size = (uint)Marshal.SizeOf<FlashInfo>(),
            Window = new WindowInteropHelper(this).Handle,
            Flags = 0x3 | 0xC, // problikávat okno i tlačítko v liště, dokud se okno nedostane do popředí
            Count = uint.MaxValue,
            Timeout = 0
        };
        FlashWindowEx(ref info);
    }

    private async Task<bool> AskMajorUpdateAsync(UpdateInfo info)
    {
        BringToFront();
        try
        {
            var notes = CleanNotes(info.Notes);
            var text = "Je dostupná nová verze Gasbyn Optimizeru: " + info.Label + " (teď máš " + AppUpdater.Current + ").\n\n" +
                       (notes.Length > 0 ? "Nové funkce:\n" + notes + "\n\n" : "") +
                       "Aktualizovat teď?\n\nANO = teď (aplikace se restartuje)\nNE = později (aktualizuje se sama při příštím spuštění)";
            await Task.Yield();
            return MessageBox.Show(this, text, "Nová verze " + info.Label, MessageBoxButton.YesNo, MessageBoxImage.Information, MessageBoxResult.Yes) == MessageBoxResult.Yes;
        }
        finally
        {
            Topmost = false;
        }
    }

    private async Task InstallUpdateAsync(UpdateInfo info)
    {
        Log.Write("Průběžná aktualizace na " + info.Label);
        var path = await AppUpdater.DownloadAsync(info, new Progress<double>(), CancellationToken.None);

        // Za tu dobu se mohlo začít něco dělat, počkáme na klidnou chvíli.
        for (var i = 0; i < 20 && IsBusyNow(); i++) await Task.Delay(3000);
        if (IsBusyNow()) return;

        _settings.LastUpdateAttempt = info.Label + "|" + DateTime.UtcNow.ToString("o");
        _settings.Save();

        AppUpdater.StartInstaller(path);
        await Task.Delay(2500);
        System.Windows.Application.Current.Shutdown();
    }
}
