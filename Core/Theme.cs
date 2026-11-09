using System.Windows;
using System.Windows.Media;

namespace GasbynOptimizer.Core;

/// <summary>Přepíná barvy aplikace. Volá se jednou při startu, před vytvořením jakéhokoli okna.</summary>
public static class Theme
{
    private static readonly Dictionary<string, string> Light = new()
    {
        ["BgBrush"] = "#F2F4F8",
        ["PanelBrush"] = "#FFFFFF",
        ["Panel2Brush"] = "#ECEFF4",
        ["BorderBrush"] = "#D3D8E0",
        ["TextBrush"] = "#14161B",
        ["MutedBrush"] = "#5B6372",
        ["OrangeSoftBrush"] = "#FFE8D6",
        ["GreenBrush"] = "#1F9D69",
        ["RedBrush"] = "#D64545",
        ["AmberBrush"] = "#D98A1E",
        ["TrackOffBrush"] = "#B9C0CC",
        ["ScrollThumbBrush"] = "#C3C9D4",
        ["ScrollThumbHoverBrush"] = "#9AA3B2"
    };

    public static bool IsLight { get; private set; }

    public static void Apply(string? theme)
    {
        IsLight = string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        if (!IsLight) return;

        var resources = System.Windows.Application.Current.Resources;
        foreach (var (key, hex) in Light)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                if (resources[key] is SolidColorBrush { IsFrozen: false } brush)
                    brush.Color = color;
                else
                    resources[key] = new SolidColorBrush(color);
            }
            catch (Exception ex) { Log.Write("Motiv: " + key + " se nepodařilo změnit: " + ex.Message); }
        }
    }
}
