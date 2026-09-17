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
    /// <summary>Reads back whatever <see cref="Write"/> last wrote for this arrangement.</summary>
    /// <param name="monitorLayout">The current <see cref="MonitorLayout.Key"/>.</param>
    /// <returns>
    /// The stored payload, or <see langword="null"/> if nothing has been stored for this
    /// arrangement yet.
    /// </returns>
    byte[]? Read(uint monitorLayout);

#if NET7_0_OR_GREATER

    /// <summary>Stores the payload for this arrangement, replacing any previous one.</summary>
    /// <param name="monitorLayout">The current <see cref="MonitorLayout.Key"/>.</param>
    /// <param name="data">
    /// The payload to store. It is only valid for the duration of the call, so an
    /// implementation that keeps it must copy it.
    /// </param>
    void Write(uint monitorLayout, ReadOnlySpan<byte> data);

#else

    /// <summary>Stores the payload for this arrangement, replacing any previous one.</summary>
    /// <param name="monitorLayout">The current <see cref="MonitorLayout.Key"/>.</param>
    /// <param name="data">The payload to store.</param>
    /// <remarks>
    /// On .NET 7 and later this takes a <c>ReadOnlySpan&lt;byte&gt;</c>, so a store can be
    /// written without the payload having to become an array first. .NET Framework has no
    /// span in the box, so it takes the array.
    /// </remarks>
    void Write(uint monitorLayout, byte[] data);

#endif
}
