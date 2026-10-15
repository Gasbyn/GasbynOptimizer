using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

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

        EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseEnterEvent,
            new MouseEventHandler(AnimateInteractiveControl), true);
        EventManager.RegisterClassHandler(typeof(Button), UIElement.MouseLeaveEvent,
            new MouseEventHandler(AnimateInteractiveControl), true);
        EventManager.RegisterClassHandler(typeof(ToggleButton), UIElement.MouseEnterEvent,
            new MouseEventHandler(AnimateInteractiveControl), true);
        EventManager.RegisterClassHandler(typeof(ToggleButton), UIElement.MouseLeaveEvent,
            new MouseEventHandler(AnimateInteractiveControl), true);
    }

    private static void AnimateInteractiveControl(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement control || !control.IsEnabled)
            return;

        if (control.RenderTransform is not ScaleTransform scale)
        {
            if (control.RenderTransform != Transform.Identity)
                return;

            scale = new ScaleTransform(1, 1);
            control.RenderTransformOrigin = new Point(0.5, 0.5);
            control.RenderTransform = scale;
        }

        var hovering = e.RoutedEvent == UIElement.MouseEnterEvent;
        var target = hovering ? 1.035 : 1.0;
        var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(125))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation.Clone());
    }

    private static void RouteDynamicAction(object sender, MouseButtonEventArgs e)
    {
        if (sender is not MainWindow window || e.OriginalSource is not DependencyObject source)
            return;

        var button = FindParent<Button>(source);
        if (button is null) return;

        // Deck UI owns these clicks (filters / back / search chrome) — do not show placeholder dialogs
        if (button.Tag is string deckTag && deckTag.StartsWith("deck:", StringComparison.OrdinalIgnoreCase))
            return;
        if (button.Content is string backText &&
            (backText.StartsWith("←", StringComparison.Ordinal) || backText.Contains("Zpět", StringComparison.OrdinalIgnoreCase)))
            return;

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
            case "Open Tweaks":
                window.ShowPage("Tweaks");
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
