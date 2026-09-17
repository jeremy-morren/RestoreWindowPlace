namespace RestoreWindowPosition.Tests;

/// <summary>
/// An in-memory store that records what it was asked to do, so tests can assert on the
/// monitor-layout key the library passes down as well as on the payload.
/// </summary>
internal sealed class FakeWindowPositionStore : IWindowPositionStore
{
    private readonly Dictionary<uint, byte[]> _data = [];

    public List<uint> ReadKeys { get; } = [];

    public List<uint> WriteKeys { get; } = [];

    public Func<uint, byte[]?>? OnRead { get; set; }

    public byte[]? Read(uint monitorLayout)
    {
        ReadKeys.Add(monitorLayout);
        if (OnRead is not null) return OnRead(monitorLayout);
        return _data.TryGetValue(monitorLayout, out var data) ? data : null;
    }

#if NET7_0_OR_GREATER

    public void Write(uint monitorLayout, ReadOnlySpan<byte> data)
    {
        WriteKeys.Add(monitorLayout);

        // The span is only valid for the call, so a store that keeps it has to copy.
        _data[monitorLayout] = data.ToArray();
    }

#else

    public void Write(uint monitorLayout, byte[] data)
    {
        WriteKeys.Add(monitorLayout);
        _data[monitorLayout] = data;
    }

#endif

    /// <summary>Writes a payload without recording the call, to set up a starting state.</summary>
    public void Seed(uint monitorLayout, params (string Key, WindowPosition Position)[] positions)
    {
        var map = new Dictionary<string, WindowPosition>(StringComparer.Ordinal);
        foreach (var (key, position) in positions) map[key] = position;
        _data[monitorLayout] = WindowPositionFormat.Format(map);
    }

    /// <summary>Reads back a payload without recording the call.</summary>
    public Dictionary<string, WindowPosition> Peek(uint monitorLayout) =>
        WindowPositionFormat.Parse(_data.TryGetValue(monitorLayout, out var data) ? data : null);
}
