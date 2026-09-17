using System.Runtime.InteropServices;

namespace RestoreWindowPosition.Interop;

/// <summary>The Win32 <c>RECT</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly ScreenRect ToScreenRect() => new(Left, Top, Right, Bottom);
}

/// <summary>The Win32 <c>POINT</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct Point
{
    public int X;
    public int Y;
}

/// <summary>The Win32 <c>SW_*</c> show commands, as used by <see cref="WindowPlacement.ShowCmd"/>.</summary>
internal enum ShowWindowCommand
{
    Hide = 0,
    Normal = 1,
    ShowMinimized = 2,
    Maximize = 3,
    ShowNoActivate = 4,
    Show = 5,
    Minimize = 6,
    ShowMinNoActive = 7,
    ShowNA = 8,
    Restore = 9,
    ShowDefault = 10,
    ForceMinimize = 11,
}

/// <summary>The Win32 <c>WINDOWPLACEMENT</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WindowPlacement
{
    public int Length;
    public int Flags;
    public ShowWindowCommand ShowCmd;
    public Point MinPosition;
    public Point MaxPosition;
    public Rect NormalPosition;

    /// <summary>
    /// Creates a structure with its <see cref="Length"/> filled in, which both
    /// <c>GetWindowPlacement</c> and <c>SetWindowPlacement</c> require.
    /// </summary>
    public static unsafe WindowPlacement Create() => new() { Length = sizeof(WindowPlacement) };
}

/// <summary>The Win32 <c>MONITORINFOEXW</c>.</summary>
/// <remarks>
/// <see cref="CharSet.Unicode"/> is load-bearing, not decoration. <see cref="char"/> is not
/// blittable, so .NET Framework's marshaller converts the device-name buffer according to
/// the character set, and the default for a structure is ANSI: the name would come back
/// mangled, and the native buffer would be 72 bytes while <c>sizeof</c> told Windows it had
/// 104 to fill.
/// </remarks>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal unsafe struct MonitorInfoEx
{
    /// <summary>The value of <c>MONITORINFOF_PRIMARY</c>.</summary>
    public const uint PrimaryFlag = 1;

    /// <summary>The value of <c>CCHDEVICENAME</c>.</summary>
    public const int DeviceNameLength = 32;

    public int Size;
    public Rect Monitor;
    public Rect Work;
    public uint Flags;
    public fixed char DeviceName[DeviceNameLength];

    /// <summary>Creates a structure with its <see cref="Size"/> filled in.</summary>
    public static MonitorInfoEx Create() => new() { Size = sizeof(MonitorInfoEx) };
}
