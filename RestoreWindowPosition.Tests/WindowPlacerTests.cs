using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using FluentAssertions;
using Xunit;

// ReSharper disable RedundantTypeArgumentsOfMethod
// ReSharper disable CommentTypo

namespace RestoreWindowPosition.Tests;

/// <summary>
/// Covers the WPF surface against real windows.
/// </summary>
/// <remarks>
/// <para>
/// The windows are never shown: <see cref="WindowInteropHelper.EnsureHandle"/> creates the
/// native window and raises <see cref="Window.SourceInitialized"/>, which is everything
/// registration hangs off, without anything appearing on screen.
/// </para>
/// <para>
/// Placements are compared by recording the window through the library and reading the record back,
/// never by asserting pixel coordinates against the window's own Left and Top:
/// those go through WPF's scaling and would make these tests a property of the machine they run on.
/// </para>
/// </remarks>
public class WindowPlacerTests
{
    private const uint Layout = 0x11111111;

    private static readonly WindowPosition Saved = new(320, 240, 500, 400);

    [Fact]
    public void Registered_windowIsRestoredWhenHandleIs_created() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer((typeof(Window).FullName!, Saved));
        var window = new Window();

        placer.Register(window);
        Realize(window);

        // The write and the read-back both go through WINDOWPLACEMENT in workspace pixels,
        // with no scaling in between, so the placement comes back exactly as it was saved.
        Record(placer, window).Should().Be(Saved);
    });

    [Fact]
    public void Restoring_thePositionOnlyLeavesTheSize_alone() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer((typeof(Window).FullName!, Saved));
        var window = new Window();

        placer.RegisterPositionOnly(window);
        Realize(window);

        var restored = Record(placer, window);
        restored.Left.Should().Be(Saved.Left);
        restored.Top.Should().Be(Saved.Top);
        restored.Width.Should().NotBe(Saved.Width);
    });

    [Fact]
    public void Predicate_receivesPlacementThatWillBe_applied() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer((typeof(Window).FullName!, Saved));
        var window = new Window();

        var seen = new List<WindowPosition>();
        placer.Register(window, (candidate, position) =>
        {
            candidate.Should().BeSameAs(window);
            seen.Add(position);
            return true;
        });

        Realize(window);

        seen.Should().Equal(Saved);
    });

    [Fact]
    public void Declining_predicateLeavesWindowWhereWpfPut_it() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer((typeof(Window).FullName!, Saved));
        var window = new Window();

        var consulted = false;
        placer.Register(window, (_, _) =>
        {
            consulted = true;
            return false;
        });
        Realize(window);

        consulted.Should().BeTrue();
        Record(placer, window).Should().NotBe(Saved);
    });

    [Fact]
    public void Predicate_isNotConsultedWhenNothing_saved() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer();
        var window = new Window();

        var consulted = false;
        placer.Register(window, (_, _) =>
        {
            consulted = true;
            return true;
        });

        Realize(window);

        consulted.Should().BeFalse();
    });

    [Fact]
    public void Registering_aWindowThatIsAlreadyRealizedStillRestores_it() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer((typeof(Window).FullName!, Saved));
        var window = new Window();
        Realize(window);

        placer.Register(window);

        Record(placer, window).Should().Be(Saved);
    });

    [Fact]
    public void Closing_aRegisteredWindowRecords_it() => OnStaThread(() =>
    {
        var (store, placer) = NewPlacer();
        var window = new Window1();

        placer.Register(window);
        Realize(window);
        window.Close();
        placer.Save();

        store.Peek(Layout).Should().ContainKey(typeof(Window1).FullName!);
    });

    [Fact]
    public void Close_cancelledAfterwardsDoesNotRecord_window() => OnStaThread(() =>
    {
        var (store, placer) = NewPlacer();
        var window = new Window1();

        placer.Register(window);

        // Added after registration, so it runs after the library's own Closing handler and
        // the library cannot see the verdict at the point where it reads the placement.
        window.Closing += (_, e) => e.Cancel = true;

        Realize(window);
        window.Close();
        placer.Save();

        store.Peek(Layout).Should().NotContainKey(typeof(Window1).FullName!);
    });

    [Fact]
    public void Registering_byTypeUsesTheTypeNameAsThe_key() => OnStaThread(() =>
    {
        var (store, placer) = NewPlacer();
        var window = new Window3();

        placer.Register(window);
        Realize(window);
        window.Close();
        placer.Save();

        store.Peek(Layout).Should().ContainKey(typeof(Window3).FullName!);
    });

    [Fact]
    public void Extension_methodRestoresOnOpenAndSavesOn_close() => OnStaThread(() =>
    {
        var store = new FakeWindowPositionStore();
        var layout = MonitorLayout.GetCurrent().Key;
        store.Seed(layout, (typeof(Window3).FullName!, Saved));

        var window = new Window3();
        window.RestoreWindowPosition(store).Should().BeSameAs(window);

        Realize(window);
        window.Close();

        store.Peek(layout).Should().ContainKey(typeof(Window3).FullName!);
    });

    [Fact]
    public void Extension_methodHonors_predicate() => OnStaThread(() =>
    {
        var store = new FakeWindowPositionStore();
        store.Seed(MonitorLayout.GetCurrent().Key, (typeof(Window3).FullName!, Saved));

        var window = new Window3();
        var consulted = false;
        window.RestoreWindowPosition(
            (_, saved) =>
            {
                consulted = true;
                saved.Should().Be(Saved);
                return false;
            },
            store);

        Realize(window);

        consulted.Should().BeTrue();
    });

    [Fact]
    public void Restoring_anUnknownTypeDoes_nothing() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer();
        var window1 = new Window1();
        Realize(window1);

        var window2 = new Window2();
        placer.Restore(window2).Should().BeFalse();
        placer.RestorePosition(window2).Should().BeFalse();
        placer.IsRegistered(window2).Should().BeFalse();
    });

    [Fact]
    public void Recording_windowWithoutHandleIsNo_op() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer();

        placer.Store(new Window2());

        placer.IsRegistered(new Window3()).Should().BeFalse();
    });

    [Fact]
    public void Null_argumentsAre_rejected() => OnStaThread(() =>
    {
        var (_, placer) = NewPlacer();
        var window = new Window();

        FluentActions.Invoking(() => new WindowPlacer((IWindowPositionStore)null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new WindowPlacer((WindowPositionRepository)null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => placer.Register<Window>(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => window.RestoreWindowPosition(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ((Window1)null!).RestoreWindowPosition<Window1>(new FakeWindowPositionStore()))
            .Should().Throw<ArgumentNullException>();
    });

    private static (FakeWindowPositionStore Store, WindowPlacer Placer) NewPlacer(
        params (string Key, WindowPosition Position)[] saved)
    {
        var store = new FakeWindowPositionStore();
        if (saved.Length > 0) store.Seed(Layout, saved);

        return (store, new WindowPlacer(new WindowPositionRepository(store, false, () => Layout)));
    }

    /// <summary>Creates the native window, which is what raises SourceInitialized.</summary>
    private static void Realize(Window window) => new WindowInteropHelper(window).EnsureHandle();

    /// <summary>
    /// Reads the window's placement back through the library,
    /// giving a value that can be compared with another reading without depending on the monitor's scaling.
    /// </summary>
    private static WindowPosition Record(WindowPlacer placer, Window window)
    {
        placer.Store(window);
        placer.Positions.TryGet(window, out var position);
        return position;
    }

    private static void OnStaThread(Action action)
    {
        Desktop.Require();

        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                failure = ExceptionDispatchInfo.Capture(e);
            }
            finally
            {
                // Creating a Window starts a Dispatcher on this thread and gives it native
                // windows to own. Letting the thread die without shutting it down leaves
                // those HWNDs receiving messages with no managed thread behind them, which
                // on .NET crashes the process inside WPF's window procedure.
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }

    /// <summary>A window type with a name of its own, to exercise keying by type.</summary>
    private sealed class Window1 : Window;

    /// <summary>A window type with a name of its own, to exercise keying by type.</summary>
    private sealed class Window2 : Window;

    /// <summary>A window type with a name of its own, to exercise keying by type.</summary>
    private sealed class Window3 : Window;
}

