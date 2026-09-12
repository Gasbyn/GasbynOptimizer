
    private static string FormatStatus(TweakStatus status) => status switch
    {
        TweakStatus.Enabled => "ENABLED",
        TweakStatus.Disabled => "DISABLED",
        TweakStatus.NotConfigured => "NOT CONFIGURED",
        _ => "UNKNOWN"
    };

    private string GetSystemDriveStatus()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\");
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            var totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
            return $"{freeGb:0.0} GB free / {totalGb:0.0} GB";
        }
        catch { return "Drive information unavailable"; }
    }

    private void AddPageHeader(string page)
    {
        var header = new TextBlock
        {
            Text = page switch
            {
                "Tweaks" => "OPTIMIZATION MODULES",
                "Cleanup" => "CLEANUP CENTER",
                "Deep Clean" => "DEEP CLEAN CENTER",
                "Apps Manager" => "APPLICATION CENTER",
                "Gaming" => "GAMING CENTER",
                "Network" => "NETWORK CENTER",
                "Health Check" => "SYSTEM HEALTH",
                "Benchmark" => "BENCHMARK CENTER",
                "Backup" => "BACKUP CENTER",
                "Startup" => "STARTUP MANAGER",
                "PC Info" => "SYSTEM INFORMATION",
                "Settings" => "APPLICATION SETTINGS",
                _ => "GASBYN OPTIMIZER"
            },
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("Orange"),
            Margin = new Thickness(0, 0, 0, 14)
        };
        SecondaryContent.Children.Add(header);
    }

    private void AddTweakCard(string title, string description, string tier, string actionText)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Border"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12),
            Effect = (System.Windows.Media.Effects.DropShadowEffect)FindResource("CardShadow")
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap });
        stack.Children.Add(new TextBlock { Text = tier, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = tier == "PREMIUM" ? (Brush)FindResource("Orange") : (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 10, 0, 0) });
        Grid.SetColumn(stack, 0); grid.Children.Add(stack);
        var button = new Button { Content = actionText, Style = (Style)FindResource("ActionButton"), VerticalAlignment = VerticalAlignment.Center, Tag = title };
        button.Click += actionText == "Manage Game Mode" ? GameModeAction_Click : PlaceholderAction_Click;
        Grid.SetColumn(button, 1); grid.Children.Add(button);
        border.Child = grid; SecondaryContent.Children.Add(border);
    }

    private void AddInfoCard(string title, string description, string actionText)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("Panel"), BorderBrush = (Brush)FindResource("Border"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 12),
            Effect = (System.Windows.Media.Effects.DropShadowEffect)FindResource("CardShadow")
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)FindResource("Orange") });
        stack.Children.Add(new TextBlock { Text = description, FontSize = 13, Foreground = (Brush)FindResource("TextMuted"), Margin = new Thickness(0, 6, 20, 0), TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(stack, 0); grid.Children.Add(stack);

        // Info-card actions are routed centrally through ActionRouting.cs.
        // Use the action text as Tag so the router receives the actual command.
        // Do not attach PlaceholderAction_Click here, otherwise it would fire after the routed action.
        var button = new Button
        {
            Content = actionText,
            Style = (Style)FindResource("NavButton"),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = actionText
        };
        Grid.SetColumn(button, 1); grid.Children.Add(button);
        border.Child = grid; SecondaryContent.Children.Add(border);
    }

    private void GameModeAction_Click(object sender, RoutedEventArgs e)
    {
        var tweak = _tweakEngine.Get("game-mode");
        if (tweak is null)
        {
            MessageBox.Show("Game Mode tweak nebyl nalezen.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var status = tweak.GetStatus(new TweakContext());
        var action = status == TweakStatus.Enabled ? "restore" : "enable";
        var question = action == "enable" ? "Zapnout Windows Game Mode? Původní nastavení se před změnou uloží do backupu." : "Game Mode je už zapnutý. Chceš obnovit původní nastavení z backupu?";
        if (MessageBox.Show(question, "Gasbyn Optimizer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var context = new TweakContext();
        var result = action == "enable" ? tweak.Apply(context) : tweak.Restore(context);
        MessageBox.Show(result.Message, "Gasbyn Optimizer", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        ShowPage("Tweaks");
    }

    private void PlaceholderAction_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Tahle funkce má připravené UI. Skutečné systémové změny budeme přidávat postupně do bezpečného engine se zálohou, ověřením a možností obnovy.", "Gasbyn Optimizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void QuickOptimize_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Quick Optimize zatím nic nemění. Nejprve vytvoříme scan + backup a následně budeme aplikovat pouze bezpečné, ověřitelné změny.", "Quick Optimize", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void QuickCleanup_Click(object sender, RoutedEventArgs e) => ShowPage("Cleanup");