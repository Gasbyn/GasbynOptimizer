using System.Windows;

namespace GasbynOptimizer.UI;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Icons.Spin(Spinner, true);
    }

    public void SetStatus(string text) => Status.Text = text;

    public void SetUpdating(string label)
    {
        Status.Text = "Aktualizuje se Gasbyn Optimizer…";
        Counter.Text = "Verze " + label;
        Motion.SetBar(Bar, 0);
    }

    public void SetFraction(double fraction)
    {
        Motion.SetBar(Bar, fraction);
    }

    public void SetProgress(int done, int total)
    {
        Motion.SetBar(Bar, total == 0 ? 0 : (double)done / total);
        Counter.Text = done + " / " + total;
    }
}
