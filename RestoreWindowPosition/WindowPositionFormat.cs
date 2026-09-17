using System.IO;
using System.Text;

namespace RestoreWindowPosition;

/// <summary>
/// Turns window placements into the bytes an <see cref="IWindowPositionStore"/> holds, and back.
/// </summary>
/// <remarks>
/// <para>
/// The payload is a two-byte header followed by a count and one record per window:
/// </para>
/// <code>
/// byte     magic      'W'
/// byte     version    1
/// varint   count
/// per window:
///   string key        length-prefixed UTF-8, as BinaryWriter writes it
///   varint left       zig-zag encoded
///   varint top        zig-zag encoded
///   varint width      zig-zag encoded
///   varint height     zig-zag encoded
///   byte   state      0 normal, 1 minimised, 2 maximised
/// </code>
/// <para>
/// Written and read by hand with <see cref="BinaryWriter"/> and <see cref="BinaryReader"/>:
/// no serializer, no reflection, no attributes, and nothing a trimmer or an ahead-of-time
/// compiler has to be told about. Nothing of the shape of these types is written down, so
/// the bytes are small — a typical window costs around twenty of them — and the encoding
/// does not depend on the current culture.
/// </para>
/// <para>
/// Numbers are zig-zag varints: small magnitudes, positive or negative, take one byte, and
/// the coordinates of a window on a second monitor to the left of the primary cost no more
/// than those of one on the primary itself.
/// </para>
/// </remarks>
public static class WindowPositionFormat
{
    /// <summary>The first byte of a well-formed payload.</summary>
    public const byte Magic = (byte)'W';

    /// <summary>The version of the encoding this type writes.</summary>
    public const byte Version = 1;

    /// <summary>Writes placements as bytes.</summary>
    /// <param name="positions">The placements to write, keyed by window key.</param>
    /// <returns>
    /// The payload, with records ordered by key so that the same placements always produce
    /// identical bytes.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="positions"/> is <see langword="null"/>.</exception>
    public static byte[] Format(IEnumerable<KeyValuePair<string, WindowPosition>> positions)
    {
        if (positions is null) throw new ArgumentNullException(nameof(positions));

        var ordered = new List<KeyValuePair<string, WindowPosition>>(positions);
        ordered.Sort(static (x, y) => string.CompareOrdinal(x.Key, y.Key));

        using var buffer = new MemoryStream();

        // UTF8Encoding without a byte order mark: BinaryWriter would otherwise have one
        // ready to emit, and the length prefix already says where each string ends.
        using (var writer = new BinaryWriter(buffer, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            WriteVarUInt32(writer, (uint)ordered.Count);

            foreach (var entry in ordered)
            {
                writer.Write(entry.Key);
                WriteVarInt32(writer, entry.Value.Left);
                WriteVarInt32(writer, entry.Value.Top);
                WriteVarInt32(writer, entry.Value.Width);
                WriteVarInt32(writer, entry.Value.Height);
                writer.Write((byte)entry.Value.State);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Reads placements back from bytes.</summary>
    /// <param name="data">A payload previously produced by <see cref="Format"/>, or <see langword="null"/>.</param>
    /// <returns>
    /// The placements. Reading is deliberately forgiving: a <see langword="null"/>, empty or
    /// unrecognised payload yields an empty result, and a payload that is truncated or
    /// damaged part-way through yields the records read up to that point. A settings file
    /// that has been damaged should cost the user their window positions, not their session.
    /// </returns>
    public static Dictionary<string, WindowPosition> Parse(byte[]? data)
    {
        var result = new Dictionary<string, WindowPosition>(StringComparer.Ordinal);
        if (data is null || data.Length < 3) return result;

        // Anything that does not open with our header is some other payload, or a version
        // we do not understand. Either way, do not guess at its contents.
        if (data[0] != Magic || data[1] != Version) return result;

        try
        {
            // Opened past the header, rather than seeking, because BinaryReader is free to
            // buffer ahead of the stream position it was handed.
            using var buffer = new MemoryStream(data, index: 2, count: data.Length - 2, writable: false);
            using var reader = new BinaryReader(buffer, new UTF8Encoding(false));

            var count = ReadVarUInt32(reader);

            for (var i = 0u; i < count; i++)
            {
                var key = reader.ReadString();
                var left = ReadVarInt32(reader);
                var top = ReadVarInt32(reader);
                var width = ReadVarInt32(reader);
                var height = ReadVarInt32(reader);
                var state = ToState(reader.ReadByte());

                result[key] = new WindowPosition(left, top, width, height, state);
            }
        }
        catch (Exception e) when (e is EndOfStreamException or IOException or FormatException or ArgumentException)
        {
            // Truncated, or a length prefix that ran off the end. Keep what was read.
        }

        return result;
    }

    // Switched by hand rather than cast, so that a byte from a damaged payload cannot
    // produce a WindowShowState with no name.
    private static WindowShowState ToState(byte value) => value switch
    {
        (byte)WindowShowState.Minimized => WindowShowState.Minimized,
        (byte)WindowShowState.Maximized => WindowShowState.Maximized,
        _ => WindowShowState.Normal,
    };

    /// <summary>Writes an unsigned value seven bits at a time, least significant first.</summary>
    /// <remarks>
    /// <c>BinaryWriter.Write7BitEncodedInt</c> would do this, but it is only public from
    /// .NET 5 onwards and this has to encode identically on .NET Framework.
    /// </remarks>
    private static void WriteVarUInt32(BinaryWriter writer, uint value)
    {
        while (value >= 0x80)
        {
            writer.Write((byte)(value | 0x80));
            value >>= 7;
        }

        writer.Write((byte)value);
    }

    private static uint ReadVarUInt32(BinaryReader reader)
    {
        var result = 0u;

        for (var shift = 0; shift <= 28; shift += 7)
        {
            var b = reader.ReadByte();
            result |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
        }

        throw new FormatException("Malformed variable-length integer.");
    }

    /// <summary>Writes a signed value zig-zag encoded, so that small negatives stay small.</summary>
    private static void WriteVarInt32(BinaryWriter writer, int value) =>
        WriteVarUInt32(writer, (uint)((value << 1) ^ (value >> 31)));

    private static int ReadVarInt32(BinaryReader reader)
    {
        var encoded = ReadVarUInt32(reader);
        return (int)(encoded >> 1) ^ -(int)(encoded & 1);
    }
}
