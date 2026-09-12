using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GasbynOptimizer.Models;
using GasbynOptimizer.Services;

namespace GasbynOptimizer.App.Apps;

public partial class AppsManagerWindow : Window
{
    private readonly ApplicationInventory _inventory = new();
    private readonly ApplicationUninstaller _uninstaller = new();
    private List<InstalledApplication> _apps = new();
    private InstalledApplication? _selectedApp;

    public AppsManagerWindow()
    {
        InitializeComponent();
        ScanApps();
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => ScanApps();

    private void ScanApps()
    {
        try
        {
            _apps = _inventory.Scan().ToList();
            RefreshList();
            CountText.Text = $"{_apps.Count} apps";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Skenování aplikací selhalo: {ex.Message}", "Apps Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    private void RefreshList()
    {
        var query = SearchBox?.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _apps
            : _apps.Where(a => Matches(a, query)).ToList();

        AppsList.Items.Clear();
        foreach (var app in filtered)
        {
            var panel = new Grid();
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            panel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = app.Name, FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = $"{app.Publisher}  •  {app.Version}", FontSize = 10, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = app.EstimatedSizeText, FontSize = 9, Foreground = (Brush)FindResource("TextDim"), Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetColumn(stack, 0);
            panel.Children.Add(stack);

            var badge = new TextBlock
            {
                Text = app.IsSystemComponent ? "SYSTEM" : "APP",
                Foreground = app.IsSystemComponent ? (Brush)FindResource("TextDim") : (Brush)FindResource("Orange"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(badge, 1);
            panel.Children.Add(badge);

            AppsList.Items.Add(new ListBoxItem { Content = panel, Tag = app });
        }
    }

    private static bool Matches(InstalledApplication app, string query)
    {
        return app.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               app.InstallLocation.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void AppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AppsList.SelectedItem is not ListBoxItem item || item.Tag is not InstalledApplication app)
            return;

        _selectedApp = app;
        ShowDetails(app);
        LeftoversList.Items.Clear();
        RemoveSelectedButton.Visibility = Visibility.Collapsed;
        DeepStatus.Text = "Deep Scan nic nemaže. Výsledky se nejdříve zobrazí k ručnímu výběru.";
    }

    private void ShowDetails(InstalledApplication app)
    {
        DetailName.Text = app.Name;
        DetailMeta.Text = $"Publisher: {app.Publisher}  •  Version: {app.Version}  •  Size: {app.EstimatedSizeText}";

        WhatText.Text = BuildWhatItDoes(app);
        ImpactText.Text = BuildImpact(app);
        InstallText.Text =
            $"Install location: {(string.IsNullOrWhiteSpace(app.InstallLocation) ? "neuvedena" : app.InstallLocation)}\n" +
            $"Official uninstaller: {(string.IsNullOrWhiteSpace(app.UninstallCommand) ? "nenalezen" : "nalezen")}\n" +
            $"System component: {(app.IsSystemComponent ? "ano — opatrně" : "ne / neuvedeno")}";
    }

    private static string BuildWhatItDoes(InstalledApplication app)
    {
        var name = app.Name;
        var publisher = app.Publisher;
        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            return "Pravděpodobně komponenta NVIDIA ovladače nebo doprovodného softwaru. Může souviset s GPU, ovladači nebo NVIDIA funkcemi.";
        if (name.Contains("Microsoft Visual C++", StringComparison.OrdinalIgnoreCase))
            return "Microsoft runtime knihovna používaná jinými programy. Není vhodné ji odinstalovávat jen kvůli úklidu.";
        if (name.Contains(".NET", StringComparison.OrdinalIgnoreCase) || name.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
            return "Microsoft .NET runtime/framework. Jiné aplikace na něm mohou být závislé.";
        if (name.Contains("Xbox", StringComparison.OrdinalIgnoreCase) || name.Contains("Gaming Services", StringComparison.OrdinalIgnoreCase))
            return "Komponenta spojená s Xbox/Gaming Services ve Windows. Může ovlivnit některé hry a herní služby.";
        if (!string.Equals(publisher, "Unknown", StringComparison.OrdinalIgnoreCase))
            return $"Registrovaná aplikace od vydavatele {publisher}. Lokální checker neumí spolehlivě určit kompletní účel bez vendor dokumentace, proto tento popis ber jako heuristiku.";
        return "Účel nelze bezpečně určit pouze z uninstall záznamu. Před odinstalací doporučujeme ověřit aplikaci podle názvu a vydavatele.";
    }

    private static string BuildImpact(InstalledApplication app)
    {
        if (app.IsSystemComponent)
            return "Protože je položka označená jako SystemComponent, její odstranění může ovlivnit Windows nebo jiné komponenty. Deep Uninstall ji proto nemaže automaticky.";
        if (app.Name.Contains("Visual C++", StringComparison.OrdinalIgnoreCase) || app.Name.Contains(".NET", StringComparison.OrdinalIgnoreCase))
            return "Jiné aplikace mohou přestat fungovat, pokud tento runtime potřebují. Zachovej ji, pokud si nejsi jistý.";
        if (app.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            return "Odstranění může ovlivnit NVIDIA ovladače, ovládací panel nebo herní funkce. Zkontroluj, zda nejde o ovladačovou komponentu.";
        if (app.Name.Contains("Xbox", StringComparison.OrdinalIgnoreCase) || app.Name.Contains("Gaming Services", StringComparison.OrdinalIgnoreCase))
            return "Mohou přestat fungovat Xbox/Gaming Services funkce a některé hry závislé na těchto službách.";
        return "Checker z uninstall registru neumí garantovat všechny závislosti. Před odstraněním zkontroluj, zda aplikaci nepoužíváš a zda na ní nejsou navázané jiné programy.";
    }

    private void OfficialUninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedApp is null)
        {
            MessageBox.Show("Nejdřív vyber aplikaci.", "Apps Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_selectedApp.IsSystemComponent)
        {
            var warning = MessageBox.Show(
                "Tato položka je označená jako systémová komponenta. Opravdu chceš spustit její oficiální odinstalátor?\n\nGasbyn Optimizer ji nebude odstraňovat násilím.",
                "System component",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (warning != MessageBoxResult.Yes)
                return;
        }

        var result = MessageBox.Show(
            $"Spustit oficiální odinstalaci pro:\n\n{_selectedApp.Name}\n{_selectedApp.Publisher}\n\nNejdřív použijeme oficiální uninstall. Deep Scan bude až další krok.",
            "Potvrdit odinstalaci",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
            return;

        if (_uninstaller.LaunchOfficialUninstaller(_selectedApp, out var message))
        {
            DeepStatus.Text = message;
            return;
        }

        MessageBox.Show(message, "Apps Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void DeepScan_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedApp is null)
        {
            MessageBox.Show("Nejdřív vyber aplikaci.", "Apps Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var candidates = _uninstaller.ScanLeftovers(_selectedApp);
        LeftoversList.Items.Clear();

        foreach (var candidate in candidates)
        {
            var check = new CheckBox
            {
                Content = $"{candidate.Type}\n{candidate.Path}\nRiziko: {candidate.Risk}",
                Tag = candidate,
                Margin = new Thickness(4, 5, 4, 5),
                Foreground = (Brush)FindResource("TextMuted")
            };
            LeftoversList.Items.Add(check);
        }

        if (candidates.Count == 0)
        {
            DeepStatus.Text = "Deep Scan nenašel bezpečně identifikovatelné leftovery.";
            RemoveSelectedButton.Visibility = Visibility.Collapsed;
            return;
        }

        DeepStatus.Text = $"Nalezeno {candidates.Count} kandidátů. Nic nebylo odstraněno. Vyber pouze položky, o kterých víš, že patří aplikaci.";
        RemoveSelectedButton.Visibility = Visibility.Visible;
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = LeftoversList.Items.OfType<CheckBox>()
            .Where(x => x.IsChecked == true && x.Tag is UninstallCandidate)
            .Select(x => (UninstallCandidate)x.Tag)
            .ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Není vybraná žádná položka.", "Deep Uninstall", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Odstranit ručně vybrané položky: {selected.Count}?\n\nDeep Uninstall nikdy nemaže nic bez tohoto potvrzení.",
            "Potvrdit Deep Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        var removed = 0;
        var failed = 0;
        foreach (var candidate in selected)
        {
            if (_uninstaller.RemoveCandidate(candidate, out _))
                removed++;
            else
                failed++;
        }

        DeepStatus.Text = $"Dokončeno: odstraněno {removed}, nepodařilo se odstranit {failed}. Doporučeno je provést nový Deep Scan.";
        DeepScan_Click(sender, e);
        ScanApps();
    }
}
