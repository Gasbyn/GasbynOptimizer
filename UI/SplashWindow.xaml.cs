using System.Windows;

namespace GasbynOptimizer.UI;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        Spinner.Data = Icons.Get("Sync");
        Icons.Spin(Spinner, true);
    }

    public void SetStatus(string text) => Status.Text = text;

    public void SetUpdating(string label)
    {
        Status.Text = "Aktualizuje se Gasbyn Optimizer…";
        Counter.Text = "Verze " + label;
        Bar.Value = 0;
    }

    public void SetFraction(double fraction)
    {
        Bar.Value = Math.Clamp(fraction, 0, 1);
    }

    public void SetProgress(int done, int total)
    {
        Bar.Value = total == 0 ? 0 : (double)done / total;
        Counter.Text = done + " / " + total;
    }
}
