using System.Windows;

namespace RestoreWindowPosition.Sample;

public partial class ToolWindow : Window
{
    public ToolWindow()
    {
        InitializeComponent();

        // The whole of this window's placement handling: restore on open, save on close.
        this.RestoreWindowPosition(App.Store);
    }
}
