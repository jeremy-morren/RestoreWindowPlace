using System.Windows;

namespace RestoreWindowPosition;

/// <summary>
/// One-line registration for a window that manages its own placement.
/// </summary>
/// <remarks>
/// Each call creates a <see cref="WindowPlacer"/> scoped to the one window and saves when
/// that window closes, so no application-wide object has to be kept or shut down. Because
/// <see cref="WindowPositionRepository.Save"/> re-reads the store and overlays only what it
/// recorded, several windows can do this against the same store without overwriting one
/// another.
/// </remarks>
public static class WindowExtensions
{
    /// <param name="window">The window to track.</param>
    extension<T>(T window) where T : Window
    {
        /// <summary>
        /// Restores this window's placement when it opens, and saves it when it closes, keyed
        /// by the name of its type.
        /// </summary>
        /// <param name="store">Where placements are kept.</param>
        /// <returns>The window, so the call can be chained.</returns>
        /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// public MainWindow()
        /// {
        ///     InitializeComponent();
        ///     this.RestoreWindowPosition(store);
        /// }
        /// </code>
        /// </example>
        public T RestoreWindowPosition(IWindowPositionStore store) =>
            window.RestoreWindowPosition(predicate: null, store);

        /// <summary>
        /// Restores this window's placement when it opens, and saves it when it closes, keyed
        /// by the name of its type, subject to a final decision by the caller.
        /// </summary>
        /// <param name="predicate">
        /// Consulted with the placement that is about to be applied. Returning
        /// <see langword="false"/> leaves the window where WPF put it.
        /// </param>
        /// <param name="store">Where placements are kept.</param>
        /// <returns>The window, so the call can be chained.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="window"/> or <paramref name="store"/> is <see langword="null"/>.
        /// </exception>
        /// <example>
        /// <code>
        /// this.RestoreWindowPosition(
        ///     (_, saved) => MonitorLayout.GetCurrent().IsVisible(saved.Bounds),
        ///     store);
        /// </code>
        /// </example>
        public T RestoreWindowPosition(Func<T, WindowPosition, bool>? predicate,
            IWindowPositionStore store)
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(store);
#else
            if (window is null)
                throw new ArgumentNullException(nameof(window));
            if (store is null)
                throw new ArgumentNullException(nameof(store));
#endif

            var placer = new WindowPlacer(store);
            placer.Register(window, predicate);

            // Registration records the placement in its own Closed handler, added just above;
            // handlers fire in the order they were added, so by the time this one runs the
            // record is in place and there is something to write.
            window.Closed += (_, _) => placer.Save();

            return window;
        }
    }
}
