using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace GasbynOptimizer;

/// <summary>
/// Branded replacement for the standard WPF MessageBox used by optimizer modules.
/// Keeps the existing MessageBox.Show call sites while giving every result dialog
/// a consistent Gasbyn look and readable layout.
/// </summary>
internal static class MessageBox
{
    public static MessageBoxResult Show(string message, string caption = "Gasbyn Optimizer",
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage image = MessageBoxImage.None)
    {
        var owner = Application.Current?.Windows.Count > 0 ? Application.Current.Windows[0] : null;
        var dialog = new ModernDialogWindow(message ?? string.Empty, caption ?? "Gasbyn Optimizer", buttons, image)
        {
            Owner = owner,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        };

        dialog.ShowDialog();
        return dialog.Result;
    }
}

internal sealed class ModernDialogWindow : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

    public ModernDialogWindow(string message, string caption, MessageBoxButton buttons, MessageBoxImage image)
    {
        Title = caption;
        Width = 620;
        MinWidth = 480;
        MaxWidth = 900;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 720;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;

        var root = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(18, 18, 21)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 62)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(1)
        };

        root.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 1) DragMove();
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { Margin = new Thickness(22, 18, 18, 14) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var accent = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(255, 110, 20)),
            VerticalAlignment = VerticalAlignment.Top
        };
        accent.Child = new TextBlock
        {
            Text = GetIcon(image),
            FontSize = 21,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };
        Grid.SetColumn(accent, 0);
        header.Children.Add(accent);

        var titlePanel = new StackPanel { Margin = new Thickness(14, 0, 12, 0) };
        titlePanel.Children.Add(new TextBlock
        {
            Text = caption,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = GetSubtitle(image),
            Margin = new Thickness(0, 3, 0, 0),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(145, 145, 155))
        });
        Grid.SetColumn(titlePanel, 1);
        header.Children.Add(titlePanel);

        var close = new Button
        {
            Content = "×",
            Width = 34,
            Height = 34,
            FontSize = 22,
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 168)),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "Zavřít"
        };
        close.Click += (_, _) => CloseWith(MessageBoxResult.Cancel);
        Grid.SetColumn(close, 2);
        header.Children.Add(close);
        Grid.SetRow(header, 0);
        layout.Children.Add(header);

        var bodyBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(25, 25, 29)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(43, 43, 49)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Margin = new Thickness(22, 0, 22, 18),
            Padding = new Thickness(16, 14, 16, 14)
        };
        var scroll = new ScrollViewer
        {
            MaxHeight = 450,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        scroll.Content = new TextBlock
        {
            Text = message,
            FontSize = 13,
            LineHeight = 21,
            Foreground = new SolidColorBrush(Color.FromRgb(225, 225, 230)),
            TextWrapping = TextWrapping.Wrap
        };
        bodyBorder.Child = scroll;
        Grid.SetRow(bodyBorder, 1);
        layout.Children.Add(bodyBorder);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(22, 0, 22, 20)
        };
        AddButtons(footer, buttons);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);

        root.Child = layout;
        Content = root;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) CloseWith(MessageBoxResult.Cancel);
        };
    }

    private void AddButtons(Panel panel, MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                panel.Children.Add(CreateButton("OK", MessageBoxResult.OK, true));
                break;
            case MessageBoxButton.YesNo:
                panel.Children.Add(CreateButton("Ne", MessageBoxResult.No, false));
                panel.Children.Add(CreateButton("Ano", MessageBoxResult.Yes, true));
                break;
            case MessageBoxButton.OKCancel:
                panel.Children.Add(CreateButton("Zrušit", MessageBoxResult.Cancel, false));
                panel.Children.Add(CreateButton("OK", MessageBoxResult.OK, true));
                break;
            case MessageBoxButton.YesNoCancel:
                panel.Children.Add(CreateButton("Zrušit", MessageBoxResult.Cancel, false));
                panel.Children.Add(CreateButton("Ne", MessageBoxResult.No, false));
                panel.Children.Add(CreateButton("Ano", MessageBoxResult.Yes, true));
                break;
        }
    }

    private Button CreateButton(string text, MessageBoxResult result, bool primary)
    {
        var button = new Button
        {
            Content = text,
            MinWidth = 92,
            Height = 38,
            Margin = new Thickness(8, 0, 0, 0),
            Padding = new Thickness(18, 0, 18, 0),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Background = primary
                ? new SolidColorBrush(Color.FromRgb(255, 110, 20))
                : new SolidColorBrush(Color.FromRgb(48, 48, 55)),
            BorderBrush = primary
                ? new SolidColorBrush(Color.FromRgb(255, 130, 45))
                : new SolidColorBrush(Color.FromRgb(70, 70, 78)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand
        };
        button.Click += (_, _) => CloseWith(result);
        return button;
    }

    private void CloseWith(MessageBoxResult result)
    {
        Result = result;
        Close();
    }

    private static string GetIcon(MessageBoxImage image) => image switch
    {
        MessageBoxImage.Warning => "!",
        MessageBoxImage.Error => "×",
        MessageBoxImage.Question => "?",
        _ => "✓"
    };

    private static string GetSubtitle(MessageBoxImage image) => image switch
    {
        MessageBoxImage.Warning => "Vyžaduje pozornost",
        MessageBoxImage.Error => "Akce se nezdařila",
        MessageBoxImage.Question => "Potvrzení akce",
        _ => "Informace o systému"
    };
}
