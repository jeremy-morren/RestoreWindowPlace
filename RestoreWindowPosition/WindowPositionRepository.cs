using System.IO;
using System.Windows;

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
    private readonly bool _cacheMonitorLayout;
    private readonly Func<uint> _monitorLayoutKeyProvider;

    /// <summary>
    /// A cache of the placements read from the store, keyed by monitor arrangement
    /// </summary>
    private readonly Dictionary<uint, Dictionary<string, WindowPosition>> _cache = new();

    /// <summary>
    /// Keys changed during this session, grouped by the layout they were edited under.
    /// </summary>
    private readonly Dictionary<uint, HashSet<string>> _dirty = new();

    /// <summary>
    /// Opens the placements for the monitor arrangement currently attached to the desktop.
    /// </summary>
    /// <param name="store">Where placements are kept.</param>
    /// <param name="cacheMonitorLayout">
    /// Whether to cache the monitor layout for the entire process.
    /// If <see langword="false"/>, the monitor layout is re-read every time a window position is saved or loaded.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="store"/> is <see langword="null"/>.
    /// </exception>
    public WindowPositionRepository(IWindowPositionStore store, bool cacheMonitorLayout)
        : this(store, cacheMonitorLayout, static () => MonitorLayout.GetCurrent().Key)
    {
    }

    /// <summary>
    /// Opens the placements for an arrangement chosen by the caller.
    /// </summary>
    /// <param name="store">Where placements are kept.</param>
    /// <param name="cacheMonitorLayout">
    /// Whether to cache the monitor layout for the entire process.
    /// If <see langword="false"/>, the monitor layout is re-read every time a window position is saved or loaded.
    /// </param>
    /// <param name="monitorLayoutKeyProvider">
    /// Supplies the key handed to the store. It is called afresh on every
    /// <see cref="Reload"/> and <see cref="Save"/>, so monitors attached or detached while
    /// the application is running are picked up.
    /// </param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public WindowPositionRepository(
        IWindowPositionStore store, 
        bool cacheMonitorLayout,
        Func<uint> monitorLayoutKeyProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _cacheMonitorLayout = cacheMonitorLayout;
        _monitorLayoutKeyProvider = monitorLayoutKeyProvider ?? throw new ArgumentNullException(nameof(monitorLayoutKeyProvider));
    }

    /// <summary>The window keys currently known, whether loaded or set this session.</summary>
    public IEnumerable<string> Keys => _cache.Values.SelectMany(d => d.Keys);
    
    private uint? _monitorLayout;

    /// <summary>
    /// Gets the key for the monitor arrangement currently attached to the desktop.
    /// If <see cref="_cacheMonitorLayout"/> is <see langword="false"/> the key is recalculated every time it is requested.
    /// </summary>
    private uint GetMonitorLayoutKey()
    {
        if (!_cacheMonitorLayout)
            return _monitorLayoutKeyProvider();
        
        return _monitorLayout ??= _monitorLayoutKeyProvider();
    }
    
    private Dictionary<string, WindowPosition> GetMapForCurrentLayout()
    {
        var monitorKey = GetMonitorLayoutKey();
        if (_cache.TryGetValue(monitorKey, out var map))
            return map;
        
        map = Read(monitorKey);
        _cache.Add(monitorKey, map);

        return map;
    }

    /// <summary>Looks up the placement saved for a window.</summary>
    /// <param name="window">The key the window was saved under.</param>
    /// <param name="position">The saved placement, if there is one.</param>
    /// <returns><see langword="true"/> if a placement was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public bool TryGet(Window window, out WindowPosition position)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(window);
#else
        if (window is null)
            throw new ArgumentNullException(nameof(window));
#endif
        
        var windowKey = _store.GetWindowKey(window);
        return GetMapForCurrentLayout().TryGetValue(windowKey, out position);
    }

    /// <summary>Determines whether a placement is saved for a window.</summary>
    /// <param name="window">The window a placement is being checked for.</param>
    /// <returns><see langword="true"/> if a placement was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public bool Contains(Window window) => TryGet(window, out _);

    /// <summary>
    /// Records a placement. Nothing reaches the store until <see cref="Save"/> is called.
    /// </summary>
    /// <param name="window">The window to save the placement for.</param>
    /// <param name="position">The placement to record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is <see langword="null"/>.</exception>
    public void Set(Window window, WindowPosition position)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(window);
#else
        if (window is null) 
            throw new ArgumentNullException(nameof(window));
#endif
        
        var windowKey = _store.GetWindowKey(window);
        GetMapForCurrentLayout()[windowKey] = position;

        var layoutKey = GetMonitorLayoutKey();
        if (!_dirty.TryGetValue(layoutKey, out var dirty))
        {
            dirty = [];
            _dirty.Add(layoutKey, dirty);
        }

        dirty.Add(windowKey);
    }

    /// <summary>
    /// Discard everything read or set this session
    /// </summary>
    public void Reload()
    {
        _monitorLayout = null;
        _cache.Clear();
        _dirty.Clear();
    }

    /// <summary>
    /// Writes everything set this session to the store
    /// </summary>
    public void Save()
    {
        var currentLayout = GetMonitorLayoutKey();
        var map = Read(currentLayout);

        foreach (var dirtyByLayout in _dirty)
        {
            if (!_cache.TryGetValue(dirtyByLayout.Key, out var cached))
                continue;

            foreach (var key in dirtyByLayout.Value)
                map[key] = cached[key];
        }

        _store.Write(currentLayout, WindowPositionFormat.Format(map));
        _dirty.Clear();
    }

    private Dictionary<string, WindowPosition> Read(uint layoutKey)
    {
        try
        {
            return WindowPositionFormat.Parse(_store.Read(layoutKey));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A settings file that is missing, locked or unreadable is not worth taking the
            // application down for at startup. Saving still surfaces its failure to the caller.
            return new Dictionary<string, WindowPosition>(StringComparer.Ordinal);
        }
    }
}
