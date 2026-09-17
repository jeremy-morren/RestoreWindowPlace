using FluentAssertions;
using Xunit;

namespace RestoreWindowPosition.Tests;

public class ScreenRectTests
{
    [Fact]
    public void A_rectangle_reports_the_size_its_edges_imply()
    {
        var rect = new ScreenRect(-100, -50, 700, 550);

        rect.Width.Should().Be(800);
        rect.Height.Should().Be(600);
        rect.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void FromSize_and_the_edge_constructor_agree() =>
        ScreenRect.FromSize(-100, -50, 800, 600).Should().Be(new ScreenRect(-100, -50, 700, 550));

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(10, 10, 10, 20)]
    [InlineData(10, 10, 20, 10)]
    [InlineData(10, 10, 5, 5)]
    public void A_rectangle_enclosing_no_pixels_is_empty(int left, int top, int right, int bottom) =>
        new ScreenRect(left, top, right, bottom).IsEmpty.Should().BeTrue();

    [Fact]
    public void Contains_is_about_enclosure_not_overlap()
    {
        var monitor = new ScreenRect(0, 0, 1920, 1080);

        monitor.Contains(new ScreenRect(0, 0, 1920, 1080)).Should().BeTrue();
        monitor.Contains(new ScreenRect(100, 100, 200, 200)).Should().BeTrue();
        monitor.Contains(new ScreenRect(-1, 0, 100, 100)).Should().BeFalse();
        monitor.Contains(new ScreenRect(1900, 100, 2000, 200)).Should().BeFalse();
    }

    [Fact]
    public void Rectangles_that_only_touch_along_an_edge_do_not_overlap()
    {
        var left = new ScreenRect(0, 0, 100, 100);

        left.IntersectsWith(new ScreenRect(100, 0, 200, 100)).Should().BeFalse();
        left.IntersectsWith(new ScreenRect(99, 0, 200, 100)).Should().BeTrue();
    }

    [Fact]
    public void Intersect_returns_the_overlapping_region() =>
        new ScreenRect(0, 0, 100, 100).Intersect(new ScreenRect(50, 50, 200, 200))
            .Should().Be(new ScreenRect(50, 50, 100, 100));

    [Fact]
    public void Equality_compares_edges()
    {
        (new ScreenRect(1, 2, 3, 4) == new ScreenRect(1, 2, 3, 4)).Should().BeTrue();
        (new ScreenRect(1, 2, 3, 4) != new ScreenRect(1, 2, 3, 5)).Should().BeTrue();
        new ScreenRect(1, 2, 3, 4).GetHashCode().Should().Be(new ScreenRect(1, 2, 3, 4).GetHashCode());
        new ScreenRect(1, 2, 3, 4).Equals("not a rectangle").Should().BeFalse();
    }
}

public class WindowPositionTests
{
    [Fact]
    public void A_placement_is_normal_unless_told_otherwise() =>
        new WindowPosition(1, 2, 3, 4).State.Should().Be(WindowShowState.Normal);

    [Fact]
    public void Bounds_follow_the_position_and_size() =>
        new WindowPosition(100, 200, 800, 600).Bounds.Should().Be(new ScreenRect(100, 200, 900, 800));

    [Theory]
    [InlineData(800, 600, true)]
    [InlineData(0, 600, false)]
    [InlineData(800, 0, false)]
    [InlineData(0, 0, false)]
    [InlineData(-1, 600, false)]
    public void HasSize_says_whether_restoring_should_resize(int width, int height, bool expected) =>
        new WindowPosition(0, 0, width, height).HasSize.Should().Be(expected);

    [Fact]
    public void Equality_compares_every_field()
    {
        var position = new WindowPosition(1, 2, 3, 4, WindowShowState.Maximized);

        (position == new WindowPosition(1, 2, 3, 4, WindowShowState.Maximized)).Should().BeTrue();
        (position != new WindowPosition(1, 2, 3, 4, WindowShowState.Minimized)).Should().BeTrue();
        (position != new WindowPosition(1, 2, 3, 5, WindowShowState.Maximized)).Should().BeTrue();
        position.GetHashCode().Should()
            .Be(new WindowPosition(1, 2, 3, 4, WindowShowState.Maximized).GetHashCode());
        position.Equals("not a placement").Should().BeFalse();
    }
}

public class MonitorInfoTests
{
    [Fact]
    public void A_monitor_needs_a_device_name() =>
        FluentActions.Invoking(() => new MonitorInfo(null!, default, default, isPrimary: false))
            .Should().Throw<ArgumentNullException>();

    [Fact]
    public void Equality_compares_every_field()
    {
        var monitor = new MonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1920, 1040), isPrimary: true);

        (monitor == new MonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1920, 1040), isPrimary: true)).Should().BeTrue();
        (monitor != new MonitorInfo(@"\\.\DISPLAY2", new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1920, 1040), isPrimary: true)).Should().BeTrue();
        (monitor != new MonitorInfo(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080),
            new ScreenRect(0, 0, 1920, 1040), isPrimary: false)).Should().BeTrue();
    }
}
