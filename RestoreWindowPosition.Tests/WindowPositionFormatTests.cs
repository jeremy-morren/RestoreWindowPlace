using System.Globalization;
using System.Text;
using FluentAssertions;
using Xunit;

namespace RestoreWindowPosition.Tests;

public class WindowPositionFormatTests
{
    [Fact]
    public void An_empty_payload_is_a_header_and_a_zero_count() =>
        WindowPositionFormat.Format(new Dictionary<string, WindowPosition>())
            .Should().Equal(WindowPositionFormat.Magic, WindowPositionFormat.Version, 0);

    [Fact]
    public void A_window_costs_about_twenty_bytes()
    {
        var data = WindowPositionFormat.Format(new Dictionary<string, WindowPosition>
        {
            ["MainWindow"] = new(100, 200, 800, 600),
        });

        // 2 header + 1 count + 11 key + 2+2+2+2 coordinates + 1 state.
        data.Should().HaveCount(23);
    }

    [Fact]
    public void Coordinates_on_a_monitor_left_of_the_primary_cost_no_more_than_positive_ones()
    {
        var onThePrimary = WindowPositionFormat.Format(new Dictionary<string, WindowPosition>
        {
            ["W"] = new(1000, 200, 800, 600),
        });

        var onTheOneToTheLeft = WindowPositionFormat.Format(new Dictionary<string, WindowPosition>
        {
            ["W"] = new(-1000, 200, 800, 600),
        });

        onTheOneToTheLeft.Should().HaveCount(onThePrimary.Length);
    }

    [Fact]
    public void Format_orders_by_key_so_the_same_placements_produce_the_same_bytes()
    {
        var forwards = WindowPositionFormat.Format(new List<KeyValuePair<string, WindowPosition>>
        {
            new("a", new WindowPosition(1, 2, 3, 4)),
            new("b", new WindowPosition(5, 6, 7, 8)),
        });

        var backwards = WindowPositionFormat.Format(new List<KeyValuePair<string, WindowPosition>>
        {
            new("b", new WindowPosition(5, 6, 7, 8)),
            new("a", new WindowPosition(1, 2, 3, 4)),
        });

        backwards.Should().Equal(forwards);
    }

    [Theory]
    [InlineData(WindowShowState.Normal)]
    [InlineData(WindowShowState.Minimized)]
    [InlineData(WindowShowState.Maximized)]
    public void Placements_round_trip(WindowShowState state)
    {
        var original = new WindowPosition(-1280, -37, 1024, 768, state);

        RoundTrip("W", original)["W"].Should().Be(original);
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(int.MaxValue, int.MinValue, int.MaxValue, int.MinValue)]
    [InlineData(-1, 1, -1, 1)]
    [InlineData(-32768, 32767, 65535, -65536)]
    public void Any_coordinate_round_trips(int left, int top, int width, int height)
    {
        var original = new WindowPosition(left, top, width, height);

        RoundTrip("W", original)["W"].Should().Be(original);
    }

    [Theory]
    [InlineData("MainWindow")]
    [InlineData("has|a|separator")]
    [InlineData(@"has\a\backslash")]
    [InlineData("has\r\na newline")]
    [InlineData("#looks like a comment")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unicode ✓ κλειδί")]
    public void Keys_round_trip_whatever_they_contain(string key)
    {
        var parsed = RoundTrip(key, new WindowPosition(1, 2, 3, 4));

        parsed.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, WindowPosition>(key, new WindowPosition(1, 2, 3, 4)));
    }

    [Fact]
    public void Many_windows_round_trip_together()
    {
        var original = new Dictionary<string, WindowPosition>(StringComparer.Ordinal);
        for (var i = 0; i < 64; i++)
        {
            original["Window" + i.ToString(CultureInfo.InvariantCulture)] =
                new WindowPosition(i * 10, i * -10, 800 + i, 600 + i, (WindowShowState)(i % 3));
        }

        WindowPositionFormat.Parse(WindowPositionFormat.Format(original))
            .Should().Equal(original);
    }

    [Fact]
    public void Keys_are_compared_ordinally()
    {
        var parsed = WindowPositionFormat.Parse(
            WindowPositionFormat.Format(new Dictionary<string, WindowPosition>(StringComparer.Ordinal)
            {
                ["Window"] = new(1, 1, 1, 1),
                ["window"] = new(2, 2, 2, 2),
            }));

        parsed.Should().HaveCount(2);
        parsed["Window"].Should().Be(new WindowPosition(1, 1, 1, 1));
        parsed["window"].Should().Be(new WindowPosition(2, 2, 2, 2));
    }

    [Fact]
    public void Parse_of_nothing_yields_nothing()
    {
        WindowPositionFormat.Parse(null).Should().BeEmpty();
        WindowPositionFormat.Parse([]).Should().BeEmpty();
    }

    [Fact]
    public void Parse_refuses_to_guess_at_a_payload_it_does_not_recognise()
    {
        // A truncated header, the wrong magic, and a version from the future.
        WindowPositionFormat.Parse([WindowPositionFormat.Magic, WindowPositionFormat.Version])
            .Should().BeEmpty();
        WindowPositionFormat.Parse([0x3C, WindowPositionFormat.Version, 0])
            .Should().BeEmpty();
        WindowPositionFormat.Parse([WindowPositionFormat.Magic, 99, 0])
            .Should().BeEmpty();

        // The DataContract XML this library used to write.
        WindowPositionFormat.Parse(Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><ArrayOfKeyValue />"))
            .Should().BeEmpty();
    }

    [Fact]
    public void A_truncated_payload_keeps_the_windows_that_were_read()
    {
        var full = WindowPositionFormat.Format(new Dictionary<string, WindowPosition>(StringComparer.Ordinal)
        {
            ["A"] = new(1, 1, 1, 1),
            ["B"] = new(2, 2, 2, 2),
            ["C"] = new(3, 3, 3, 3),
        });

        // Chop the last record in half; the count still claims three.
        var parsed = WindowPositionFormat.Parse(full.Take(full.Length - 5).ToArray());

        parsed.Keys.Should().BeEquivalentTo(["A", "B"]);
    }

    [Fact]
    public void Random_trailing_damage_does_not_throw()
    {
        var full = WindowPositionFormat.Format(new Dictionary<string, WindowPosition> { ["A"] = new(1, 1, 1, 1) });
        var random = new Random(20260917);

        for (var i = 0; i < 500; i++)
        {
            var damaged = (byte[])full.Clone();
            random.NextBytes(damaged);
            damaged[0] = WindowPositionFormat.Magic;
            damaged[1] = WindowPositionFormat.Version;

            // The contract is that a damaged file loses positions, never the session.
            var parse = () => WindowPositionFormat.Parse(damaged);
            parse.Should().NotThrow();
        }
    }

    [Fact]
    public void A_show_state_that_is_not_one_of_ours_reads_back_as_normal()
    {
        var data = WindowPositionFormat.Format(new Dictionary<string, WindowPosition> { ["W"] = new(1, 2, 3, 4) });
        data[data.Length - 1] = 200;

        WindowPositionFormat.Parse(data)["W"].State.Should().Be(WindowShowState.Normal);
    }

    [Fact]
    public void A_payload_written_under_one_culture_reads_back_under_another()
    {
        var data = InCulture("sv-SE", () => WindowPositionFormat.Format(
            new Dictionary<string, WindowPosition> { ["W"] = new(-1920, -1080, 800, 600) }));

        var parsed = InCulture("en-US", () => WindowPositionFormat.Parse(data));

        parsed["W"].Should().Be(new WindowPosition(-1920, -1080, 800, 600));
    }

    [Fact]
    public void Null_placements_are_rejected()
    {
        var format = () => WindowPositionFormat.Format(null!);

        format.Should().Throw<ArgumentNullException>();
    }

    private static Dictionary<string, WindowPosition> RoundTrip(string key, WindowPosition position) =>
        WindowPositionFormat.Parse(
            WindowPositionFormat.Format(new Dictionary<string, WindowPosition> { [key] = position }));

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
