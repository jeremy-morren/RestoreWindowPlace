# RestoreWindowPosition

Save and restore the size and position of WPF windows.

A window remembers where it was, per monitor arrangement: dock your laptop and windows
return to the big screen, undock it and they return to where you had them on the laptop.

Targets **net7.0-windows** and **net472**. No dependencies, no runtime reflection, and
nothing a trimmer or an ahead-of-time compiler needs to be told about.

```
dotnet add package RestoreWindowPosition
```

## One line per window

```cs
using RestoreWindowPosition;

public partial class MainWindow : Window
{
    private static readonly IWindowPositionStore Store =
        new FileWindowPositionStore("placement.config");

    public MainWindow()
    {
        InitializeComponent();

        this.RestoreWindowPosition(Store);
    }
}
```

The window is restored when it opens, recorded when it closes, and saved on the spot.
Windows are keyed by the name of their type, so each window class gets its own placement and
nothing else has to be wired up.

## Having the last word

Pass a predicate and you decide, with the placement in hand, whether the window actually
moves. The most useful case is refusing a position that has gone off-screen since it was
saved:

```cs
this.RestoreWindowPosition(
    (_, saved) => MonitorLayout.GetCurrent().IsVisible(saved.Bounds),
    Store);
```

The predicate is consulted only when there is a saved placement to apply and the window has a
handle to move — that is, only when the window really is about to be moved.

## An application-wide placer

If you would rather keep one object and save once, use `WindowPlacer`:

```cs
public partial class App : Application
{
    public WindowPlacer Windows { get; } = new("placement.config");

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        Windows.Save();
    }
}
```

```cs
public MainWindow()
{
    InitializeComponent();

    // Keyed by type name...
    ((App)Application.Current).Windows.Register(this);

    // ...or by a key of your own, for several instances of the same window class.
    ((App)Application.Current).Windows.Register(this, "MainWindow");

    // Restore where the window was, but not how big it was.
    ((App)Application.Current).Windows.RegisterPositionOnly(this);
}
```

`WindowPlacer` also exposes `Restore`, `RestorePosition` and `Store` if you want to drive
things yourself, and `IsSavingSnappedPositionEnabled` to choose whether a snapped window is
saved where it is snapped or where it would spring back to.

## Storing placements somewhere else

`IWindowPositionStore` is the whole storage contract:

```cs
public interface IWindowPositionStore
{
    byte[]? Read(uint monitorLayout);
    void Write(uint monitorLayout, ReadOnlySpan<byte> data);   // byte[] on net472
}
```

Both methods receive `MonitorLayout.Key`: a stable 32-bit digest of the monitors currently
attached, derived from their bounds and work areas in virtual-screen coordinates and from
which one is primary. Because the primary monitor anchors the origin, those coordinates
encode where the monitors sit relative to one another, so moving a monitor from the left of
the primary to its right produces a different key. Display device names are deliberately left
out, since Windows reassigns them as monitors come and go.

Ignore the argument and one set of placements serves every arrangement. Key your storage by
it and each arrangement gets its own set — which is what `FileWindowPositionStore` does when
you ask it to:

```cs
// placement.3f2a1c09.config, one file per arrangement
new FileWindowPositionStore("placement.config", perMonitorLayout: true);
```

A store backed by the registry, by a settings table, or by your own configuration system is a
dozen lines:

```cs
sealed class RegistryStore : IWindowPositionStore
{
    public byte[]? Read(uint monitorLayout) =>
        Registry.CurrentUser.OpenSubKey(@"Software\Contoso\Windows")
            ?.GetValue(monitorLayout.ToString("x8")) as byte[];

    public void Write(uint monitorLayout, ReadOnlySpan<byte> data) =>
        Registry.CurrentUser.CreateSubKey(@"Software\Contoso\Windows")
            .SetValue(monitorLayout.ToString("x8"), data.ToArray(), RegistryValueKind.Binary);
}
```

The payload handed to `Write` is only valid for the duration of the call, so a store that
keeps it must copy it.

Saving is a read-modify-write: what is already stored is merged with what this session
recorded, so several windows — or a second instance of the application — can save against the
same store without overwriting each other.

## What gets written

A short binary blob, read and written by hand with `BinaryWriter` and `BinaryReader`:

```
byte     magic      'W'
byte     version    1
varint   count
per window:
  string key        length-prefixed UTF-8
  varint left       zig-zag encoded
  varint top        zig-zag encoded
  varint width      zig-zag encoded
  varint height     zig-zag encoded
  byte   state      0 normal, 1 minimised, 2 maximised
```

Nothing of the shape of these types is recorded, so a typical window costs about twenty
bytes. Zig-zag varints keep small magnitudes to one byte in either direction, so a window on
a monitor to the left of the primary — where the coordinates are negative — costs no more
than one on the primary itself. Records are ordered by key, so the same placements always
produce identical bytes.

`WindowPositionFormat.Format` and `WindowPositionFormat.Parse` are public if you want to read
or write the payload yourself. Parsing is forgiving: an unrecognised payload yields nothing,
and a truncated or damaged one yields the records read up to the damage. A settings file that
has been corrupted should cost the user their window positions, not their session.

## Notes on the implementation

- **No runtime reflection.** Serialization is written out by hand rather than handed to
  `DataContractSerializer`, and show states are mapped with a `switch` rather than
  `Enum.Parse`. `BinaryFormatter` is not an option here — it was removed from .NET in 5.0 and
  is reflection-based besides.
- **Source-generated interop.** On .NET 7 and later the Win32 signatures use
  `[LibraryImport]`, so the marshalling stubs are emitted at compile time; .NET Framework has
  no such generator, so the identical signatures are declared with `[DllImport]` there. Every
  parameter type is blittable, which is what lets one set of signatures serve both.
- **DPI-independent keys.** Windows reports monitor rectangles scaled to the calling thread's
  DPI context, and a WPF application changes its own context partway through startup. The
  monitors are read under an explicit per-monitor DPI context so the key depends on the
  hardware alone — otherwise an application would load its placements under one key and save
  them under another, and would never restore a window again.
- **No WPF in the core.** `WindowPositionRepository` holds the placements and the storage
  cycle without touching WPF, so it can be driven from WinForms or from anywhere else that
  can produce a `WindowPosition`. `WindowPlacer` is a thin shim over it.
- **Cancelled closes.** A placement is read on `Closing`, while the window still has a handle,
  but only committed on `Closed` — so a close that another handler cancels does not record the
  window.

## Building

```
dotnet build RestoreWindowPosition.slnx
dotnet test
```

Tests are xUnit v3 on Microsoft.Testing.Platform (the `xunit.v3.mtp-v2` package) with
FluentAssertions, and run against both target frameworks. `global.json` opts `dotnet test`
into Microsoft.Testing.Platform mode.

Do not pass `--nologo` to `dotnet test` here. It is a VSTest-only option; in MTP mode it is
forwarded verbatim to each test application, which rejects it — MTP spells it `--no-banner` —
and the run reports "Zero tests ran" with exit code 5 before discovering anything. The same
goes for other VSTest-only options.

The test project also builds to a self-contained executable, so
`artifacts/bin/RestoreWindowPosition.Tests/debug_net472/RestoreWindowPosition.Tests.exe`
runs the suite directly.

Most of the suite is pure logic and runs anywhere. The handful that need the operating
system — reading the attached monitors, creating a real window — fail rather than skip when
no monitor is attached: an agent with no display cannot verify them, and a green run that
quietly proved nothing about the half of this library that talks to Win32 is worse than a
red one. A test that wants a *second* monitor still skips, since that is genuinely optional.

CI runs the suite on x86, x64 and arm64, each across both target frameworks — six runs in
total. .NET Framework 4.8.1 added native arm64 support, so the net472 arm64 leg is native
rather than emulated. `ArchitectureTests` asserts the exact bytes a placement encodes to and
the exact key an arrangement hashes to, so a change that made either depend on word size,
byte order or the current culture would break every leg at once rather than lying dormant
until someone ran a 32-bit build.

A further job attaches a second, virtual monitor before running the suite, because a build
agent has one display and a single monitor never exercises the relative positioning that the
arrangement key exists for. It uses Amyuni's usbmmidd driver, pinned by SHA-256: the vendor
publishes one unversioned URL, so the checksum is the real pin and a replaced download fails
the job rather than quietly changing what was tested. Its licence permits redistribution, so
mirroring the zip to a release in this repository — and pointing `USBMMIDD_URL` at that — is
the sturdier option if you would rather not depend on a third-party host at build time.

`RestoreWindowPosition.Sample` is a small WPF application that shows the current monitor
arrangement and its key, and exercises both registration styles.

## Credits

A fork of [Boredbone/RestoreWindowPlace](https://github.com/Boredbone/RestoreWindowPlace).
MIT licensed.
