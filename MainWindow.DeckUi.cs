using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private WrapPanel? _tweakDeckHost;
    private TextBox? _tweakSearchBox;
    private string _tweakFilter = "all";
    private readonly List<(string Id, Border Card, string Title, string Description, string[] Badges, bool Incompatible)> _tweakDeckIndex = new();

    /// <summary>Back button for every workspace page — top-left above content.</summary>
    private void AddWorkspaceBackBar()
    {
        var bar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var back = new Button
        {
            Content = "← Zpět",
            Tag = "deck:back",
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidColorBrush(Color.FromRgb(28, 30, 36)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 58, 66)),
            BorderThickness = new Thickness(1)
        };
        back.Click += (_, _) => NavigateBack();
        Grid.SetColumn(back, 0);
        bar.Children.Add(back);
        SecondaryContent.Children.Add(bar);
    }

    private void EnsureTweakDeckHost(bool gamingOnly)
    {
        _tweakDeckIndex.Clear();
        _tweakDeckHost = null;
        _tweakSearchBox = null;
        _tweakFilter = "all";

        // Back is already added via AddPageHeader / AddWorkspaceBackBar on ShowPage.
        // Search + filters only for Tweaks/Gaming deck.
        var toolbar = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        var searchRow = new Grid();
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });

        var subtitle = new TextBlock
        {
            Text = gamingOnly ? "Gaming tweaks" : "System Tweaks — piš a filtruje se hned",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextMuted")
        };
        Grid.SetColumn(subtitle, 0);
        searchRow.Children.Add(subtitle);

        _tweakSearchBox = new TextBox
        {
            Height = 36,
            Padding = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(45, 48, 56)),
            BorderThickness = new Thickness(1),
            Tag = "deck:search",
            ToolTip = "Hledat tweak (live — bez Enter)"
        };
        // Live filter on every keystroke (no Enter needed)
        _tweakSearchBox.TextChanged += (_, _) => ApplyTweakDeckFilter();
        Grid.SetColumn(_tweakSearchBox, 1);
        searchRow.Children.Add(_tweakSearchBox);
        toolbar.Children.Add(searchRow);

        var filters = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        void AddFilter(string id, string label)
        {
            var btn = new Button
            {
                Content = label,
                Tag = "deck:filter:" + id,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(14, 7, 14, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                Background = id == "all"
                    ? (Brush)FindResource("OrangeSoft")
                    : new SolidColorBrush(Color.FromRgb(22, 24, 30)),
                Foreground = id == "all"
                    ? (Brush)FindResource("OrangeLight")
                    : (Brush)FindResource("TextMuted"),
                BorderBrush = id == "all"
                    ? new SolidColorBrush(Color.FromRgb(74, 40, 20))
                    : new SolidColorBrush(Color.FromRgb(45, 48, 56))
            };
            btn.Click += (_, _) =>
            {
                _tweakFilter = id;
                foreach (Button b in filters.Children.OfType<Button>())
                {
                    var on = string.Equals(b.Tag as string, "deck:filter:" + id, StringComparison.OrdinalIgnoreCase);
                    b.Background = on
                        ? (Brush)FindResource("OrangeSoft")
                        : new SolidColorBrush(Color.FromRgb(22, 24, 30));
                    b.Foreground = on
                        ? (Brush)FindResource("OrangeLight")
                        : (Brush)FindResource("TextMuted");
                    b.BorderBrush = on
                        ? new SolidColorBrush(Color.FromRgb(74, 40, 20))
                        : new SolidColorBrush(Color.FromRgb(45, 48, 56));
                }
                ApplyTweakDeckFilter();
            };
            filters.Children.Add(btn);
        }

        AddFilter("all", "Všechny");
        AddFilter("recommended", "Doporučené");
        AddFilter("optional", "Volitelné");
        AddFilter("privacy", "Soukromí");
        AddFilter("gaming", "Gaming");
        AddFilter("windows", "Windows");
        AddFilter("unsupported", "Nepodporované");
        toolbar.Children.Add(filters);

        SecondaryContent.Children.Add(toolbar);
        _tweakDeckHost = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        SecondaryContent.Children.Add(_tweakDeckHost);
    }

    private void ApplyTweakDeckFilter()
    {
        var q = (_tweakSearchBox?.Text ?? string.Empty).Trim();

        // If index empty (cards not registered), still try to show all
        foreach (var item in _tweakDeckIndex)
        {
            var textMatch = string.IsNullOrEmpty(q) ||
                            item.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            item.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            item.Id.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            item.Badges.Any(b => b.Contains(q, StringComparison.OrdinalIgnoreCase));

            var filterMatch = _tweakFilter switch
            {
                "recommended" => item.Badges.Any(b =>
                                       b.Contains("DOPORUČENO", StringComparison.OrdinalIgnoreCase) ||
                                       b.Contains("DOPORUCENO", StringComparison.OrdinalIgnoreCase)) ||
                                   (!item.Incompatible &&
                                    !item.Badges.Any(b =>
                                        b.Contains("POZOR", StringComparison.OrdinalIgnoreCase) ||
                                        b.Contains("POKROČILÉ", StringComparison.OrdinalIgnoreCase) ||
                                        b.Contains("POKROCILE", StringComparison.OrdinalIgnoreCase))),
                "optional" => item.Badges.Any(b =>
                                    b.Contains("VOLITELNÉ", StringComparison.OrdinalIgnoreCase) ||
                                    b.Contains("VOLITELNE", StringComparison.OrdinalIgnoreCase) ||
                                    b.Contains("PODMÍNĚNĚ", StringComparison.OrdinalIgnoreCase) ||
                                    b.Contains("PODMINENE", StringComparison.OrdinalIgnoreCase)),
                "privacy" => item.Badges.Any(b =>
                                   b.Contains("SOUKROMÍ", StringComparison.OrdinalIgnoreCase) ||
                                   b.Contains("SOUKROMI", StringComparison.OrdinalIgnoreCase)),
                "gaming" => item.Badges.Any(b =>
                                  b.Contains("GAMING", StringComparison.OrdinalIgnoreCase) ||
                                  b.Contains("FPS", StringComparison.OrdinalIgnoreCase) ||
                                  b.Contains("FORTNITE", StringComparison.OrdinalIgnoreCase)),
                "windows" => item.Badges.Any(b =>
                                   b.Contains("WINDOWS", StringComparison.OrdinalIgnoreCase) ||
                                   b.Contains("APLIKACE", StringComparison.OrdinalIgnoreCase)),
                "unsupported" => item.Incompatible,
                _ => true
            };

            item.Card.Visibility = textMatch && filterMatch ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
