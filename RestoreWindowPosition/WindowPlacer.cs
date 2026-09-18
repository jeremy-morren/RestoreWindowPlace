using System.Windows;
using System.Windows.Interop;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable UnusedMember.Global

namespace RestoreWindowPosition;

/// <summary>
/// Saves and restores the size and position of WPF windows.
/// </summary>
/// <example>
/// <code>
/// public partial class App : Application
/// {
///     public WindowPlacer WindowPlacer { get; } = new WindowPlacer("placement.bin");
///
///     protected override void OnExit(ExitEventArgs e)
///     {
///         base.OnExit(e);
///         WindowPlacer.Save();
///     }
/// }
/// </code>
/// </example>
public sealed class WindowPlacer
{
    /// <summary>Saves window placements in a file.</summary>
    /// <param name="filePath">Name or path of the file to save placements in.</param>
    /// <param name="perMonitorLayout">
    /// When <see langword="true"/>, each monitor arrangement gets its own file. See
    /// <see cref="FileWindowPositionStore"/>.
    /// </param>
    public WindowPlacer(string filePath, bool perMonitorLayout = false)
        : this(new FileWindowPositionStore(filePath, perMonitorLayout))
    {
    }

    /// <summary>Saves window placements wherever the caller chooses, caching the monitor layout when first accessed.</summary>
    /// <param name="store">Where placements are kept.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public WindowPlacer(IWindowPositionStore store)
        : this(new WindowPositionRepository(store, true))
    {
    }

    /// <summary>Saves window placements through an existing repository.</summary>
    /// <param name="positions">The repository to read and write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="positions"/> is <see langword="null"/>.</exception>
    public WindowPlacer(WindowPositionRepository positions) =>
        Positions = positions ?? throw new ArgumentNullException(nameof(positions));

    /// <summary>
    /// When <see langword="true"/>, a snapped window is saved where it is drawn. When
    /// <see langword="false"/> (the default), it is saved at the position it would return
    /// to if it were unsnapped.
    /// </summary>
    public bool IsSavingSnappedPositionEnabled { get; set; }

    /// <summary>The placements this instance reads and writes.</summary>
    public WindowPositionRepository Positions { get; }

    /// <summary>Writes every placement recorded so far to the store.</summary>
    public void Save() => Positions.Save();

    /// <summary>Re-reads the store, discarding anything recorded but not yet saved.</summary>
    public void Reload() => Positions.Reload();

    /// <summary>Determines whether a window has been saved under a key.</summary>
    /// <param name="window">The window to look for.</param>
    /// <returns><see langword="true"/> if a placement was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public bool IsRegistered(Window window) => Positions.Contains(window);

    /// <summary>Restores a window's size, position and show state.</summary>
    /// <param name="window">The window to move.</param>
    /// <param name="predicate">
    /// Consulted with the placement that is about to be applied. Returning
    /// <see langword="false"/> leaves the window where it is.
    /// </param>
    /// <returns><see langword="true"/> if the window was moved.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public bool Restore<T>(T window, Func<T, WindowPosition, bool>? predicate = null) 
        where T : Window =>
        RestoreCore(window, positionOnly: false, Bind(window, predicate));

    /// <summary>Restores a window's position, leaving its size and show state alone.</summary>
    /// <param name="window">The window to move.</param>
    /// <param name="predicate">
    /// Consulted with the placement that is about to be applied. Returning
    /// <see langword="false"/> leaves the window where it is.
    /// </param>
    /// <returns><see langword="true"/> if the window was moved.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public bool RestorePosition<T>(T window, Func<T, WindowPosition, bool>? predicate = null) 
        where T : Window =>
        RestoreCore(window, positionOnly: true, Bind(window, predicate));

    /// <summary>
    /// Records a window's current placement. Nothing reaches the store until <see cref="Save"/> is called.
    /// </summary>
    /// <param name="window">The window to store.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Store(Window window)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(window);
#else
        if (window is null) 
            throw new ArgumentNullException(nameof(window));
#endif

        if (TryGetPosition(window, out var position)) 
            Positions.Set(window, position);
    }

    private bool TryGetPosition(Window window, out WindowPosition position)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            position = default;
            return false;
        }

        position = WindowRelocate.GetPlace(handle, IsSavingSnappedPositionEnabled);
        return true;
    }

    /// <summary>
    /// Restores a window's position and size when it opens and records them when it closes
    /// </summary>
    /// <typeparam name="T">The window type, whose name becomes the key.</typeparam>
    /// <param name="window">The window to track.</param>
    /// <param name="predicate">
    /// Consulted with the placement that is about to be applied, giving the caller the last
    /// word on whether the window is moved — to decline a position that is now off-screen,
    /// say, or one saved by a different user of the application.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public void Register<T>(T window, Func<T, WindowPosition, bool>? predicate = null) 
        where T : Window => 
        RegisterCore(window, positionOnly: false, predicate);

    /// <summary>
    /// Restores a window's position when it opens and records it when it closes
    /// </summary>
    /// <typeparam name="T">The window type.</typeparam>
    /// <param name="window">The window to track.</param>
    /// <param name="predicate">
    /// Consulted with the placement that is about to be applied. Returning
    /// <see langword="false"/> leaves the window where WPF put it.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void RegisterPositionOnly<T>(T window, Func<T, WindowPosition, bool>? predicate = null)
        where T : Window =>
        RegisterCore(window, positionOnly: true, predicate);

    private void RegisterCore<T>(T window, bool positionOnly, Func<T, WindowPosition, bool>? predicate)
        where T : Window
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(window);
#else
        if (window is null) 
            throw new ArgumentNullException(nameof(window));
#endif

        var bound = Bind(window, predicate);

        // A window registered after it has already been shown never raises SourceInitialized
        // again, so restore it straight away instead of silently doing nothing.
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero)
        {
            RestoreCore(window, positionOnly, bound);
        }
        else
        {
            window.SourceInitialized += (_, _) => RestoreCore(window, positionOnly, bound);
        }

        // The placement has to be read while the window still has a handle, which means Closing event.
        // However, a handler added after this one can still cancel the close, and no handler can see the final verdict.
        // So read on Closing and commit on Closed, which only runs if the window really did close.
        WindowPosition pending = default;
        var read = false;

        // We use the window object from the sender rather than the captured variable for robustness
        window.Closing += (_, _) =>
        {
            read = TryGetPosition(window, out pending);
        };
        window.Closed += (_, _) =>
        {
            if (read) 
                Positions.Set(window, pending);
            read = false;
        };
    }

    private bool RestoreCore(Window window, bool positionOnly, Func<WindowPosition, bool>? predicate)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(window);
#else
        if (window is null) 
            throw new ArgumentNullException(nameof(window));
#endif

        if (!Positions.TryGet(window, out var position)) 
            return false;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return false;

        // Consulted last, so the predicate is only asked about a move that would otherwise
        // happen — not about one already ruled out for want of a placement or a handle.
        if (predicate is not null && !predicate(position))
            return false;

        WindowRelocate.Relocate(handle, position, positionOnly);
        return true;
    }

    private static Func<WindowPosition, bool>? Bind<T>(T window, Func<T, WindowPosition, bool>? predicate)
        where T : Window =>
        predicate is null ? null : position => predicate(window, position);
}
