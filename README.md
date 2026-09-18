# RestoreWindowPosition

Save and restore WPF window placement.

## Install

```bash
dotnet add package RestoreWindowPosition
```

## Quick start

```csharp
using RestoreWindowPosition;

public partial class MainWindow : Window
{
    private static readonly IWindowPositionStore Store =
        new FileWindowPositionStore("placement.bin", perMonitorLayout: true);

    public MainWindow()
    {
        InitializeComponent();
        this.RestoreWindowPosition(Store);
    }
}
```

- `IWindowPositionStore` supplies the window key through `GetWindowKey(Window)`.
- `FileWindowPositionStore` stores plain payloads, capped at 1 MiB when read to protect
  startup from corrupted or substituted settings files.
- Set `perMonitorLayout: true` to keep separate placements for each monitor arrangement.

## One app-wide placer

```csharp
public partial class App : Application
{
    public WindowPlacer Windows { get; } = new("placement.bin");

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        Windows.Save();
    }
}
```

## Build and test

```bash
dotnet build RestoreWindowPosition.slnx
dotnet test
```

## Notes

- The format is a small binary blob written by `WindowPositionFormat`.
- Parsing is forgiving: unreadable or damaged payloads are treated as empty.
- The library targets `net7.0-windows` and `net472`.
