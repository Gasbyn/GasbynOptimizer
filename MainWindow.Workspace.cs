using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
using GasbynOptimizer.Services;

namespace GasbynOptimizer;

// Temporary stub - full Workspace body restored in next commit via Apply partial.
public partial class MainWindow
{
    private static readonly string WorkspaceDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GasbynOptimizer");
    private static readonly string WorkspaceStatePath = Path.Combine(WorkspaceDataDirectory, "optimization-state.json");
    private static readonly string WorkspaceBenchmarkPath = Path.Combine(WorkspaceDataDirectory, "fps-tests.json");
    private static readonly string WorkspacePresentMonPath = Path.Combine(WorkspaceDataDirectory, "presentmon-path.txt");

    private readonly Dictionary<string, ToggleButton> _workspaceSwitches = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _workspaceDriftedTweaks = new();
    private readonly Dictionary<string, TextBlock> _workspaceSwitchStatuses = new(StringComparer.OrdinalIgnoreCase);
    private bool _loadingWorkspaceSwitches;
    private bool _workspaceTweakBusy;
}
