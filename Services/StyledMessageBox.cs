using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace GasbynOptimizer;

/// <summary>
/// Branded replacement for the standard Windows MessageBox used by optimizer modules.
/// Keeps the familiar MessageBox.Show API while providing a readable Gasbyn UI.
/// </summary>
public static class MessageBox
{
    public static System.Windows.MessageBoxResult Show(string message, string caption = "Gasbyn Optimizer", System.Windows.MessageBoxButton buttons = System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage image = System.Windows.MessageBoxImage.None)
    {
        return ShowInternal(message, caption, buttons, image, null);
    }

    public static System.Windows.MessageBoxResult Show(Window? owner, string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image)
    {
        return ShowInternal(message, caption, buttons, image, owner);
    }

    private static System.Windows.MessageBoxResult ShowInternal(string message, string caption, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image, Window? owner)
    {
        var window = new Window
        {
            Title = caption,
            Width = 620,
            MinWidth = 480,
            Height = 360,
            MinHeight = 260,
            MaxHeight = 720,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.CanResize,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(8, 9, 11)),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI")
        };

        if (owner is not null) window.Owner = owner;

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(36, 39, 46)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(20, 16, 20, 16)
        };
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
        headerPanel.Children.Add(new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(255, 106, 0)),
            Child = new TextBlock
            {
                Text = GetIcon(image),
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        });
        headerPanel.Children.Add(new StackPanel
        {
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = caption, FontSize = 15, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = GetTypeLabel(image, buttons), FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(133, 138, 148)), Margin = new Thickness(0, 2, 0, 0) }
            }
        });
        header.Child = headerPanel;
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var contentBorder = new Border
        {
            Padding = new Thickness(22, 20, 22, 12),
            Background = new SolidColorBrush(Color.FromRgb(8, 9, 11))
        };
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        scroll.Content = new TextBlock
        {
            Text = message,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(218, 220, 224)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21
        };
        contentBorder.Child = scroll;
        Grid.SetRow(contentBorder, 1);
        root.Children.Add(contentBorder);

        var footer = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(13, 14, 17)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(36, 39, 46)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(20, 14, 20, 14)
        };
        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        System.Windows.MessageBoxResult result = System.Windows.MessageBoxResult.OK;

        void AddButton(string text, System.Windows.MessageBoxResult value, bool primary)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 96,
                Height = 38,
                Margin = new Thickness(8, 0, 0, 0),
                Padding = new Thickness(18, 0, 18, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = primary ? new SolidColorBrush(Color.FromRgb(255, 106, 0)) : new SolidColorBrush(Color.FromRgb(30, 33, 39)),
                BorderBrush = primary ? new SolidColorBrush(Color.FromRgb(255, 106, 0)) : new SolidColorBrush(Color.FromRgb(55, 59, 68)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            button.Click += (_, _) =>
            {
                result = value;
                window.DialogResult = true;
            };
            buttonsPanel.Children.Add(button);
        }

        switch (buttons)
        {
            case System.Windows.MessageBoxButton.YesNo:
                AddButton("NE", System.Windows.MessageBoxResult.No, false);
                AddButton("ANO", System.Windows.MessageBoxResult.Yes, true);
                break;
            case System.Windows.MessageBoxButton.OKCancel:
                AddButton("Zrušit", System.Windows.MessageBoxResult.Cancel, false);
                AddButton("OK", System.Windows.MessageBoxResult.OK, true);
                break;
            case System.Windows.MessageBoxButton.YesNoCancel:
                AddButton("Zrušit", System.Windows.MessageBoxResult.Cancel, false);
                AddButton("NE", System.Windows.MessageBoxResult.No, false);
                AddButton("ANO", System.Windows.MessageBoxResult.Yes, true);
                break;
            default:
                AddButton("OK", System.Windows.MessageBoxResult.OK, true);
                break;
        }

        footer.Child = buttonsPanel;
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        window.Content = root;
        window.Closed += (_, _) => { if (window.DialogResult != true) result = System.Windows.MessageBoxResult.Cancel; };
        window.Loaded += (_, _) =>
        {
            window.Activate();
            Keyboard.Focus(buttonsPanel.Children[buttonsPanel.Children.Count - 1]);
        };

        window.ShowDialog();
        return result;
    }

    private static string GetIcon(System.Windows.MessageBoxImage image) => image switch
    {
        System.Windows.MessageBoxImage.Warning => "!",
        System.Windows.MessageBoxImage.Error => "×",
        System.Windows.MessageBoxImage.Question => "?",
        System.Windows.MessageBoxImage.Information => "i",
        _ => "G"
    };

    private static string GetTypeLabel(System.Windows.MessageBoxImage image, System.Windows.MessageBoxButton buttons)
    {
        if (buttons == System.Windows.MessageBoxButton.YesNo || buttons == System.Windows.MessageBoxButton.YesNoCancel)
            return "ACTION REQUIRED";
        return image switch
        {
            System.Windows.MessageBoxImage.Warning => "WARNING",
            System.Windows.MessageBoxImage.Error => "ERROR",
            _ => "INFORMATION"
        };
    }
}
