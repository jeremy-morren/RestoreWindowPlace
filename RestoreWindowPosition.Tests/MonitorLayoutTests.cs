using System.Globalization;
using FluentAssertions;
using Xunit;

namespace RestoreWindowPosition.Tests;

public class MonitorLayoutTests
{
    private static readonly MonitorInfo Laptop = new(
        @"\\.\DISPLAY1",
        new ScreenRect(0, 0, 1920, 1080),
        new ScreenRect(0, 0, 1920, 1040),
        isPrimary: true);

    private static readonly MonitorInfo ExternalOnTheLeft = new(
        @"\\.\DISPLAY2",
        new ScreenRect(-2560, -200, 0, 1240),
        new ScreenRect(-2560, -200, 0, 1240),
        isPrimary: false);

    private static readonly MonitorInfo ExternalOnTheRight = new(
        @"\\.\DISPLAY2",
        new ScreenRect(1920, -200, 4480, 1240),
        new ScreenRect(1920, -200, 4480, 1240),
        isPrimary: false);

    [Fact]
    public void Same_arrangementProducesSame_key() =>
        new MonitorLayout([Laptop, ExternalOnTheLeft]).Key
            .Should().Be(new MonitorLayout([Laptop, ExternalOnTheLeft]).Key);

    [Fact]
    public void Enumeration_orderDoesNotAffectThe_key() =>
        new MonitorLayout([ExternalOnTheLeft, Laptop]).Key
            .Should().Be(new MonitorLayout([Laptop, ExternalOnTheLeft]).Key);

    [Fact]
    public void Moving_monitorChanges_key() =>
        new MonitorLayout([Laptop, ExternalOnTheRight]).Key
            .Should().NotBe(new MonitorLayout([Laptop, ExternalOnTheLeft]).Key);

    [Fact]
    public void Undocking_changesThe_key() =>
        new MonitorLayout([Laptop]).Key
            .Should().NotBe(new MonitorLayout([Laptop, ExternalOnTheRight]).Key);

    [Fact]
    public void Moving_taskbarChanges_key()
    {
        var taskbarOnTheLeft = new MonitorLayout(
        [
            new MonitorInfo(Laptop.DeviceName, Laptop.Bounds, new ScreenRect(40, 0, 1920, 1080), isPrimary: true),
        ]);

        taskbarOnTheLeft.Key.Should().NotBe(new MonitorLayout([Laptop]).Key);
    }

    [Fact]
    public void Primary_monitorChanges_key()
    {
        var externalPrimary = new MonitorLayout(
        [
            new MonitorInfo(Laptop.DeviceName, Laptop.Bounds, Laptop.WorkArea, isPrimary: false),
            new MonitorInfo(
                ExternalOnTheRight.DeviceName,
                ExternalOnTheRight.Bounds,
                ExternalOnTheRight.WorkArea,
                isPrimary: true),
        ]);

        externalPrimary.Key.Should().NotBe(new MonitorLayout([Laptop, ExternalOnTheRight]).Key);
    }

    [Fact]
    public void Renaming_displayDeviceDoesNotChange_key()
    {
        // Windows reassigns DISPLAY1/DISPLAY2 as monitors come and go, so a key that moved
        // with the name would split one physical arrangement into several.
        var renamed = new MonitorLayout(
        [
            new MonitorInfo(@"\\.\DISPLAY7", Laptop.Bounds, Laptop.WorkArea, isPrimary: true),
        ]);

        renamed.Key.Should().Be(new MonitorLayout([Laptop]).Key);
    }

    [Fact]
    public void Key_isCulture_invariant()
    {
        var swedish = InCulture("sv-SE", () => new MonitorLayout([Laptop, ExternalOnTheLeft]).Key);
        var american = InCulture("en-US", () => new MonitorLayout([Laptop, ExternalOnTheLeft]).Key);

        swedish.Should().Be(american);
    }

    [Fact]
    public void Monitors_areReportedInAStable_order() =>
        new MonitorLayout([Laptop, ExternalOnTheLeft]).Monitors
            .Should().Equal(ExternalOnTheLeft, Laptop);

    [Fact]
    public void Empty_arrangementIs_allowed() =>
        new MonitorLayout(Array.Empty<MonitorInfo>()).Monitors.Should().BeEmpty();

    [Fact]
    public void Null_monitorsAre_rejected()
    {
        var create = () => new MonitorLayout(null!);

        create.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    // Fully inside the laptop's work area.
    [InlineData(100, 100, 400, 300, true)]
    // Overlapping the work area by a single pixel column.
    [InlineData(-400, 100, 401, 300, true)]
    // In the strip the taskbar occupies, which is not part of any work area.
    [InlineData(100, 1040, 400, 40, false)]
    // Off the far right of every monitor.
    [InlineData(9000, 100, 400, 300, false)]
    public void IsVisible_reports_reachability(
        int left, int top, int width, int height, bool expected) =>
        new MonitorLayout([Laptop])
            .IsVisible(ScreenRect.FromSize(left, top, width, height))
            .Should().Be(expected);

    [Fact]
    public void Attached_monitorsCanBeReadFromOperating_system()
    {
        Desktop.Require();

        var layout = MonitorLayout.GetCurrent();

        layout.Monitors.Should().NotBeEmpty();
        layout.Monitors.Should().ContainSingle(monitor => monitor.IsPrimary);

        foreach (var monitor in layout.Monitors)
        {
            monitor.Bounds.IsEmpty.Should().BeFalse();
            monitor.WorkArea.IsEmpty.Should().BeFalse();
            monitor.Bounds.Contains(monitor.WorkArea).Should().BeTrue();
            monitor.DeviceName.Should().StartWith(@"\\.\");
        }

        // The primary monitor's origin is the origin of the virtual screen, so a window just
        // inside it must read as visible. If this fails, a predicate written against
        // IsVisible would silently refuse to restore anything.
        layout.IsVisible(ScreenRect.FromSize(100, 100, 400, 300)).Should().BeTrue();
    }

    [Fact]
    public void Real_multiMonitorArrangementIs_coherent()
    {
        Desktop.Require();

        var layout = MonitorLayout.GetCurrent();
        Assert.SkipUnless(layout.Monitors.Count > 1, "Only one monitor is attached.");

        // Windows lays monitors out side-by-side on the virtual desktop;
        // two that overlapped would mean the bounds are being read in the wrong DPI context, or not at all.
        for (var i = 0; i < layout.Monitors.Count; i++)
        {
            for (var j = i + 1; j < layout.Monitors.Count; j++)
            {
                layout.Monitors[i].Bounds.IntersectsWith(layout.Monitors[j].Bounds)
                    .Should().BeFalse("monitors {0} and {1} must not overlap", i, j);
            }
        }

        // Losing a monitor has to change the key
        new MonitorLayout(layout.Monitors.Take(1)).Key.Should().NotBe(layout.Key);
    }

    [Fact]
    public void Reading_attachedMonitorsTwiceGivesSame_key() =>
        MonitorLayout.GetCurrent().Key.Should().Be(MonitorLayout.GetCurrent().Key);

    [Fact]
    public void Key_isDpiAwareness_invariant()
    {
        Desktop.Require();

        // Windows reports monitor rectangles scaled to the caller's DPI context, and a WPF
        // application changes its own context partway through startup. If that leaked into
        // the key, an application would load its placements under one key and save them under
        // another, and would never restore a window again.
        var asIs = MonitorLayout.GetCurrent();

        var restore = Interop.NativeMethods.SetThreadDpiAwarenessContext(DpiAwarenessContextUnaware);
        Assert.SkipWhen(restore == IntPtr.Zero, "Windows 8.1 or earlier has no per-thread DPI context.");

        try
        {
            var unaware = MonitorLayout.GetCurrent();

            unaware.Key.Should().Be(asIs.Key);
            unaware.Monitors.Should().Equal(asIs.Monitors);
        }
        finally
        {
            Interop.NativeMethods.SetThreadDpiAwarenessContext(restore);
        }
    }

    /// <summary>The value of <c>DPI_AWARENESS_CONTEXT_UNAWARE</c>.</summary>
    private static readonly IntPtr DpiAwarenessContextUnaware = new(-1);

    private static T InCulture<T>(string name, Func<T> action)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}

