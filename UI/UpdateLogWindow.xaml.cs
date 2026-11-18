using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GasbynOptimizer.Core;

namespace GasbynOptimizer.UI;

/// <summary>Okno Update log: co je nového v poslední verzi (datum a čas vydání, co se přidalo, upravilo, odebralo).</summary>
public partial class UpdateLogWindow : Window
{
    private readonly AppSettings _settings;
    private static readonly CultureInfo Czech = new("cs-CZ");

    public UpdateLogWindow(AppSettings settings, string version)
    {
        InitializeComponent();
        _settings = settings;
        ReceiveCheck.IsChecked = settings.ShowUpdateLog;
        Build(version);
    }

    /// <summary>Ukáže Update log při prvním spuštění po stažení a po každé aktualizaci (pokud to uživatel nevypnul).</summary>
    internal static void ShowIfNeeded(OptimizerWindow owner)
    {
        try
        {
            var version = CurrentVersion();
            var settings = owner.Settings;
            if (settings.LastSeenVersion == version) return;

            settings.LastSeenVersion = version;
            settings.Save();

            if (!settings.ShowUpdateLog || Changelog.For(version) == null) return;
            new UpdateLogWindow(settings, version) { Owner = owner }.ShowDialog();
            owner.SyncUpdateLogSettings();
        }
        catch (Exception ex)
        {
            Log.Write("Update log se nepodařilo zobrazit: " + ex.Message);
        }
    }

    public static string CurrentVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? "" : v.Major + "." + v.Minor + "." + Math.Max(v.Build, 0) + "." + Math.Max(v.Revision, 0);
    }

    private static string When(DateTime time) =>
        time.ToString("d. M. yyyy", Czech) + " v " + time.ToString("HH:mm", Czech);

    private void Build(string version)
    {
        var detailed = _settings.DetailedUpdateLog;
        var entries = Changelog.Entries.ToList();
        var latest = Changelog.For(version) ?? entries.FirstOrDefault();
        if (latest == null) return;

        TitleText.Text = "Update log";
        Subtitle.Text = "Verze " + latest.Version + "  ·  vydáno " + When(latest.Released);

        AddEntry(latest, detailed, true);

        if (detailed)
        {
            foreach (var older in entries.Where(e => e != latest))
                AddEntry(older, true, false);
        }
        else
        {
            Content.Children.Add(new TextBlock
            {
                Text = "Starší verze a popisy změn zapneš v Nastavení → Podrobný Update log.",
                Foreground = (Brush)FindResource("MutedBrush"), FontSize = 12, Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void AddEntry(ChangeEntry entry, bool detailed, bool isLatest)
    {
        if (!isLatest)
        {
            Content.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("BorderBrush"), Margin = new Thickness(0, 14, 0, 14) });
            Content.Children.Add(new TextBlock { Text = "Verze " + entry.Version, FontSize = 15, FontWeight = FontWeights.Bold });
            Content.Children.Add(new TextBlock
            {
                Text = "Vydáno " + When(entry.Released), FontSize = 12, Margin = new Thickness(0, 2, 0, 6),
                Foreground = (Brush)FindResource("MutedBrush")
            });
        }

        foreach (var (kind, title, color) in new[]
                 {
                     (ChangeKind.Added, "Přidáno", "GreenBrush"),
                     (ChangeKind.Changed, "Upraveno", "AmberBrush"),
                     (ChangeKind.Removed, "Odebráno", "RedBrush")
                 })
        {
            var items = entry.Items.Where(i => i.Kind == kind).ToList();
            if (items.Count == 0) continue;

            Content.Children.Add(new TextBlock
            {
                Text = title.ToUpper(Czech), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource(color),
                Margin = new Thickness(0, isLatest ? 0 : 4, 0, 6)
            });

            foreach (var item in items)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, detailed && item.Detail.Length > 0 ? 2 : 6) };
                row.Children.Add(new TextBlock { Text = "•", Foreground = (Brush)FindResource(color), FontSize = 14, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top });
                row.Children.Add(new TextBlock { Text = item.Title, FontSize = 13.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 });
                Content.Children.Add(row);

                if (detailed && item.Detail.Length > 0)
                    Content.Children.Add(new TextBlock
                    {
                        Text = item.Detail, FontSize = 12.5, Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(16, 0, 0, 8)
                    });
            }
        }
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private void Receive_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowUpdateLog = ReceiveCheck.IsChecked == true;
        _settings.Save();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
