using RestoreWindowPosition.Interop;

namespace RestoreWindowPosition;

/// <summary>
/// Reads and writes a window's placement through <c>GetWindowPlacement</c> and
/// <c>SetWindowPlacement</c>.
/// </summary>
internal static class WindowRelocate
{
    /// <summary>Moves, and optionally resizes, a window.</summary>
    /// <param name="windowHandle">The window handle.</param>
    /// <param name="position">The placement to apply.</param>
    /// <param name="positionOnly">
    /// When <see langword="true"/>, the window keeps its current size and show state and
    /// only its top-left corner moves.
    /// </param>
    public static void Relocate(IntPtr windowHandle, WindowPosition position, bool positionOnly)
    {
        var placement = WindowPlacement.Create();
        if (!NativeMethods.GetWindowPlacement(windowHandle, ref placement)) return;

        var width = placement.NormalPosition.Right - placement.NormalPosition.Left;
        var height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;

        if (!positionOnly && position.HasSize)
        {
            width = position.Width;
            height = position.Height;
        }

        placement.Flags = 0;
        placement.ShowCmd = positionOnly ? ShowWindowCommand.Restore : ToShowCommand(position.State);
        placement.NormalPosition.Left = position.Left;
        placement.NormalPosition.Top = position.Top;
        placement.NormalPosition.Right = position.Left + width;
        placement.NormalPosition.Bottom = position.Top + height;

        if (!NativeMethods.SetWindowPlacement(windowHandle, ref placement)) return;

        // Bringing a window the user deliberately left minimised back to the front would
        // undo the very state we just restored.
        if (placement.ShowCmd != ShowWindowCommand.ShowMinimized)
        {
            NativeMethods.SetForegroundWindow(windowHandle);
        }
    }

    /// <summary>Reads a window's current placement.</summary>
    /// <param name="windowHandle">The window handle.</param>
    /// <param name="useActualPosition">
    /// When <see langword="true"/>, a normal (neither minimised nor maximised) window reports
    /// where it is actually drawn rather than its restored position, which is what records a
    /// snapped window where the user snapped it.
    /// </param>
    /// <returns>The placement.</returns>
    public static WindowPosition GetPlace(IntPtr windowHandle, bool useActualPosition)
    {
        var placement = WindowPlacement.Create();
        if (!NativeMethods.GetWindowPlacement(windowHandle, ref placement)) return default;

        var state = placement.ShowCmd switch
        {
            ShowWindowCommand.ShowMinimized or ShowWindowCommand.Minimize => WindowShowState.Minimized,
            ShowWindowCommand.Maximize => WindowShowState.Maximized,
            _ => WindowShowState.Normal,
        };

        if (useActualPosition && state == WindowShowState.Normal &&
            NativeMethods.GetWindowRect(windowHandle, out var rect))
        {
            // GetWindowRect reports the position on screen, while SetWindowPlacement takes a
            // workspace position, so the two differ by the size of any appbar along the top or
            // the left of the monitor.
            var offset = GetTaskbarOffset(ref rect);

            return new WindowPosition(
                rect.Left - offset.X,
                rect.Top - offset.Y,
                rect.Right - rect.Left,
                rect.Bottom - rect.Top,
                state);
        }

        return new WindowPosition(
            placement.NormalPosition.Left,
            placement.NormalPosition.Top,
            placement.NormalPosition.Right - placement.NormalPosition.Left,
            placement.NormalPosition.Bottom - placement.NormalPosition.Top,
            state);
    }

    private static ShowWindowCommand ToShowCommand(WindowShowState state) => state switch
    {
        WindowShowState.Minimized => ShowWindowCommand.ShowMinimized,
        WindowShowState.Maximized => ShowWindowCommand.Maximize,
        _ => ShowWindowCommand.Restore,
    };

    private static Point GetTaskbarOffset(ref Rect rect)
    {
        var monitor = NativeMethods.MonitorFromRect(ref rect, NativeMethods.MonitorDefaultToNearest);

        var info = MonitorInfoEx.Create();
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return default;

        return new Point
        {
            X = Math.Max(info.Work.Left - info.Monitor.Left, 0),
            Y = Math.Max(info.Work.Top - info.Monitor.Top, 0),
        };
    }
}
