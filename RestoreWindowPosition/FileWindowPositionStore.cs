using System.Globalization;
using System.IO;
using System.Windows;

namespace RestoreWindowPosition;

/// <summary>
/// An <see cref="IWindowPositionStore"/> backed by a file.
/// </summary>
public sealed class FileWindowPositionStore : IWindowPositionStore
{
    // Window-placement payloads are normally a few hundred bytes. This bound prevents a
    // corrupted or substituted settings file from becoming an unbounded allocation.
    private const int MaximumPayloadBytes = 1024 * 1024;
    private readonly string _path;
    private readonly bool _perMonitorLayout;

    /// <summary>Keeps window placements in a file.</summary>
    /// <param name="path">
    /// Path of the file, absolute or relative to the working directory.
    /// </param>
    /// <param name="perMonitorLayout">
    /// Whether to keep a separate set of placements for each monitor arrangement.
    /// If <see langword="false"/>, the same set is used for all arrangements.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is empty or whitespace
    /// .</exception>
    public FileWindowPositionStore(string path, bool perMonitorLayout = false)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(path);
#else
        if (path is null) 
            throw new ArgumentNullException(nameof(path));
#endif

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        _path = path;
        _perMonitorLayout = perMonitorLayout;
    }
    
    /// <summary>
    /// Gets a key for a window
    /// </summary>
    /// <remarks>
    /// Key windows based on the type. We intentionally use full name (but not assembly-qualified name)
    /// so that new versions of the app can restore windows created by old versions.
    /// </remarks>
    public string GetWindowKey(Window window) => window.GetType().FullName!;

    /// <inheritdoc />
    public byte[]? Read(uint monitorLayout)
    {
        var path = GetFilePath(monitorLayout);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > MaximumPayloadBytes)
                return null;

            return file.ReadBytesExactly((int)file.Length);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            // File not found or other IO exception, treat as no data
            return null;
        }
    }
    
    /// <inheritdoc />
    public void Write(uint monitorLayout, byte[] data)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(data);
#else
        if (data is null) 
            throw new ArgumentNullException(nameof(data));
#endif

        var path = GetFilePath(monitorLayout);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) 
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(path, data);
    }

    
    /// <summary>
    /// The file this store reads and writes for a given monitor arrangement.
    /// </summary>
    /// <param name="monitorLayout">The arrangement key, as passed to <see cref="Read"/> and <see cref="Write"/>.</param>
    /// <returns>The file path.</returns>
    public string GetFilePath(uint monitorLayout)
    {
        if (!_perMonitorLayout) 
            return _path;

        var directory = Path.GetDirectoryName(_path);
        
        var name = string.Format(
            CultureInfo.InvariantCulture,
            "{0}_{1:x8}{2}",
            Path.GetFileNameWithoutExtension(_path),
            monitorLayout,
            Path.GetExtension(_path));
        
        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }
}
