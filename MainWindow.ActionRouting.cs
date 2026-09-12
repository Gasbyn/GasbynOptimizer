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
            Mouse.PreviewMouseLeftButtonDownEvent,
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

        window.RunDynamicAction(title);
        e.Handled = true;
    }
}
