namespace RestoreWindowPosition;

/// <summary>
/// The 32-bit FNV-1a non-cryptographic hash.
/// </summary>
/// <remarks>
/// <see cref="object.GetHashCode()"/> on a string is randomised per process and so cannot be
/// used to derive a key that has to survive a restart. FNV-1a is a handful of lines,
/// allocates nothing, and is stable by definition.
/// </remarks>
internal static class Fnv1a32
{
    private const uint OffsetBasis = 2166136261;
    private const uint Prime = 16777619;

    /// <summary>Hashes the first <paramref name="count"/> bytes of <paramref name="data"/>.</summary>
    /// <param name="data">The buffer to hash.</param>
    /// <param name="count">How many of its bytes to read.</param>
    /// <returns>The digest.</returns>
    public static uint Hash(byte[] data, int count)
    {
        var hash = OffsetBasis;

        for (var i = 0; i < count; i++)
        {
            hash ^= data[i];
            hash *= Prime;
        }

        return hash;
    }
}
