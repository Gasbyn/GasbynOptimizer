using System.Windows;
using GasbynOptimizer.Startup;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void OpenStartupManager()
    {
        var view = new StartupManagerWindow();
        view.CloseRequested += (_, _) => ShowPage("Startup");
        SecondaryContent.Children.Clear();
        AddPageHeader("Startup");
        SecondaryContent.Children.Add(view);
    }
}
