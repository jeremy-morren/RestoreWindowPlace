using System.Globalization;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo

namespace RestoreWindowPosition;

/// <summary>
/// How a window is shown, independently of its restored size and position.
/// </summary>
public enum WindowShowState
{
    /// <summary>The window is shown at its restored size and position.</summary>
    Normal = 0,

    /// <summary>The window is minimised to the taskbar.</summary>
    Minimized = 1,

    /// <summary>The window fills the work area of its monitor.</summary>
    Maximized = 2,
}

/// <summary>
/// The persisted placement of a single window: its restored size and position, plus
/// the show state it was last left in.
/// </summary>
/// <remarks>
/// <see cref="Left"/> and <see cref="Top"/> are always the restored (non-maximised)
/// position, which is what Windows itself keeps in <c>WINDOWPLACEMENT</c>. A maximised
/// window therefore round-trips both the monitor it was maximised on and the size it
/// returns to when un-maximised.
/// </remarks>
public readonly struct WindowPosition : IEquatable<WindowPosition>
{
    /// <summary>Creates a window placement.</summary>
    /// <param name="left">The x-coordinate of the restored left edge.</param>
    /// <param name="top">The y-coordinate of the restored top edge.</param>
    /// <param name="width">The restored width, in pixels. Zero means the size is unknown.</param>
    /// <param name="height">The restored height, in pixels. Zero means the size is unknown.</param>
    /// <param name="state">The show state the window was last left in.</param>
    public WindowPosition(int left, int top, int width, int height, WindowShowState state = WindowShowState.Normal)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        State = state;
    }

    /// <summary>The x-coordinate of the restored left edge.</summary>
    public int Left { get; }

    /// <summary>The y-coordinate of the restored top edge.</summary>
    public int Top { get; }

    /// <summary>The restored width, in pixels.</summary>
    public int Width { get; }

    /// <summary>The restored height, in pixels.</summary>
    public int Height { get; }

    /// <summary>The show state the window was last left in.</summary>
    public WindowShowState State { get; }

    /// <summary>
    /// <see langword="true"/> when a usable size was recorded. When <see langword="false"/>,
    /// restoring moves the window without resizing it.
    /// </summary>
    public bool HasSize => Width > 0 && Height > 0;

    /// <summary>The restored rectangle in virtual-screen coordinates.</summary>
    public ScreenRect Bounds => ScreenRect.FromSize(Left, Top, Width, Height);

    /// <inheritdoc />
    public bool Equals(WindowPosition other) =>
        Left == other.Left && Top == other.Top &&
        Width == other.Width && Height == other.Height &&
        State == other.State;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WindowPosition other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Left;
            hash = (hash * 397) ^ Top;
            hash = (hash * 397) ^ Width;
            hash = (hash * 397) ^ Height;
            hash = (hash * 397) ^ (int)State;
            return hash;
        }
    }

    /// <summary>Compares two placements for equality.</summary>
    /// <param name="left">The first placement.</param>
    /// <param name="right">The second placement.</param>
    /// <returns><see langword="true"/> if every field matches.</returns>
    public static bool operator ==(WindowPosition left, WindowPosition right) => left.Equals(right);

    /// <summary>Compares two placements for inequality.</summary>
    /// <param name="left">The first placement.</param>
    /// <param name="right">The second placement.</param>
    /// <returns><see langword="true"/> if any field differs.</returns>
    public static bool operator !=(WindowPosition left, WindowPosition right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0},{1} {2}x{3} ({4})", Left, Top, Width, Height, State);
}
