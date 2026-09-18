using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using RestoreWindowPosition.Interop;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable ConvertIfStatementToReturnStatement

namespace RestoreWindowPosition;

/// <summary>
/// The set of monitors currently attached to the desktop,
/// together with a stable <see cref="Key"/> identifying that arrangement.
/// </summary>
/// <remarks>
/// <para>
/// The key is a stable digest of a monitor arrangement so a store can keep a separate set of window
/// placements per physical arrangement: undocking a laptop, or swapping a portrait monitor for
/// a landscape one, selects a different set instead of overwriting the old one.
/// </para>
/// <para>
/// The key is derived from each monitor's bounds and work area in virtual-screen coordinates
/// plus which monitor is primary. Because the primary monitor anchors the origin, those
/// coordinates encode the monitors' positions relative to one another, so moving a monitor from
/// the left of the primary to its right changes the key.
/// </para>
/// <para>
/// Device names are deliberately excluded: Windows reassigns them as displays come and go.
/// </para>
/// </remarks>
public sealed class MonitorLayout
{
    /// <summary>Describes a fixed set of monitors.</summary>
    /// <param name="monitors">The attached monitors, in any order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="monitors"/> is <see langword="null"/>.</exception>
    public MonitorLayout(IEnumerable<MonitorInfo> monitors)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(monitors);
#else
        if (monitors is null) 
            throw new ArgumentNullException(nameof(monitors));
#endif

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
    /// The same arrangement always produces the same key, on any machine
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
    public bool IsVisible(ScreenRect rect) => Monitors.Any(monitor => monitor.WorkArea.IntersectsWith(rect));

    /// <inheritdoc />
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture, "{0:x8} ({1} monitor(s))", Key, Monitors.Count);

    private static int CompareMonitors(MonitorInfo x, MonitorInfo y)
    {
        var c = x.Bounds.Left.CompareTo(y.Bounds.Left);
        if (c != 0) return c;
        c = x.Bounds.Top.CompareTo(y.Bounds.Top);
        if (c != 0) return c;
        c = x.Bounds.Right.CompareTo(y.Bounds.Right);
        if (c != 0) return c;
        c = x.Bounds.Bottom.CompareTo(y.Bounds.Bottom);
        if (c != 0) return c;
        c = x.WorkArea.Left.CompareTo(y.WorkArea.Left);
        if (c != 0) return c;
        c = x.WorkArea.Top.CompareTo(y.WorkArea.Top);
        if (c != 0) return c;
        c = x.WorkArea.Right.CompareTo(y.WorkArea.Right);
        if (c != 0) return c;
        c = x.WorkArea.Bottom.CompareTo(y.WorkArea.Bottom);
        if (c != 0) return c;
        return x.IsPrimary.CompareTo(y.IsPrimary);
    }

    private static uint ComputeKey(List<MonitorInfo> ordered)
    {
        // Two rectangles of four ints, plus one byte for the primary flag
        const int bytesPerMonitor = (2 * 4 * sizeof(int)) + 1;
        
        // Sized exactly, so the stream never has to grow its buffer.
        using var writer = new MemoryStream(ordered.Count * bytesPerMonitor);
    
        foreach (var monitor in ordered)
        {
            WriteRect(writer, monitor.Bounds);
            WriteRect(writer, monitor.WorkArea);
            writer.WriteBool(monitor.IsPrimary);
        }

        // GetBuffer hands back the stream's own array, so nothing is copied to hash it.
        return Fnv1a32.Hash(writer.GetBuffer(), (int)writer.Length);
        
        static void WriteRect(Stream stream, ScreenRect rect)
        {
            stream.WriteInt32(rect.Left);
            stream.WriteInt32(rect.Top);
            stream.WriteInt32(rect.Right);
            stream.WriteInt32(rect.Bottom);
        }
    }
}
