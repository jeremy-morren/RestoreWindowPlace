using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using RestoreWindowPosition.Interop;

namespace RestoreWindowPosition;

/// <summary>
/// The set of monitors currently attached to the desktop, together with a stable
/// <see cref="Key"/> identifying that arrangement.
/// </summary>
/// <remarks>
/// <para>
/// The key is what <see cref="WindowPlacer"/> hands to <see cref="IWindowPositionStore.Read"/>
/// and <see cref="IWindowPositionStore.Write"/>, so a store can keep a separate set of window
/// placements per physical arrangement: undocking a laptop, or swapping a portrait monitor for
/// a landscape one, selects a different set instead of overwriting the old one.
/// </para>
/// <para>
/// The key is derived from each monitor's bounds and work area in virtual-screen coordinates
/// plus which monitor is primary. Because the primary monitor anchors the origin, those
/// coordinates encode the monitors' positions relative to one another, so moving a monitor from
/// the left of the primary to its right changes the key. Device names are deliberately excluded:
/// Windows reassigns them as displays come and go.
/// </para>
/// </remarks>
public sealed class MonitorLayout
{
    /// <summary>Describes a fixed set of monitors.</summary>
    /// <param name="monitors">The attached monitors, in any order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="monitors"/> is <see langword="null"/>.</exception>
    public MonitorLayout(IEnumerable<MonitorInfo> monitors)
    {
        if (monitors is null) throw new ArgumentNullException(nameof(monitors));

        // Enumeration order is not guaranteed to be stable across calls, so canonicalise it.
        var ordered = new List<MonitorInfo>(monitors);
        ordered.Sort(CompareMonitors);

        Monitors = new ReadOnlyCollection<MonitorInfo>(ordered);
        Key = ComputeKey(ordered);
    }

    /// <summary>The attached monitors, ordered top-left to bottom-right.</summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>
    /// A stable 32-bit digest of this arrangement.
    /// </summary>
    /// <remarks>
    /// The same arrangement always produces the same key, on any machine and across process
    /// restarts. Thirty-two bits leave the chance of two of a user's arrangements colliding
    /// at roughly one in a million even for someone who has had a hundred of them.
    /// </remarks>
    public uint Key { get; }

    /// <summary>
    /// Reads the monitors currently attached to the desktop.
    /// </summary>
    /// <remarks>
    /// Each call queries the operating system afresh, so the result reflects monitors
    /// attached or detached since the last call.
    /// </remarks>
    /// <returns>The current arrangement.</returns>
    public static MonitorLayout GetCurrent() => new(MonitorEnumerator.GetMonitors());

    /// <summary>
    /// Determines whether <paramref name="rect"/> would land somewhere the user can see and
    /// reach it, that is, whether it overlaps the work area of at least one monitor.
    /// </summary>
    /// <param name="rect">The rectangle to test, in virtual-screen coordinates.</param>
    /// <returns><see langword="true"/> if any monitor's work area overlaps the rectangle.</returns>
    public bool IsVisible(ScreenRect rect)
    {
        foreach (var monitor in Monitors)
        {
            if (monitor.WorkArea.IntersectsWith(rect)) return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0:x8} ({1} monitor(s))", Key, Monitors.Count);

    private static int CompareMonitors(MonitorInfo x, MonitorInfo y)
    {
        var result = x.Bounds.Left.CompareTo(y.Bounds.Left);
        if (result != 0) return result;
        result = x.Bounds.Top.CompareTo(y.Bounds.Top);
        if (result != 0) return result;
        result = x.Bounds.Right.CompareTo(y.Bounds.Right);
        if (result != 0) return result;
        result = x.Bounds.Bottom.CompareTo(y.Bounds.Bottom);
        if (result != 0) return result;
        result = x.WorkArea.Left.CompareTo(y.WorkArea.Left);
        if (result != 0) return result;
        result = x.WorkArea.Top.CompareTo(y.WorkArea.Top);
        if (result != 0) return result;
        result = x.WorkArea.Right.CompareTo(y.WorkArea.Right);
        if (result != 0) return result;
        result = x.WorkArea.Bottom.CompareTo(y.WorkArea.Bottom);
        if (result != 0) return result;
        return x.IsPrimary.CompareTo(y.IsPrimary);
    }

    /// <summary>Two rectangles of four <see cref="int"/>s, plus one byte for the primary flag.</summary>
    private const int BytesPerMonitor = (2 * 4 * sizeof(int)) + 1;

    private static uint ComputeKey(List<MonitorInfo> ordered)
    {
        // Sized exactly, so the stream never has to grow its buffer.
        using var buffer = new MemoryStream(ordered.Count * BytesPerMonitor);

        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false), leaveOpen: true))
        {
            foreach (var monitor in ordered)
            {
                WriteRect(writer, monitor.Bounds);
                WriteRect(writer, monitor.WorkArea);
                writer.Write(monitor.IsPrimary);
            }
        }

        // GetBuffer hands back the stream's own array, so nothing is copied to hash it.
        return Fnv1a32.Hash(buffer.GetBuffer(), (int)buffer.Length);
    }

    // Fixed-width little-endian, straight from the values. Rendering them as text first
    // would drag in the current culture's negative sign, and a key must not depend on the
    // thread it was computed on.
    private static void WriteRect(BinaryWriter writer, ScreenRect rect)
    {
        writer.Write(rect.Left);
        writer.Write(rect.Top);
        writer.Write(rect.Right);
        writer.Write(rect.Bottom);
    }
}
