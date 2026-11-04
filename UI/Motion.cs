using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GasbynOptimizer.UI;

/// <summary>Plynulé animace rozhraní: vjezd karet, pulz při změně, plynulý a neindeterminovaný ukazatel průběhu.</summary>
internal static class Motion
{
    private static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction Back = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 };

    /// <summary>Přepíná se mezi animovanou a okamžitou změnou, když uživatel vypne animace ve Windows.</summary>
    private static bool Enabled => SystemParameters.ClientAreaAnimation;

    private static (ScaleTransform Scale, TranslateTransform Move) Prepare(FrameworkElement element)
    {
        if (element.RenderTransform is TransformGroup { Children.Count: 2 } group &&
            group.Children[0] is ScaleTransform s && group.Children[1] is TranslateTransform t)
            return (s, t);

        var scale = new ScaleTransform(1, 1);
        var move = new TranslateTransform();
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        element.RenderTransform = new TransformGroup { Children = { scale, move } };
        return (scale, move);
    }

    /// <summary>Karta vyjede zdola a zprůhlední (postupně podle pořadí, takže seznam „naběhne“).</summary>
    public static void Enter(FrameworkElement element, int index)
    {
        if (!Enabled) return;
        var (_, move) = Prepare(element);
        var delay = TimeSpan.FromMilliseconds(Math.Min(index, 14) * 26);

        var fade = new DoubleAnimation { From = 0, Duration = TimeSpan.FromMilliseconds(260), BeginTime = delay, EasingFunction = EaseOut };
        var slide = new DoubleAnimation { From = 16, To = 0, Duration = TimeSpan.FromMilliseconds(320), BeginTime = delay, EasingFunction = EaseOut };

        // Počáteční stav, aby karta před startem animace neproblikla v plné viditelnosti.
        var targetOpacity = element.Opacity;
        element.Opacity = 0;
        fade.To = targetOpacity;
        fade.Completed += (_, _) =>
        {
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.Opacity = targetOpacity;
        };
        move.Y = 16;
        slide.Completed += (_, _) =>
        {
            move.BeginAnimation(TranslateTransform.YProperty, null);
            move.Y = 0;
        };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        move.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    /// <summary>Krátké „cuknutí“ prvku při změně (zapnutí, vypnutí, výsledek akce).</summary>
    public static void Pop(FrameworkElement element)
    {
        if (!Enabled) return;
        var (scale, _) = Prepare(element);
        var up = new DoubleAnimation(1, 1.025, TimeSpan.FromMilliseconds(110)) { EasingFunction = EaseOut, AutoReverse = true };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, up);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, up);

        var fade = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>Jemné zvýraznění textu při změně stavu.</summary>
    public static void Blink(FrameworkElement element)
    {
        if (!Enabled) return;
        var (_, move) = Prepare(element);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(300)) { EasingFunction = Back });
    }

    /// <summary>Plynulý přechod stránky (při přepnutí kategorie).</summary>
    public static void Page(FrameworkElement element)
    {
        if (!Enabled) return;
        var (_, move) = Prepare(element);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = EaseOut });
        move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = EaseOut });
    }

    // --- ukazatele průběhu -------------------------------------------------------------------------

    /// <summary>
    /// Plynulá výplň ukazatele. Šířka se mění přes měřítko, takže animace je hladká a vždy dojede k aktuální hodnotě
    /// (výchozí ProgressBar ve WPF zaostává a ukazuje starší hodnotu).
    /// </summary>
    public static void SetBar(Border fill, double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fill.RenderTransform is not ScaleTransform scale)
        {
            fill.RenderTransformOrigin = new Point(0, 0.5);
            scale = new ScaleTransform(0, 1);
            fill.RenderTransform = scale;
        }

        if (!Enabled)
        {
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.ScaleX = fraction;
            return;
        }

        scale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(fraction, TimeSpan.FromMilliseconds(140)) { EasingFunction = EaseOut });
    }

    /// <summary>Nekonečný ukazatel: úsek, který plynule přejíždí zleva doprava (hned od prvního snímku, bez čekání na rozložení okna).</summary>
    public static void Indeterminate(Border track, Border fill, bool on)
    {
        if (fill.RenderTransform is not TranslateTransform move)
        {
            move = new TranslateTransform();
            fill.RenderTransform = move;
        }

        move.BeginAnimation(TranslateTransform.XProperty, null);
        if (!on)
        {
            fill.Opacity = 0;
            return;
        }

        var width = track.ActualWidth > 0 ? track.ActualWidth : (double.IsNaN(track.Width) ? 360 : track.Width);
        fill.Width = width * 0.35;
        fill.Opacity = 1;
        move.X = -fill.Width;
        move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-fill.Width, width, TimeSpan.FromMilliseconds(1100))
        {
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        });
    }
}
