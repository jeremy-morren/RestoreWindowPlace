using System.Globalization;
using System.Text;
using System.Windows;

namespace RestoreWindowPosition.Sample;

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();

        // Registered with a predicate, which gets the last word on whether the saved
        // placement is used. Here it declines a position that no longer lands on a monitor,
        // so a window saved on a screen that has since been unplugged opens where WPF put it
        // instead of somewhere the user cannot reach.
        App.Current.Placer.Register(this, (_, saved) => MonitorLayout.GetCurrent().IsVisible(saved.Bounds));

        Loaded += (_, _) => Layout.Text = DescribeMonitors();
    }

    private void OpenTool_Click(object sender, RoutedEventArgs e) => new ToolWindow { Owner = this }.Show();

    private void SaveNow_Click(object sender, RoutedEventArgs e)
    {
        App.Current.Placer.Store(this);
        App.Current.Placer.Save();
    }

    private static string DescribeMonitors()
    {
        var layout = MonitorLayout.GetCurrent();

        var text = new StringBuilder();
        text.AppendLine("Monitor arrangement key: " + layout.Key.ToString("x8", CultureInfo.InvariantCulture));
        text.AppendLine();

        foreach (var monitor in layout.Monitors)
        {
            text.AppendLine(monitor.ToString());
            text.AppendLine("    work area " + monitor.WorkArea);
        }

        return text.ToString();
    }
}
