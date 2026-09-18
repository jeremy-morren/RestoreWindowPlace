using System.IO;
using FluentAssertions;
using Xunit;

namespace RestoreWindowPosition.Tests;

public class StreamBinaryReaderWriterTests
{
    [Fact]
    public void WriteInt32_writesLittleEndian_bytes()
    {
        using var stream = new MemoryStream();

        stream.WriteInt32(0x12345678);

        stream.ToArray().Should().Equal(0x78, 0x56, 0x34, 0x12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void Int32_round_trips(int value)
    {
        using var stream = new MemoryStream();
        stream.WriteInt32(value);
        stream.Position = 0;

        stream.ReadInt32().Should().Be(value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bool_round_trips(bool value)
    {
        using var stream = new MemoryStream();
        stream.WriteBool(value);
        stream.Position = 0;

        stream.ReadBool().Should().Be(value);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(127u)]
    [InlineData(128u)]
    [InlineData(300u)]
    [InlineData(uint.MaxValue)]
    public void VarUInt32_round_trips(uint value)
    {
        using var stream = new MemoryStream();
        stream.WriteVarUInt32(value);
        stream.Position = 0;

        stream.ReadVarUInt32().Should().Be(value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void VarInt32_round_trips(int value)
    {
        using var stream = new MemoryStream();
        stream.WriteVarInt32(value);
        stream.Position = 0;

        stream.ReadVarInt32().Should().Be(value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("unicode ✓ κλειδί")]
    public void Utf8_round_trips(string value)
    {
        using var stream = new MemoryStream();
        stream.WriteUtf8(value);
        stream.Position = 0;

        stream.ReadUtf8().Should().Be(value);
    }

    [Fact]
    public void ReadBytesExactly_returnsRequested_bytes()
    {
        using var stream = new MemoryStream([1, 2, 3, 4]);

        stream.ReadBytesExactly(3).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ReadBytesExactly_throwsAtEndOf_stream()
    {
        var stream = new MemoryStream([1, 2]);

        FluentActions.Invoking(() => stream.ReadBytesExactly(3))
            .Should().Throw<EndOfStreamException>();
    }

    [Fact]
    public void Read1Byte_throwsAtEndOf_stream()
    {
        var stream = new MemoryStream();

        FluentActions.Invoking(stream.Read1Byte)
            .Should().Throw<EndOfStreamException>();
    }

    [Fact]
    public void ReadVarUInt32_throwsForMalformed_sequence()
    {
        var stream = new MemoryStream([0x80, 0x80, 0x80, 0x80, 0x80]);

        FluentActions.Invoking(stream.ReadVarUInt32)
            .Should().Throw<FormatException>();
    }
}


