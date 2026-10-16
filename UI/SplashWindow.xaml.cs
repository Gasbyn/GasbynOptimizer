using System.Windows;

namespace GasbynOptimizer.UI;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void SetProgress(int done, int total)
    {
        Bar.Value = total == 0 ? 0 : (double)done / total;
        Counter.Text = done + " / " + total;
    }
}
