using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace GasbynOptimizer.Startup;

public sealed class StartupItem
{
    public string Name { get; init; } = "";
    public string Location { get; init; } = "";
    public string Command { get; init; } = "";
    public string Status { get; init; } = "";
    public string Scope { get; init; } = "";
    public string RegistryName { get; init; } = "";
    public bool CanDisable { get; init; }
    public bool IsDisabledBackup { get; init; }
}

public partial class StartupManagerWindow : UserControl
{
    private const string BackupPath = @"Software\GasbynOptimizer\StartupBackup";
    private readonly List<StartupItem> _items = new();

    public StartupManagerWindow()
    {
        InitializeComponent();
        LoadItems();
    }

    private void LoadItems()
    {
        _items.Clear();
        try
        {
            using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (run != null)
            {
                foreach (var name in run.GetValueNames())
                {
                    var command = run.GetValue(name)?.ToString() ?? "";
                    _items.Add(new StartupItem
                    {
                        Name = name, Location = "HKCU\\...\\Run", Command = command,
                        Status = "Enabled", Scope = "User", RegistryName = name, CanDisable = true
                    });
                }
            }

            using var runOnce = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce");
            if (runOnce != null)
            {
                foreach (var name in runOnce.GetValueNames())
                {
                    var command = runOnce.GetValue(name)?.ToString() ?? "";
                    _items.Add(new StartupItem
                    {
                        Name = name, Location = "HKCU\\...\\RunOnce", Command = command,
                        Status = "One-time", Scope = "User", RegistryName = name, CanDisable = false
                    });
                }
            }

            using var disabled = Registry.CurrentUser.OpenSubKey(BackupPath);
            if (disabled != null)
            {
                foreach (var name in disabled.GetValueNames())
                {
                    var command = disabled.GetValue(name)?.ToString() ?? "";
                    _items.Add(new StartupItem
                    {
                        Name = name, Location = "Gasbyn Backup", Command = command,
                        Status = "Disabled", Scope = "User", RegistryName = name, IsDisabledBackup = true
                    });
                }
            }
        }
        catch { }

        _items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        StartupGrid.ItemsSource = null;
        StartupGrid.ItemsSource = _items;
        SummaryText.Text = $"{_items.Count} položek nalezeno • {_items.Count(x => x.Status == "Enabled")} aktivních • {_items.Count(x => x.Status == "Disabled")} zakázaných";
        FooterText.Text = "Bezpečný režim: pouze HKCU Run položky lze v této verzi měnit.";
    }

    private void DisableSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = StartupGrid.SelectedItems.Cast<StartupItem>().Where(x => x.CanDisable && x.Status == "Enabled").ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Vyber jednu nebo více aktivních uživatelských startup položek.", "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Zakázat {selected.Count} položek?\n\nPřed změnou se jejich příkazy uloží do Gasbyn backupu a lze je obnovit.", "Potvrdit změnu", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var backup = Registry.CurrentUser.CreateSubKey(BackupPath);
            using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (backup == null || run == null) throw new InvalidOperationException("Nelze otevřít uživatelský startup registr.");

            foreach (var item in selected)
            {
                var value = run.GetValue(item.RegistryName);
                if (value == null) continue;
                backup.SetValue(item.RegistryName, value, RegistryValueKind.String);
                run.DeleteValue(item.RegistryName, false);
            }
            LoadItems();
            MessageBox.Show("Vybrané položky byly zakázány a jejich příkazy jsou v backupu.", "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Změna nebyla dokončena:\n\n{ex.Message}", "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RestoreSelected_Click(object sender, RoutedEventArgs e)
    {
        var selected = StartupGrid.SelectedItems.Cast<StartupItem>().Where(x => x.IsDisabledBackup).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show("Vyber zakázanou položku z Gasbyn Backup.", "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Obnovit {selected.Count} položek do HKCU Run?", "Potvrdit restore", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using var backup = Registry.CurrentUser.OpenSubKey(BackupPath, writable: true);
            using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (backup == null || run == null) throw new InvalidOperationException("Nelze otevřít startup registry.");

            foreach (var item in selected)
            {
                var value = backup.GetValue(item.RegistryName);
                if (value == null) continue;
                run.SetValue(item.RegistryName, value, RegistryValueKind.String);
                backup.DeleteValue(item.RegistryName, false);
            }
            LoadItems();
            MessageBox.Show("Položky byly obnoveny.", "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore nebyl dokončen:\n\n{ex.Message}", "Startup Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadItems();
    public event EventHandler? CloseRequested;

    private void Close_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}