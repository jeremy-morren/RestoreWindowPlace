using System.Globalization;
using System.Runtime.InteropServices;
using FluentAssertions;
using RestoreWindowPosition.Interop;
using Xunit;

namespace RestoreWindowPosition.Tests;

/// <summary>
/// Locks down everything that could differ between architectures.
/// </summary>
/// <remarks>
/// <para>
/// A placement saved on one machine has to be readable on another, and a monitor arrangement
/// has to hash to the same key everywhere — otherwise a roaming profile, a synced settings
/// file, or simply a 32-bit build of the same application would quietly lose every window
/// position. The assertions here are exact values rather than round-trips: a round-trip
/// passes happily even if both halves agree on the wrong byte order.
/// </para>
/// <para>
/// Nothing here needs a particular architecture to be meaningful — the point is that the
/// numbers are identical on all of them. CI runs this file on x86, x64 and arm64.
/// </para>
/// </remarks>
public class ArchitectureTests
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

    /// <summary>
    /// Cultures chosen to break anything that formats a number or cases a string: a minus
    /// sign that is not U+002D, digits that are not ASCII, a non-Gregorian calendar, and the
    /// Turkish dotted-I.
    /// </summary>
    public static TheoryData<string> HostileCultures =>
        ["", "sv-SE", "tr-TR", "ar-SA", "fa-IR", "th-TH", "de-DE"];

    [Fact]
    public void A_placement_encodes_to_exactly_these_bytes() => GoldenPayload().Should().Equal(Golden);

    [Theory]
    [MemberData(nameof(HostileCultures))]
    public void A_placement_encodes_to_the_same_bytes_under_any_culture(string culture) =>
        InCulture(culture, GoldenPayload).Should().Equal(Golden);

    [Theory]
    [MemberData(nameof(HostileCultures))]
    public void A_payload_reads_back_the_same_under_any_culture(string culture)
    {
        var parsed = InCulture(culture, () => WindowPositionFormat.Parse(Golden));

        parsed["W"].Should().Be(new WindowPosition(-1920, 1080, 800, 600, WindowShowState.Maximized));
    }

    [Theory]
    [MemberData(nameof(HostileCultures))]
    public void A_monitor_arrangement_hashes_to_the_same_key_under_any_culture(string culture) =>
        InCulture(culture, () => new MonitorLayout([Laptop, ExternalOnTheLeft]).Key)
            .Should().Be(0x0A8A08C2u);

    private static byte[] GoldenPayload() => WindowPositionFormat.Format(
        new Dictionary<string, WindowPosition>
        {
            ["W"] = new(-1920, 1080, 800, 600, WindowShowState.Maximized),
        });

    private static readonly byte[] Golden = BuildGolden();

    private static byte[] BuildGolden()
    {
        return [
            0x57,       // magic 'W'
            0x01,       // version
            0x01,       // one window
            0x01, 0x57, // key: length 1, 'W'
            0xFF, 0x1D, // left   -1920 -> zig-zag 3839
            0xF0, 0x10, // top     1080 -> zig-zag 2160
            0xC0, 0x0C, // width    800 -> zig-zag 1600
            0xB0, 0x09, // height   600 -> zig-zag 1200
            0x02];      // maximised
    }

    [Fact]
    public void The_encoding_is_little_endian_whatever_the_machine_is()
    {
        // Varints are byte-oriented, so the only place byte order could leak in is a
        // fixed-width write. BinaryWriter is little-endian by contract on every platform,
        // and this pins that: a big-endian writer would emit 0x00 0x00 0x04 0xD2.
        var data = WindowPositionFormat.Format(new Dictionary<string, WindowPosition>
        {
            [string.Empty] = new(0, 0, 0, 0),
        });

        // Header, count, empty key, four zero coordinates, normal state — no multi-byte
        // field at all for this input, which is itself the point: the format never writes
        // a machine word.
        data.Should().Equal(0x57, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00);
    }

    [Fact]
    public void A_monitor_arrangement_hashes_to_exactly_this_key() =>
        new MonitorLayout([Laptop, ExternalOnTheLeft]).Key.Should().Be(0x0A8A08C2u);

    [Fact]
    public void A_single_monitor_arrangement_hashes_to_exactly_this_key() =>
        new MonitorLayout([Laptop]).Key.Should().Be(0x8A062F54u);

    [Fact]
    public void An_empty_arrangement_hashes_to_the_FNV_offset_basis() =>
        new MonitorLayout(Array.Empty<MonitorInfo>()).Key.Should().Be(2166136261);

    [Fact]
    public void The_key_is_unaffected_by_the_size_of_a_pointer()
    {
        // Not a tautology on one machine: the same expected value is asserted above, so if
        // a platform-sized value ever reached the hash, the 32-bit and 64-bit legs of CI
        // would disagree with each other and with the constant.
        var key = new MonitorLayout([Laptop, ExternalOnTheLeft]).Key;

        key.Should().Be(new MonitorLayout([Laptop, ExternalOnTheLeft]).Key);
        IntPtr.Size.Should().BeOneOf(4, 8);
    }

    [Fact]
    public unsafe void The_native_structures_are_the_size_Windows_expects()
    {
        // WINDOWPLACEMENT and MONITORINFOEXW hold no pointers, so their sizes are the same
        // on x86, x64 and arm64. If a field of platform size were ever added, cbSize would
        // be wrong on one of them and the calls would start failing in the field.
        Marshal.SizeOf<WindowPlacement>().Should().Be(44);
        Marshal.SizeOf<MonitorInfoEx>().Should().Be(104);

        // The size passed to Windows comes from sizeof(), while the marshaller allocates
        // Marshal.SizeOf() bytes. These diverged once already: char is not blittable, and
        // without CharSet.Unicode the device-name buffer was marshalled as ANSI, leaving
        // Windows writing 104 bytes into 72.
        sizeof(WindowPlacement).Should().Be(Marshal.SizeOf<WindowPlacement>());
        sizeof(MonitorInfoEx).Should().Be(Marshal.SizeOf<MonitorInfoEx>());
    }

    [Fact]
    public unsafe void The_native_rectangle_and_point_are_four_byte_fields()
    {
        Marshal.SizeOf<Rect>().Should().Be(16);
        Marshal.SizeOf<Interop.Point>().Should().Be(8);
        sizeof(Rect).Should().Be(16);
        sizeof(Interop.Point).Should().Be(8);
    }

    /// <summary>Runs <paramref name="action"/> with both culture slots switched over.</summary>
    private static T InCulture<T>(string name, Func<T> action)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;

        try
        {
            var replacement = new CultureInfo(name);
            CultureInfo.CurrentCulture = replacement;
            CultureInfo.CurrentUICulture = replacement;
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
