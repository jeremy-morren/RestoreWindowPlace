using System.Globalization;

namespace RestoreWindowPosition;

/// <summary>
/// One monitor attached to the desktop, as reported by the operating system.
/// </summary>
public readonly struct MonitorInfo : IEquatable<MonitorInfo>
{
    /// <summary>Creates a monitor description.</summary>
    /// <param name="deviceName">The display device name, for example <c>\\.\DISPLAY1</c>.</param>
    /// <param name="bounds">The full monitor rectangle in virtual-screen coordinates.</param>
    /// <param name="workArea">The monitor rectangle excluding the taskbar and other appbars.</param>
    /// <param name="isPrimary">Whether this is the primary monitor.</param>
    /// <exception cref="ArgumentNullException"><paramref name="deviceName"/> is <see langword="null"/>.</exception>
    public MonitorInfo(string deviceName, ScreenRect bounds, ScreenRect workArea, bool isPrimary)
    {
        DeviceName = deviceName ?? throw new ArgumentNullException(nameof(deviceName));
        Bounds = bounds;
        WorkArea = workArea;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// The display device name, for example <c>\\.\DISPLAY1</c>.
    /// </summary>
    /// <remarks>
    /// Windows reassigns these names as monitors are attached and detached, so the name
    /// is carried for diagnostics only and takes no part in <see cref="MonitorLayout.Key"/>.
    /// </remarks>
    public string DeviceName { get; }

    /// <summary>The full monitor rectangle in virtual-screen coordinates.</summary>
    public ScreenRect Bounds { get; }

    /// <summary>The monitor rectangle excluding the taskbar and other appbars.</summary>
    public ScreenRect WorkArea { get; }

    /// <summary>Whether this is the primary monitor, whose top-left corner is the origin.</summary>
    public bool IsPrimary { get; }

    /// <inheritdoc />
    public bool Equals(MonitorInfo other) =>
        Bounds == other.Bounds &&
        WorkArea == other.WorkArea &&
        IsPrimary == other.IsPrimary &&
        string.Equals(DeviceName, other.DeviceName, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is MonitorInfo other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Bounds.GetHashCode();
            hash = (hash * 397) ^ WorkArea.GetHashCode();
            hash = (hash * 397) ^ (IsPrimary ? 1 : 0);
            hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DeviceName ?? string.Empty);
            return hash;
        }
    }

    /// <summary>Compares two monitor descriptions for equality.</summary>
    /// <param name="left">The first monitor.</param>
    /// <param name="right">The second monitor.</param>
    /// <returns><see langword="true"/> if every field matches.</returns>
    public static bool operator ==(MonitorInfo left, MonitorInfo right) => left.Equals(right);

    /// <summary>Compares two monitor descriptions for inequality.</summary>
    /// <param name="left">The first monitor.</param>
    /// <param name="right">The second monitor.</param>
    /// <returns><see langword="true"/> if any field differs.</returns>
    public static bool operator !=(MonitorInfo left, MonitorInfo right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} {1}{2}", DeviceName, Bounds, IsPrimary ? " (primary)" : string.Empty);
}
