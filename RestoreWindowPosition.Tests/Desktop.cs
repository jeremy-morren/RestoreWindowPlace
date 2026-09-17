namespace RestoreWindowPosition.Tests;

/// <summary>
/// The gate that tests needing a real desktop go through.
/// </summary>
/// <remarks>
/// Most of the suite is pure logic and runs anywhere. A handful genuinely need the operating
/// system — reading the attached monitors, creating a real window. Those fail rather than
/// skip when there is no desktop: an agent with no display cannot verify them, and a green
/// run that quietly proved nothing about the half of this library that talks to Win32 is
/// worse than a red one.
/// </remarks>
internal static class Desktop
{
    /// <summary>Throws when no monitor is attached.</summary>
    /// <exception cref="InvalidOperationException">The desktop reports no monitors.</exception>
    public static void Require()
    {
        if (MonitorLayout.GetCurrent().Monitors.Count > 0) return;

        throw new InvalidOperationException(
            "No monitors are attached, so the tests that exercise the real desktop cannot be " +
            "verified. Run them on a machine or build agent that has a display.");
    }
}
