using GasbynOptimizer.Cleanup;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void OpenDeepClean()
    {
        var window = new DeepCleanWindow { Owner = this };
        window.ShowDialog();
    }
}
