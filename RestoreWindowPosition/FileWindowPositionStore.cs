using System.Globalization;
using System.IO;

namespace RestoreWindowPosition;

/// <summary>
/// An <see cref="IWindowPositionStore"/> backed by a file.
/// </summary>
public sealed class FileWindowPositionStore : IWindowPositionStore
{
    private readonly string _path;
    private readonly bool _perMonitorLayout;

    /// <summary>Keeps window placements in a file.</summary>
    /// <param name="path">
    /// Path of the file, absolute or relative to the working directory.
    /// </param>
    /// <param name="perMonitorLayout">
    /// When <see langword="true"/>, the monitor arrangement key is inserted before the
    /// file's extension, so each arrangement gets its own file — <c>placement.config</c>
    /// becomes <c>placement.3f2a1c09.config</c>. When <see langword="false"/> (the default)
    /// a single file holds the placements for every arrangement, matching how the library
    /// behaved before arrangements were tracked.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    public FileWindowPositionStore(string path, bool perMonitorLayout = false)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (path.Trim().Length == 0) throw new ArgumentException("Path must not be empty.", nameof(path));

        _path = path;
        _perMonitorLayout = perMonitorLayout;
    }

    /// <summary>
    /// The file this store reads and writes for a given monitor arrangement.
    /// </summary>
    /// <param name="monitorLayout">The arrangement key, as passed to <see cref="Read"/> and <see cref="Write"/>.</param>
    /// <returns>The file path.</returns>
    public string GetFilePath(uint monitorLayout)
    {
        if (!_perMonitorLayout) return _path;

        var directory = Path.GetDirectoryName(_path);
        var name = Path.GetFileNameWithoutExtension(_path) +
                   "." + monitorLayout.ToString("x8", CultureInfo.InvariantCulture) +
                   Path.GetExtension(_path);

        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory!, name);
    }

    /// <inheritdoc />
    public byte[]? Read(uint monitorLayout)
    {
        var path = GetFilePath(monitorLayout);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

#if NET7_0_OR_GREATER

    /// <inheritdoc />
    public void Write(uint monitorLayout, ReadOnlySpan<byte> data)
    {
        // Written through a FileStream rather than File.WriteAllBytes, which has no span
        // overload before .NET 9 and would make the caller's span an array first.
        using var file = Create(monitorLayout);
        file.Write(data);
    }

#else

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    public void Write(uint monitorLayout, byte[] data)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));

        using var file = Create(monitorLayout);
        file.Write(data, 0, data.Length);
    }

#endif

    private FileStream Create(uint monitorLayout)
    {
        var path = GetFilePath(monitorLayout);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory!);

        // FileMode.Create truncates, so a shorter payload cannot leave a tail of the old one.
        return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
    }
}
