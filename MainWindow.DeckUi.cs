using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GasbynOptimizer.Services;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private bool IsWorkspaceTweakIncompatible(string id)
    {
        var hw = _hardwareInventory;
        if (string.Equals(id, "gpu-preference", StringComparison.OrdinalIgnoreCase))
        {
            var gpu = hw?.Gpu;
            return string.IsNullOrWhiteSpace(gpu) ||
                   gpu.Contains("GPU nedostupné", StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(id, "fullscreen-optimizations", StringComparison.OrdinalIgnoreCase))
            return !File.Exists(FindFortniteExecutablePath());

        if (string.Equals(id, "amd-gpu-profile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "amd-anti-lag-hint", StringComparison.OrdinalIgnoreCase))
            return hw is null || !hw.HasAmdGpu;

        if (string.Equals(id, "nvidia-low-latency", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(id, "nvidia-power-max", StringComparison.OrdinalIgnoreCase))
            return hw is null || !hw.HasNvidiaGpu;

        return false;
    }

    private string WorkspaceIncompatibilityReason(string id)
    {
        var hw = _hardwareInventory;
        if (id.StartsWith("amd", StringComparison.OrdinalIgnoreCase))
            return "Vyžaduje AMD Radeon GPU (detekováno: " + (hw?.Gpu ?? "neznámé") + ")";
        if (id.StartsWith("nvidia", StringComparison.OrdinalIgnoreCase))
            return "Vyžaduje NVIDIA GPU (detekováno: " + (hw?.Gpu ?? "neznámé") + ")";
        if (string.Equals(id, "fullscreen-optimizations", StringComparison.OrdinalIgnoreCase))
            return "Fortnite nebyl nalezen na tomto PC";
        if (string.Equals(id, "gpu-preference", StringComparison.OrdinalIgnoreCase))
            return "GPU nebyla spolehlivě detekována";
        return "Tato optimalizace neodpovídá tvé hardwarové konfiguraci";
    }

    private void AddAmdCompatibilityInfo(GasbynOptimizer.Services.HardwareInventory? hardware, bool advanced)
    {
        var warning = hardware is null
            ? "INFO · GPU nebyla ověřena, proto zůstává ovladačový modul nedostupný."
            : hardware.GetCompatibilityNote("amd-gpu-profile", advanced);
        if (hardware?.HasAmdGpu == true)
            warning = "INFO · AMD Radeon GPU detekována, ale zatím není nakonfigurována ověřená vratná změna ovladače; nic se nepřepíše.";

        var card = WorkspaceCard();
        card.Opacity = advanced ? 0.95 : 0.58;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var copy = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        copy.Children.Add(WorkspaceHeading((!advanced ? "🔒 " : (hardware?.HasAmdGpu == true ? "" : "⚠️ ")) + "AMD RADEON-ONLY GPU TUNING"));
        copy.Children.Add(WorkspaceBody("Tento modul je indikátor kompatibility. Nepoužívá neověřené ovladačové, napěťové ani taktovací zásahy."));
        var status = WorkspaceBody(warning);
        status.Margin = new Thickness(0, 8, 0, 0);
        copy.Children.Add(status);
        Grid.SetColumn(copy, 0);
        root.Children.Add(copy);

        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("SwitchToggleButton"),
            IsChecked = false,
            IsEnabled = advanced,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = advanced
                ? "Pokročilý režim: při zapnutí se zeptá na potvrzení."
                : "🔒 Zapni Pokročilý režim v Settings."
        };
        toggle.Checked += (_, _) =>
        {
            if (!advanced)
            {
                toggle.IsChecked = false;
                return;
            }
            var ok = MessageBox.Show(
                "AMD Radeon-only GPU tuning není plně implementovaný jako agresivní ovladačový zápis." +
                Environment.NewLine + Environment.NewLine +
                WorkspaceIncompatibilityReason("amd-gpu-profile") +
                Environment.NewLine + Environment.NewLine +
                "Opravdu chceš označit preferenci? Systémové ovladače se tím nemění.",
                "Nepodporovaná / omezená optimalizace",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (ok != MessageBoxResult.Yes)
                toggle.IsChecked = false;
        };
        Grid.SetColumn(toggle, 1);
        root.Children.Add(toggle);
        card.Child = root;
        SecondaryContent.Children.Add(card);
    }

    private WrapPanel? _tweakDeckHost;
    private TextBox? _tweakSearchBox;
    private string _tweakFilter = "all";
    private readonly List<(string Id, Border Card, string Title, string Description, string[] Badges, bool Incompatible)> _tweakDeckIndex = new();

    private void EnsureTweakDeckHost(bool gamingOnly)
    {
        _tweakDeckIndex.Clear();
        _tweakDeckHost = null;
        _tweakSearchBox = null;
        _tweakFilter = "all";

        var toolbar = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });

        var back = new Button
        {
            Content = "← Zpět",
            Style = (Style)FindResource("NavButton"),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 10, 0)
        };
        back.Click += (_, _) => NavigateBack();
        Grid.SetColumn(back, 0);
        top.Children.Add(back);

        var title = new TextBlock
        {
            Text = gamingOnly ? "Gaming tweaks" : "System Tweaks",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("TextMuted")
        };
        Grid.SetColumn(title, 1);
        top.Children.Add(title);

        _tweakSearchBox = new TextBox
        {
            Height = 36,
            Padding = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(Color.FromRgb(18, 20, 25)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(45, 48, 56)),
            BorderThickness = new Thickness(1),
            ToolTip = "Hledat tweak…"
        };
        _tweakSearchBox.TextChanged += (_, _) => ApplyTweakDeckFilter();
        Grid.SetColumn(_tweakSearchBox, 2);
        top.Children.Add(_tweakSearchBox);
        toolbar.Children.Add(top);

        var filters = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        void AddFilter(string id, string label)
        {
            var btn = new Button
            {
                Content = label,
                Tag = id,
                Margin = new Thickness(0, 0, 8, 6),
                Padding = new Thickness(14, 7, 14, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                Background = id == "all" ? (Brush)FindResource("OrangeSoft") : new SolidColorBrush(Color.FromRgb(22, 24, 30)),
                Foreground = id == "all" ? (Brush)FindResource("OrangeLight") : (Brush)FindResource("TextMuted"),
                BorderBrush = id == "all" ? new SolidColorBrush(Color.FromRgb(74, 40, 20)) : new SolidColorBrush(Color.FromRgb(45, 48, 56))
            };
            btn.Click += (_, _) =>
            {
                _tweakFilter = id;
                foreach (Button b in filters.Children.OfType<Button>())
                {
                    var on = string.Equals(b.Tag as string, id, StringComparison.OrdinalIgnoreCase);
                    b.Background = on ? (Brush)FindResource("OrangeSoft") : new SolidColorBrush(Color.FromRgb(22, 24, 30));
                    b.Foreground = on ? (Brush)FindResource("OrangeLight") : (Brush)FindResource("TextMuted");
                    b.BorderBrush = on ? new SolidColorBrush(Color.FromRgb(74, 40, 20)) : new SolidColorBrush(Color.FromRgb(45, 48, 56));
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
        AddFilter("unsupported", "Nepodporované");
        toolbar.Children.Add(filters);

        SecondaryContent.Children.Add(toolbar);
        _tweakDeckHost = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        SecondaryContent.Children.Add(_tweakDeckHost);
    }

    private void ApplyTweakDeckFilter()
    {
        var q = (_tweakSearchBox?.Text ?? string.Empty).Trim();
        foreach (var item in _tweakDeckIndex)
        {
            var textMatch = string.IsNullOrEmpty(q) ||
                            item.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            item.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                            item.Id.Contains(q, StringComparison.OrdinalIgnoreCase);
            var filterMatch = _tweakFilter switch
            {
                "recommended" => item.Badges.Any(b => b.Contains("DOPORUČENO", StringComparison.OrdinalIgnoreCase)),
                "optional" => item.Badges.Any(b => b.Contains("VOLITELNÉ", StringComparison.OrdinalIgnoreCase) || b.Contains("PODMÍNĚNĚ", StringComparison.OrdinalIgnoreCase)),
                "privacy" => item.Badges.Any(b => b.Contains("SOUKROMÍ", StringComparison.OrdinalIgnoreCase)),
                "gaming" => item.Badges.Any(b => b.Contains("GAMING", StringComparison.OrdinalIgnoreCase) || b.Contains("FPS", StringComparison.OrdinalIgnoreCase) || b.Contains("FORTNITE", StringComparison.OrdinalIgnoreCase)),
                "unsupported" => item.Incompatible,
                _ => true
            };
            item.Card.Visibility = textMatch && filterMatch ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void AddWorkspaceTweakCard(string title, string description, string id, string tooltip, params string[] badges)
    {
        var incompatible = IsWorkspaceTweakIncompatible(id);
        var advanced = OptimizerPreferencesStore.Load().AdvancedRecommendations;
        var locked = incompatible && !advanced;
        object detailForHover = locked
            ? "🔒 Tato optimalizace není podporována na zjištěné konfiguraci. Zapni Pokročilý režim v Settings a při zapnutí potvrď riziko."
            : BuildTweakToolTip(title, description, tooltip);

        var card = new Border
        {
            Background = (Brush)FindResource("Panel"),
            BorderBrush = incompatible
                ? new SolidColorBrush(Color.FromRgb(90, 60, 40))
                : (Brush)FindResource("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 12, 12),
            Width = 268,
            MinHeight = 200,
            ToolTip = detailForHover
        };
        card.Opacity = locked ? 0.55 : 1.0;
        ToolTipService.SetShowDuration(card, 30000);
        ToolTipService.SetInitialShowDelay(card, 350);

        card.MouseEnter += (_, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(91, 54, 30));
        card.MouseLeave += (_, _) => card.BorderBrush = incompatible
            ? new SolidColorBrush(Color.FromRgb(90, 60, 40))
            : (Brush)FindResource("Border");

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock
        {
            Text = (incompatible ? "⚠️ " : "") + title,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            FontFamily = (FontFamily)FindResource("DisplayFont"),
            Foreground = (Brush)FindResource("OrangeLight"),
            TextWrapping = TextWrapping.Wrap
        });
        var toggle = new ToggleButton
        {
            Style = (Style)FindResource("SwitchToggleButton"),
            IsChecked = false,
            IsEnabled = !locked,
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = locked
                ? "🔒 Nepodporováno. Zapni Pokročilý režim v Settings."
                : incompatible
                    ? "Nepodporováno na tomto HW — při zapnutí se zeptá na potvrzení."
                    : null
        };
        Grid.SetColumn(toggle, 1);
        header.Children.Add(toggle);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        body.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextMuted"),
            TextWrapping = TextWrapping.Wrap
        });
        if (incompatible)
        {
            body.Children.Add(new TextBlock
            {
                Text = "Nepodporováno · " + WorkspaceIncompatibilityReason(id),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 154, 82)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
        var badgePanel = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var badge in badges.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
            badgePanel.Children.Add(CreateTweakBadge(badge));
        if (incompatible)
            badgePanel.Children.Add(CreateTweakBadge(locked ? "ZAMČENO" : "NEPODPOROVÁNO"));
        if (badgePanel.Children.Count > 0)
            body.Children.Add(badgePanel);
        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var status = new TextBlock
        {
            Text = "Načítám stav…",
            FontSize = 10,
            Foreground = (Brush)FindResource("TextDim"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };
        _workspaceSwitchStatuses[id] = status;
        Grid.SetRow(status, 2);
        root.Children.Add(status);

        System.Windows.Automation.AutomationProperties.SetName(toggle, title);
        toggle.Checked += async (_, _) =>
        {
            if (_loadingWorkspaceSwitches) return;
            if (incompatible)
            {
                var ok = MessageBox.Show(
                    "Tato optimalizace není podporována na tvé sestavě." +
                    Environment.NewLine + Environment.NewLine +
                    WorkspaceIncompatibilityReason(id) +
                    Environment.NewLine + Environment.NewLine +
                    "Opravdu ji chceš zapnout? Účinek může být nulový nebo nežádoucí.",
                    "Nepodporovaná optimalizace",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (ok != MessageBoxResult.Yes)
                {
                    _loadingWorkspaceSwitches = true;
                    toggle.IsChecked = false;
                    _loadingWorkspaceSwitches = false;
                    return;
                }
            }
            await ChangeWorkspaceTweakAsync(id, true, toggle, status);
        };
        toggle.Unchecked += async (_, _) =>
        {
            if (!_loadingWorkspaceSwitches)
                await ChangeWorkspaceTweakAsync(id, false, toggle, status);
        };
        _workspaceSwitches[id] = toggle;
        card.Child = root;

        if (_tweakDeckHost is not null)
        {
            _tweakDeckHost.Children.Add(card);
            _tweakDeckIndex.Add((id, card, title, description, badges, incompatible));
        }
        else
            SecondaryContent.Children.Add(card);
    }
}
