using System.IO;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo

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

        using var writer = new MemoryStream();
        
        writer.Write(Magic);
        writer.Write(Version);
        writer.WriteVarUInt32((uint)ordered.Count);

        foreach (var entry in ordered)
        {
            writer.WriteUtf8(entry.Key);
            writer.WriteVarInt32(entry.Value.Left);
            writer.WriteVarInt32(entry.Value.Top);
            writer.WriteVarInt32(entry.Value.Width);
            writer.WriteVarInt32(entry.Value.Height);
            writer.Write((byte)entry.Value.State);
        }
        return writer.ToArray();
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
        if (data is null || data.Length < 3)
            return result;

        // Anything that does not open with our header is some other payload, or a version
        // we do not understand. Either way, do not guess at its contents.
        if (data[0] != Magic || data[1] != Version) 
            return result;

        try
        {
            using var stream = new MemoryStream(data);
            stream.Position = 2; // Skip the header

            var count = stream.ReadVarUInt32();

            for (var i = 0u; i < count; i++)
            {
                var key = stream.ReadUtf8();
                var left = stream.ReadVarInt32();
                var top = stream.ReadVarInt32();
                var width = stream.ReadVarInt32();
                var height = stream.ReadVarInt32();
                var state = ToState(stream.Read1Byte());

                result[key] = new WindowPosition(left, top, width, height, state);
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or FormatException or ArgumentException or OverflowException)
        {
            // Truncated, or a length prefix that ran off the end. Keep what was read.
        }

        return result;
    }

    /// <summary>
    /// Converts a byte read from a payload into a <see cref="WindowShowState"/>,
    /// defaulting to <see cref="WindowShowState.Normal"/> for unrecognised values.
    /// </summary>
    private static WindowShowState ToState(byte value) => value switch
    {
        (byte)WindowShowState.Minimized => WindowShowState.Minimized,
        (byte)WindowShowState.Maximized => WindowShowState.Maximized,
        _ => WindowShowState.Normal,
    };
}
