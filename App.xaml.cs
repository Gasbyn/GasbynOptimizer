using System.Threading;
using System.Windows;
using GasbynOptimizer.Core;
using GasbynOptimizer.UI;

namespace GasbynOptimizer;

public partial class App : System.Windows.Application
{
    private static Mutex? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, @"Local\GasbynOptimizer.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("Gasbyn Optimizer už běží.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write("Neočekávaná chyba: " + args.Exception);
            MessageBox.Show("Došlo k neočekávané chybě:\n" + args.Exception.Message + "\n\nPodrobnosti jsou v souboru log.txt.",
                "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var splash = new SplashWindow();
        splash.Show();

        try
        {
            if (await TryUpdateAsync(splash))
            {
                Shutdown();
                return;
            }

            splash.SetStatus("Čekejte, zjišťuji stav optimalizací…");
            splash.SetProgress(0, 1);

            var service = new TweakService();
            var tweaks = Catalog.All();

            var progress = new Progress<(int, int)>(p => splash.SetProgress(p.Item1, p.Item2));

            // Čekací okno se zobrazí vždy aspoň na chvíli, i když je kontrola rychlá.
            var scan = Task.Run(() => service.ScanAll(tweaks, progress));
            await Task.WhenAll(scan, Task.Delay(900));

            var window = new OptimizerWindow(service, tweaks, scan.Result);
            MainWindow = window;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            window.Show();
            splash.Close();
        }
        catch (Exception ex)
        {
            Log.Write("Spuštění selhalo: " + ex);
            MessageBox.Show("Aplikaci se nepodařilo spustit:\n" + ex.Message, "Gasbyn Optimizer",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>Zkontroluje GitHub a případně nainstaluje novou verzi. Vrací true, když se aplikace má ukončit.</summary>
    private static async Task<bool> TryUpdateAsync(SplashWindow splash)
    {
        try
        {
            UpdateInfo? info;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                info = await AppUpdater.CheckAsync(cts.Token);

            if (info == null) return false;

            // Pokud jsme se o stejnou verzi už před chvílí pokoušeli a pořád běží stará, nezkoušíme dokola.
            var settings = AppSettings.Load();
            var parts = settings.LastUpdateAttempt.Split('|');
            if (parts.Length == 2 && parts[0] == info.Label &&
                DateTime.TryParse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind, out var when) &&
                DateTime.UtcNow - when < TimeSpan.FromHours(6))
            {
                Log.Write("Aktualizace na " + info.Label + " se už zkoušela, přeskakuji.");
                return false;
            }

            splash.SetUpdating(info.Label);
            var progress = new Progress<double>(splash.SetFraction);
            var path = await AppUpdater.DownloadAsync(info, progress, CancellationToken.None);

            settings.LastUpdateAttempt = info.Label + "|" + DateTime.UtcNow.ToString("o");
            settings.Save();

            Log.Write("Spouštím instalaci verze " + info.Label);
            AppUpdater.StartInstaller(path);
            splash.SetStatus("Dokončuji aktualizaci…");
            await Task.Delay(2500);
            return true;
        }
        catch (Exception ex)
        {
            // Bez internetu nebo při chybě aplikace prostě normálně nastartuje.
            Log.Write("Kontrola aktualizací selhala: " + ex.Message);
            return false;
        }
    }
}
