using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable IdentifierTypo

namespace RestoreWindowPosition.Interop;

/// <summary>
/// Reads the monitors attached to the desktop via <c>EnumDisplayMonitors</c>.
/// </summary>
internal static class MonitorEnumerator
{
    /// <summary>Enumerates the attached monitors.</summary>
    /// <returns>
    /// The monitors, in whatever order the operating system reports them.
    /// Empty if the desktop cannot be enumerated, for instance from a service in session 0.
    /// </returns>
    public static List<MonitorInfo> GetMonitors()
    {
        var restore = EnterPerMonitorAwareness();
        try
        {
            return Enumerate();
        }
        finally
        {
            if (restore != IntPtr.Zero) 
                NativeMethods.SetThreadDpiAwarenessContext(restore);
        }
    }

    private static List<MonitorInfo> Enumerate()
    {
        var monitors = new List<MonitorInfo>();

        // The list travels into the callback as a handle rather than as a captured variable:
        // an [UnmanagedCallersOnly] method cannot close over anything.
        var handle = GCHandle.Alloc(monitors);
        bool enumerated;
        try
        {
#if NET7_0_OR_GREATER
            unsafe
            {
                enumerated = NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, &OnMonitor, GCHandle.ToIntPtr(handle));
            }
#else
            enumerated = NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, OnMonitor, GCHandle.ToIntPtr(handle));
#endif
        }
        finally
        {
            handle.Free();
        }

        return enumerated ? monitors : [];
    }

    /// <summary>
    /// Puts this thread into per-monitor DPI awareness for the duration of the enumeration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Windows reports monitor rectangles in the DPI context of the calling thread:
    /// a process that is not DPI aware sees a 3840x2160 monitor at 150% as 2560x1440.
    /// A WPF application becomes DPI aware partway through startup,
    /// so the same machine reports one arrangement from the <c>App</c> constructor and a different one a moment later,
    /// and a key derived from it would never match itself across a process.
    /// </para>
    /// <para>
    /// Asking for per-monitor awareness here makes the reported rectangles physical pixels
    /// whatever the host process has done, so the key depends on the monitors alone.
    /// </para>
    /// </remarks>
    /// <returns>
    /// The context to put back afterwards, or <see cref="IntPtr.Zero"/> if this version of
    /// Windows has no such call, in which case the caller's own context is used.
    /// </returns>
    private static IntPtr EnterPerMonitorAwareness()
    {
        if (_threadDpiApiMissing)
            return IntPtr.Zero;

        try
        {
            // V2 arrived in Windows 10 1703 and V1 in 1607
            // the call simply returns null for a context this build does not know.
            var restore = NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.PerMonitorAwareV2);
            if (restore == IntPtr.Zero)
                restore = NativeMethods.SetThreadDpiAwarenessContext(NativeMethods.PerMonitorAware);

            return restore;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // Windows 8.1 and earlier.
            // Fall back to the caller's context, which on those versions cannot change partway through a process anyway.
            _threadDpiApiMissing = true;
            return IntPtr.Zero;
        }
    }

    private static bool _threadDpiApiMissing;

#if NET7_0_OR_GREATER

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe int OnMonitor(IntPtr hMonitor, IntPtr hdc, Rect* lprcMonitor, IntPtr dwData) =>
        Collect(hMonitor, dwData);

#else

    private static int OnMonitor(IntPtr hMonitor, IntPtr hdc, ref Rect lprcMonitor, IntPtr dwData) =>
        Collect(hMonitor, dwData);

#endif

    private static int Collect(IntPtr hMonitor, IntPtr dwData)
    {
        try
        {
            if (GCHandle.FromIntPtr(dwData).Target is List<MonitorInfo> monitors &&
                TryDescribe(hMonitor, out var monitor))
            {
                monitors.Add(monitor);
            }
        }
        catch
        {
            // Letting an exception unwind through native frames would tear down the process.
            // A monitor we failed to describe is simply left out of the arrangement.
        }

        return 1; // Keep enumerating.
    }

    private static bool TryDescribe(IntPtr hMonitor, out MonitorInfo monitor)
    {
        var native = MonitorInfoEx.Create();
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref native))
        {
            monitor = default;
            return false;
        }

        monitor = new MonitorInfo(
            ReadDeviceName(ref native),
            native.Monitor.ToScreenRect(),
            native.Work.ToScreenRect(),
            (native.Flags & MonitorInfoEx.PrimaryFlag) != 0);
        return true;
    }

    private static unsafe string ReadDeviceName(ref MonitorInfoEx native)
    {
        fixed (char* start = native.DeviceName)
        {
            var length = 0;
            while (length < MonitorInfoEx.DeviceNameLength && start[length] != '\0') length++;
            return new string(start, 0, length);
        }
    }
}
