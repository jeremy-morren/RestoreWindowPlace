using System.IO;
namespace RestoreWindowPosition;

/// <summary>
/// The saved window placements for the current monitor arrangement, and the read-modify-write
/// cycle that keeps them in an <see cref="IWindowPositionStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the whole of the library that does not touch WPF; <see cref="WindowPlacer"/> is a
/// thin shim over it. Anything that can produce a <see cref="WindowPosition"/> — a WinForms
/// form, a console host repositioning someone else's window — can use it directly.
/// </para>
/// <para>
/// <see cref="Save"/> re-reads the store before writing and overlays only the entries set
/// during this session. Two windows saving independently therefore do not overwrite each
/// other, and neither does a second instance of the application.
/// </para>
/// </remarks>
public sealed class WindowPositionRepository
{
    private readonly IWindowPositionStore _store;
    private readonly Func<uint> _monitorLayoutKeyProvider;

    /// <summary>What the store held when it was last read.</summary>
    private Dictionary<string, WindowPosition> _stored;

    /// <summary>Placements set during this session, which win over <see cref="_stored"/>.</summary>
    private readonly Dictionary<string, WindowPosition> _pending =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Opens the placements for the monitor arrangement currently attached to the desktop.
    /// </summary>
    /// <param name="store">Where placements are kept.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public WindowPositionRepository(IWindowPositionStore store)
        : this(store, static () => MonitorLayout.GetCurrent().Key)
    {
    }

    /// <summary>
    /// Opens the placements for an arrangement chosen by the caller.
    /// </summary>
    /// <param name="store">Where placements are kept.</param>
    /// <param name="monitorLayoutKeyProvider">
    /// Supplies the key handed to the store. It is called afresh on every
    /// <see cref="Reload"/> and <see cref="Save"/>, so monitors attached or detached while
    /// the application is running are picked up.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public WindowPositionRepository(IWindowPositionStore store, Func<uint> monitorLayoutKeyProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _monitorLayoutKeyProvider = monitorLayoutKeyProvider ?? throw new ArgumentNullException(nameof(monitorLayoutKeyProvider));

        MonitorLayoutKey = _monitorLayoutKeyProvider();
        _stored = Read(MonitorLayoutKey);
    }

    /// <summary>The arrangement key the loaded placements came from.</summary>
    public uint MonitorLayoutKey { get; private set; }

    /// <summary>The window keys currently known, whether loaded or set this session.</summary>
    public IEnumerable<string> Keys
    {
        get
        {
            foreach (var key in _pending.Keys) yield return key;
            foreach (var key in _stored.Keys)
            {
                if (!_pending.ContainsKey(key)) yield return key;
            }
        }
    }

    /// <summary>Looks up the placement saved for a window.</summary>
    /// <param name="windowKey">The key the window was saved under.</param>
    /// <param name="position">The saved placement, if there is one.</param>
    /// <returns><see langword="true"/> if a placement was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="windowKey"/> is <see langword="null"/>.</exception>
    public bool TryGet(string windowKey, out WindowPosition position)
    {
        if (windowKey is null) throw new ArgumentNullException(nameof(windowKey));

        return _pending.TryGetValue(windowKey, out position) || _stored.TryGetValue(windowKey, out position);
    }

    /// <summary>Determines whether a placement is saved for a window.</summary>
    /// <param name="windowKey">The key the window was saved under.</param>
    /// <returns><see langword="true"/> if a placement was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="windowKey"/> is <see langword="null"/>.</exception>
    public bool Contains(string windowKey) => TryGet(windowKey, out _);

    /// <summary>
    /// Records a placement. Nothing reaches the store until <see cref="Save"/> is called.
    /// </summary>
    /// <param name="windowKey">The key to save the window under.</param>
    /// <param name="position">The placement to record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="windowKey"/> is <see langword="null"/>.</exception>
    public void Set(string windowKey, WindowPosition position)
    {
        if (windowKey is null) throw new ArgumentNullException(nameof(windowKey));

        _pending[windowKey] = position;
    }

    /// <summary>
    /// Discards everything set this session and re-reads the store for the arrangement
    /// currently attached to the desktop.
    /// </summary>
    public void Reload()
    {
        _pending.Clear();
        MonitorLayoutKey = _monitorLayoutKeyProvider();
        _stored = Read(MonitorLayoutKey);
    }

    /// <summary>
    /// Writes everything set this session to the store, preserving entries written by anyone
    /// else since the last read.
    /// </summary>
    /// <remarks>
    /// When the monitor arrangement has changed since loading, the placements are written
    /// against the new arrangement — they describe where the windows are now, not where they
    /// were — and entries belonging only to the old arrangement are left behind with it.
    /// </remarks>
    public void Save()
    {
        var layoutKey = _monitorLayoutKeyProvider();
        var merged = Read(layoutKey);

        foreach (var entry in _pending) merged[entry.Key] = entry.Value;

        _store.Write(layoutKey, WindowPositionFormat.Format(merged));

        MonitorLayoutKey = layoutKey;
        _stored = merged;
    }

    private Dictionary<string, WindowPosition> Read(uint layoutKey)
    {
        try
        {
            return WindowPositionFormat.Parse(_store.Read(layoutKey));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A settings file that is missing, locked or unreadable is not worth taking the
            // application down for at startup. Saving still surfaces its failure to the caller.
            return new Dictionary<string, WindowPosition>(StringComparer.Ordinal);
        }
    }
}
