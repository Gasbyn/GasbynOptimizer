using System.Windows;
using GasbynOptimizer.App.Optimization;

namespace GasbynOptimizer;

public partial class MainWindow
{
    private void FullOptimizer_Click(object sender, RoutedEventArgs e)
    {
        var window = new FullOptimizerWindow { Owner = this };
        window.ShowDialog();
    }
}
