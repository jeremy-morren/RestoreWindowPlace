using System.Runtime.InteropServices;

namespace RestoreWindowPosition.Interop;

/// <summary>
/// The user32 entry points the library needs.
/// </summary>
/// <remarks>
/// On .NET 7 and later these are <c>[LibraryImport]</c> declarations, so the SDK source
/// generator emits the marshalling stubs at compile time and nothing has to be generated at
/// runtime. .NET Framework has no such generator, so the same signatures are declared with
/// <see cref="DllImportAttribute"/> there. Every parameter type is blittable, which is what
/// lets one set of signatures serve both.
/// </remarks>
internal static partial class NativeMethods
{
#if NET7_0_OR_GREATER

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr MonitorFromRect(ref Rect lprc, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static unsafe partial bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Rect*, IntPtr, int> lpfnEnum,
        IntPtr dwData);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

#else

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromRect(ref Rect lprc, uint dwFlags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    /// <summary>The <c>MONITORENUMPROC</c> callback.</summary>
    internal delegate int MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Rect lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

#endif

    /// <summary>The value of <c>MONITOR_DEFAULTTONEAREST</c>.</summary>
    internal const uint MonitorDefaultToNearest = 2;

    /// <summary>The value of <c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c> (Windows 10 1703).</summary>
    internal static readonly IntPtr PerMonitorAwareV2 = new(-4);

    /// <summary>The value of <c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE</c> (Windows 10 1607).</summary>
    internal static readonly IntPtr PerMonitorAware = new(-3);
}
