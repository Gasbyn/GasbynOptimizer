using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GasbynOptimizer;

public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(RouteDynamicAction),
            true);
    }

    private static void RouteDynamicAction(object sender, MouseButtonEventArgs e)
    {
        if (sender is not MainWindow window || e.OriginalSource is not DependencyObject source)
            return;

        var button = FindParent<Button>(source);
        if (button is null) return;

        if (button.Content is string content && content.Contains("QUICK OPTIMIZE", System.StringComparison.OrdinalIgnoreCase))
        {
            window.QuickOptimizeSafe();
            e.Handled = true;
            return;
        }

        if (button.Tag is not string title || !button.IsDescendantOf(window.SecondaryContent))
            return;

        switch (title)
        {
            case "GAMING OPTIMIZER":
                window.RunGamingAudit();
                break;
            case "Hardware Accelerated GPU Scheduling":
            case "GPU Scheduling":
                window.ScanHags();
                break;
            case "High Performance Power Plan":
            case "Power Plan":
                window.FullOptimizer_Click(window, new RoutedEventArgs());
                break;
            case "Windows Gaming Services":
                window.ScanGamingServices();
                break;
            case "Fullscreen Optimization Check":
                window.ScanFullscreen();
                break;
            case "Fortnite Optimization Profile":
            case "Fortnite Profile":
                window.ScanFortniteProfile();
                break;
            case "NVIDIA Gaming Profile":
            case "NVIDIA Profile":
                window.ScanNvidiaProfile();
                break;
            case "Recycle Bin":
                window.ShowRecycleBinInfoReal();
                break;
            case "View startup":
                window.OpenStartupManager();
                break;
            case "Deep Scan Summary":
            case "Deep Clean":
                window.OpenDeepClean();
                break;
            case "Run health check":
                window.RunHealthCheck();
                break;
            default:
                window.RunDynamicAction(title);
                break;
        }

        e.Handled = true;
    }
}
