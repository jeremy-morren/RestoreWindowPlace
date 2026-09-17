using System.Windows;

namespace RestoreWindowPosition.Sample;

public partial class App : Application
{
    /// <summary>
    /// One store for the whole application. Windows that want a placement of their own can
    /// take it from here, whether through <see cref="Placer"/> or through the
    /// <c>RestoreWindowPosition</c> extension method.
    /// </summary>
    public static IWindowPositionStore Store { get; } =
        new FileWindowPositionStore("placement.config", perMonitorLayout: true);

    public WindowPlacer Placer { get; } = new(Store)
    {
        // Save a snapped window where it is snapped, rather than where it would spring back to.
        IsSavingSnappedPositionEnabled = true,
    };

    public static new App Current => (App)Application.Current;

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        Placer.Save();
    }
}
