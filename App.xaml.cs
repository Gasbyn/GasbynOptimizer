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
}
