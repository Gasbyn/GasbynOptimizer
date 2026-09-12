using System.Windows;
using GasbynOptimizer.Startup;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void OpenStartupManager()
    {
        var window = new StartupManagerWindow { Owner = this };
        window.ShowDialog();
    }
}
