using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

// ReSharper disable GrammarMistakeInComment
// ReSharper disable UnusedMember.Global
// ReSharper disable MemberCanBePrivate.Global

namespace RestoreWindowPosition;

/// <summary>
/// Helpers for reading and writing binary data to/from a stream
/// in a way that is stable across platforms and .NET versions.
/// </summary>
internal static class StreamBinaryReaderWriter
{
    extension(Stream stream)
    {
        public void WriteInt32(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            ReverseIfBigEndian(bytes);
            stream.Write(bytes);
        }

        public int ReadInt32()
        {
            var bytes = stream.ReadBytesExactly(sizeof(int));
            ReverseIfBigEndian(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        public void WriteBool(bool value) => 
            stream.Write(value ? (byte)1 : (byte)0);

        public bool ReadBool() => 
            stream.Read1Byte() != 0;

        public void WriteUtf8(string value)
        {
            var bytes = Utf8NoBom.GetBytes(value);
            stream.WriteInt32(bytes.Length);
            stream.Write(bytes);
        }

        public string ReadUtf8()
        {
            var length = stream.ReadInt32();
            if (length < 0 || (stream.CanSeek && length > stream.Length - stream.Position))
                throw new FormatException("String length exceeds the remaining payload.");

            var bytes = stream.ReadBytesExactly(length);
            return Utf8NoBom.GetString(bytes);
        }

        /// <summary>
        /// Reads a signed integer that was written using zig-zag encoding.
        /// </summary>
        public int ReadVarInt32()
        {
            var encoded = stream.ReadVarUInt32();
            return (int)(encoded >> 1) ^ -(int)(encoded & 1);
        }

        /// <summary>
        /// Reads an unsigned integer that was written seven bits at a time, least significant first.
        /// </summary>
        public uint ReadVarUInt32()
        {
            var result = 0u;

            for (var shift = 0; shift <= 28; shift += 7)
            {
                var b = stream.ReadByte();
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                    return result;
            }

            throw new FormatException("Malformed variable-length integer.");
        }

        /// <summary>
        /// Writes an unsigned value seven bits at a time, least significant first.
        /// </summary>
        /// <remarks>
        /// <c>Stream.Write7BitEncodedInt</c> would do this, but it is only public from .NET 5 onwards
        /// </remarks>
        public void WriteVarUInt32(uint value)
        {
            while (value >= 0x80)
            {
                stream.Write((byte)(value | 0x80));
                value >>= 7;
            }

            stream.Write((byte)value);
        }

        /// <summary>
        /// Writes a signed value zig-zag encoded, so that small negatives stay small.
        /// </summary>
        public void WriteVarInt32(int value) => 
            stream.WriteVarUInt32((uint)((value << 1) ^ (value >> 31)));
        
        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/>, or throws if the stream ends first.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] ReadBytesExactly(int count)
        {
            var buffer = new byte[count];
            return stream.Read(buffer, 0, count) == count 
                ? buffer 
                : throw new EndOfStreamException();
        }

        /// <summary>
        /// Reads a single byte from <paramref name="stream"/>, or throws if the stream ends first.
        /// </summary>
        public byte Read1Byte()
        {
            var result = stream.ReadByte();
            if (result == -1)
                throw new EndOfStreamException();
            return (byte)result;
        }

        /// <summary>
        /// Writes a single byte to <paramref name="stream"/>.
        /// </summary>
        public void Write(byte @byte) => 
            stream.Write([@byte], 0, 1);
        
#if NETFRAMEWORK
        public void Write(byte[] buffer) => 
            stream.Write(buffer, 0, buffer.Length);
#endif
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ReverseIfBigEndian(byte[] bytes)
    {
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes);
    }
    
    /// <summary>
    /// UTF8-encoding without a byte order mark
    /// </summary>
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);

}
