using System.Globalization;

namespace RestoreWindowPosition;

/// <summary>
/// A rectangle in virtual-screen coordinates, described by its edges.
/// </summary>
/// <remarks>
/// Virtual-screen coordinates place the top-left corner of the primary monitor at
/// <c>(0, 0)</c>; monitors to the left of or above the primary one therefore have
/// negative coordinates.
/// </remarks>
public readonly struct ScreenRect : IEquatable<ScreenRect>
{
    /// <summary>Creates a rectangle from its four edges.</summary>
    /// <param name="left">The x-coordinate of the left edge.</param>
    /// <param name="top">The y-coordinate of the top edge.</param>
    /// <param name="right">The x-coordinate of the right edge (exclusive).</param>
    /// <param name="bottom">The y-coordinate of the bottom edge (exclusive).</param>
    public ScreenRect(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>Creates a rectangle from a corner and a size.</summary>
    /// <param name="left">The x-coordinate of the left edge.</param>
    /// <param name="top">The y-coordinate of the top edge.</param>
    /// <param name="width">The width, in pixels.</param>
    /// <param name="height">The height, in pixels.</param>
    /// <returns>The rectangle.</returns>
    public static ScreenRect FromSize(int left, int top, int width, int height) =>
        new(left, top, left + width, top + height);

    /// <summary>The x-coordinate of the left edge.</summary>
    public int Left { get; }

    /// <summary>The y-coordinate of the top edge.</summary>
    public int Top { get; }

    /// <summary>The x-coordinate of the right edge (exclusive).</summary>
    public int Right { get; }

    /// <summary>The y-coordinate of the bottom edge (exclusive).</summary>
    public int Bottom { get; }

    /// <summary>The width, in pixels. May be negative for a malformed rectangle.</summary>
    public int Width => Right - Left;

    /// <summary>The height, in pixels. May be negative for a malformed rectangle.</summary>
    public int Height => Bottom - Top;

    /// <summary><see langword="true"/> when the rectangle encloses no pixels.</summary>
    public bool IsEmpty => Right <= Left || Bottom <= Top;

    /// <summary>Determines whether <paramref name="other"/> lies entirely within this rectangle.</summary>
    /// <param name="other">The rectangle to test.</param>
    /// <returns><see langword="true"/> if this rectangle fully encloses <paramref name="other"/>.</returns>
    public bool Contains(ScreenRect other) =>
        other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    /// <summary>Determines whether this rectangle shares at least one pixel with <paramref name="other"/>.</summary>
    /// <param name="other">The rectangle to test.</param>
    /// <returns><see langword="true"/> if the two rectangles overlap.</returns>
    public bool IntersectsWith(ScreenRect other) => !Intersect(other).IsEmpty;

    /// <summary>Computes the overlap between this rectangle and <paramref name="other"/>.</summary>
    /// <param name="other">The rectangle to intersect with.</param>
    /// <returns>The overlapping region, which is empty when the rectangles do not overlap.</returns>
    public ScreenRect Intersect(ScreenRect other) => new(
        Math.Max(Left, other.Left),
        Math.Max(Top, other.Top),
        Math.Min(Right, other.Right),
        Math.Min(Bottom, other.Bottom));

    /// <inheritdoc />
    public bool Equals(ScreenRect other) =>
        Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ScreenRect other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Left;
            hash = (hash * 397) ^ Top;
            hash = (hash * 397) ^ Right;
            hash = (hash * 397) ^ Bottom;
            return hash;
        }
    }

    /// <summary>Compares two rectangles for equality.</summary>
    /// <param name="left">The first rectangle.</param>
    /// <param name="right">The second rectangle.</param>
    /// <returns><see langword="true"/> if the rectangles have identical edges.</returns>
    public static bool operator ==(ScreenRect left, ScreenRect right) => left.Equals(right);

    /// <summary>Compares two rectangles for inequality.</summary>
    /// <param name="left">The first rectangle.</param>
    /// <param name="right">The second rectangle.</param>
    /// <returns><see langword="true"/> if the rectangles differ.</returns>
    public static bool operator !=(ScreenRect left, ScreenRect right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "[{0},{1} {2}x{3}]", Left, Top, Width, Height);
}
