using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GasbynOptimizer.Services;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void AddSettingsCard()
    {
        var card = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 12)
        };
        var content = new StackPanel();
        content.Children.Add(new TextBlock
        {
            Text = "PREFERENCE & DIAGNOSTICS", FontSize = 13, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Orange"), Margin = new Thickness(0, 0, 0, 6)
        });
        content.Children.Add(new TextBlock
        {
            Text = "Nastavení se ukládá pro další spuštění aplikace. Detailní logy zaznamenají každou optimalizaci, pokus o obnovu, výsledek a jednotlivé registry/powercfg operace.",
            FontSize = 12, Foreground = (Brush)FindResource("TextMuted"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
        });

        var preferences = OptimizerPreferencesStore.Load();
        AddPreferenceRow(content, "Detailní logování optimalizací",
            "Výchozí stav: zapnuto. Logy: " + OptimizerAuditLog.LogPath,
            preferences.LoggingEnabled,
            enabled =>
            {
                preferences.LoggingEnabled = enabled;
                OptimizerPreferencesStore.Save(preferences);
            });

        AddPreferenceRow(content, "Pokročilý režim doporučení",
            "Zobrazí i nedoporučené/nekompatibilní moduly jako méně výraznou informaci. Hardware-nekompatibilní operace zůstávají blokované.",
            preferences.AdvancedRecommendations,
            enabled =>
            {
                preferences.AdvancedRecommendations = enabled;
                OptimizerPreferencesStore.Save(preferences);
            });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var openLogs = new Button
        {
            Content = "Otevřít složku logů", Background = (Brush)FindResource("Orange"),
            Foreground = Brushes.White, BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 9, 14, 9), Cursor = System.Windows.Input.Cursors.Hand
        };
        openLogs.Click += (_, _) =>
        {
            try
            {
                Directory.CreateDirectory(OptimizerAuditLog.LogDirectory);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + OptimizerAuditLog.LogDirectory + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Složku logů se nepodařilo otevřít: " + ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        var openLatest = new Button
        {
            Content = "Otevřít log v Poznámkovém bloku", Background = (Brush)FindResource("Panel"),
            Foreground = (Brush)FindResource("TextMuted"), BorderBrush = (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1), Padding = new Thickness(14, 9, 14, 9),
            Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand
        };
        openLatest.Click += (_, _) =>
        {
            try
            {
                if (!File.Exists(OptimizerAuditLog.LogPath))
                {
                    MessageBox.Show("Zatím není uložený žádný log. Změň některý tweak nebo zapni logování a zkus to znovu.", "Logs", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                Process.Start(new ProcessStartInfo("notepad.exe", "\"" + OptimizerAuditLog.LogPath + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Log se nepodařilo otevřít: " + ex.Message, "Logs", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
        actions.Children.Add(openLogs);
        actions.Children.Add(openLatest);
        content.Children.Add(actions);
        card.Child = content;
        SecondaryContent.Children.Add(card);
    }

    private void AddPreferenceRow(Panel parent, string title, string description, bool enabled, Action<bool> save)
    {
        var row = new Grid { Margin = new Thickness(0, 8, 0, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        text.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold });
        text.Children.Add(new TextBlock
        {
            Text = description, FontSize = 11, Foreground = (Brush)FindResource("TextMuted"),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var toggle = new ToggleButton
        {
            Content = enabled ? "ON" : "OFF", IsChecked = enabled, Width = 70, Height = 32,
            Background = enabled ? (Brush)FindResource("Orange") : (Brush)FindResource("Border"),
            Foreground = Brushes.White, BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold,
            Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
            ToolTip = enabled ? "Kliknutím vypneš tuto možnost." : "Kliknutím zapneš tuto možnost."
        };
        toggle.Checked += (_, _) => Persist(true);
        toggle.Unchecked += (_, _) => Persist(false);
        void Persist(bool value)
        {
            try
            {
                save(value);
                toggle.Content = value ? "ON" : "OFF";
                toggle.Background = value ? (Brush)FindResource("Orange") : (Brush)FindResource("Border");
                toggle.ToolTip = value ? "Kliknutím vypneš tuto možnost." : "Kliknutím zapneš tuto možnost.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nastavení se nepodařilo uložit: " + ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                toggle.IsChecked = !value;
            }
        }
        Grid.SetColumn(toggle, 1);
        row.Children.Add(toggle);
        parent.Children.Add(row);
    }
}
