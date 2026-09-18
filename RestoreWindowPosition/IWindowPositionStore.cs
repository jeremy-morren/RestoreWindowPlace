using System.Windows;

// ReSharper disable GrammarMistakeInComment

namespace RestoreWindowPosition;

/// <summary>
/// Somewhere to keep saved window placements: a file, a registry value, a settings row, or
/// anything else that can hold a blob.
/// </summary>
/// <remarks>
/// <para>
/// Both methods receive the current <see cref="MonitorLayout.Key"/>. A store is free to
/// ignore it and keep one set of placements for every arrangement, or to key its storage by
/// it and keep a separate set per arrangement, so that undocking a laptop does not overwrite
/// the placements used with the docked monitors.
/// </para>
/// <para>
/// The payload is produced and consumed by <see cref="WindowPositionFormat"/>. It is a small
/// binary blob — a couple of dozen bytes per window — and an implementation should treat it
/// as opaque and hand it back byte for byte.
/// </para>
/// </remarks>
public interface IWindowPositionStore
{
    /// <summary>
    /// Returns a string that uniquely identifies the window for this store
    /// </summary>
    /// <param name="window">The window to get the key for.</param>
    /// <returns>A string that uniquely identifies the window.</returns>
    /// <remarks>
    /// This key is used to store and retrieve the window's placement in the store.
    /// </remarks>
    public string GetWindowKey(Window window);
    
    /// <summary>Reads back whatever <see cref="Write"/> last wrote for this arrangement.</summary>
    /// <param name="monitorLayout">The current <see cref="MonitorLayout.Key"/>.</param>
    /// <returns>
    /// The stored payload, or <see langword="null"/> if nothing has been stored for this arrangement yet.
    /// </returns>
    byte[]? Read(uint monitorLayout);

    /// <summary>Stores the payload for this arrangement, replacing any previous one.</summary>
    /// <param name="monitorLayout">The current <see cref="MonitorLayout.Key"/>.</param>
    /// <param name="data">The payload to store.</param>
    void Write(uint monitorLayout, byte[] data);
}
