using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using FluentAssertions;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Local

namespace RestoreWindowPosition.Tests;

public class WindowPositionRepositoryTests
{
    private const uint Docked = 0x11111111;
    private const uint Undocked = 0x22222222;

    private readonly FakeWindowPositionStore _store = new();
    private uint _layout = Docked;

    private WindowPositionRepository Open() => new(_store, false, () => _layout);

    [Fact]
    public void Save_writesOnlyWhen_called() => OnStaThread(() =>
    {
        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(1, 2, 3, 4));

        _store.WriteKeys.Should().BeEmpty();

        repository.Save();

        _store.WriteKeys.Should().Equal(Docked);
        _store.Peek(Docked)[Key<MainWindow>()].Should().Be(new WindowPosition(1, 2, 3, 4));
    });

    [Fact]
    public void Save_usesCurrentLayout_key() => OnStaThread(() =>
    {
        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(1, 2, 3, 4));
        repository.Save();

        _store.ReadKeys.Should().Equal(new[] { Docked, Docked });
        _store.WriteKeys.Should().Equal(Docked);
    });

    [Fact]
    public void Saved_placementsAreRead_back() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<MainWindow>(), new WindowPosition(10, 20, 800, 600, WindowShowState.Maximized)));

        Open().TryGet(new MainWindow(), out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(10, 20, 800, 600, WindowShowState.Maximized));
    });

    [Fact]
    public void Placements_areKeptPer_layout() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<MainWindow>(), new WindowPosition(-1920, 0, 800, 600)));
        _store.Seed(Undocked, (Key<MainWindow>(), new WindowPosition(100, 100, 640, 480)));

        Open().TryGet(new MainWindow(), out var docked).Should().BeTrue();

        _layout = Undocked;
        Open().TryGet(new MainWindow(), out var undocked).Should().BeTrue();

        docked.Should().Be(new WindowPosition(-1920, 0, 800, 600));
        undocked.Should().Be(new WindowPosition(100, 100, 640, 480));
    });

    [Fact]
    public void Unsaved_placementOverrides_stored() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<MainWindow>(), new WindowPosition(1, 1, 1, 1)));

        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(2, 2, 2, 2));

        repository.TryGet(new MainWindow(), out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(2, 2, 2, 2));
    });

    [Fact]
    public void Save_mergesEntriesWritten_elsewhere() => OnStaThread(() =>
    {
        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(1, 1, 1, 1));

        // A second window, or a second instance of the application, saves first.
        _store.Seed(Docked, (Key<LogWindow>(), new WindowPosition(9, 9, 9, 9)));

        repository.Save();

        _store.Peek(Docked).Should().Equal(new Dictionary<string, WindowPosition>
        {
            [Key<MainWindow>()] = new(1, 1, 1, 1),
            [Key<LogWindow>()] = new(9, 9, 9, 9),
        });
    });

    [Fact]
    public void Save_twiceKeepsSession_placements() => OnStaThread(() =>
    {
        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(1, 1, 1, 1));
        repository.Save();
        repository.Save();

        _store.Peek(Docked)[Key<MainWindow>()].Should().Be(new WindowPosition(1, 1, 1, 1));
    });

    [Fact]
    public void Save_afterLayoutChangeWritesNew_layout() => OnStaThread(() =>
    {
        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(500, 400, 800, 600));

        _layout = Undocked;
        repository.Save();

        _store.WriteKeys.Should().Equal(Undocked);
        _store.Peek(Undocked)[Key<MainWindow>()].Should().Be(new WindowPosition(500, 400, 800, 600));
        _store.Peek(Docked).Should().BeEmpty();
    });

    [Fact]
    public void Save_afterLayoutChangeKeepsOldLayout_untouched() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<LogWindow>(), new WindowPosition(9, 9, 9, 9)));

        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(1, 1, 1, 1));

        _layout = Undocked;
        repository.Save();

        _store.Peek(Docked)[Key<LogWindow>()].Should().Be(new WindowPosition(9, 9, 9, 9));
        _store.Peek(Undocked).Should().NotContainKey(Key<LogWindow>());
    });

    [Fact]
    public void Reload_discardsUnsaved_changes() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<MainWindow>(), new WindowPosition(1, 1, 1, 1)));

        var repository = Open();
        repository.Set(new MainWindow(), new WindowPosition(2, 2, 2, 2));
        repository.Reload();

        repository.TryGet(new MainWindow(), out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(1, 1, 1, 1));
    });

    [Fact]
    public void Reload_picksUpLayout_change() => OnStaThread(() =>
    {
        _store.Seed(Undocked, (Key<MainWindow>(), new WindowPosition(100, 100, 640, 480)));

        var repository = Open();

        _layout = Undocked;
        repository.Reload();

        repository.TryGet(new MainWindow(), out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(100, 100, 640, 480));
    });

    [Fact]
    public void Keys_includeStoredAndUnsavedWithout_duplicates() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<A>(), new WindowPosition(1, 1, 1, 1)), (Key<B>(), new WindowPosition(2, 2, 2, 2)));

        var repository = Open();
        repository.Set(new B(), new WindowPosition(3, 3, 3, 3));
        repository.Set(new C(), new WindowPosition(4, 4, 4, 4));

        repository.Keys.Should().BeEquivalentTo([Key<A>(), Key<B>(), Key<C>()]);
    });

    [Fact]
    public void Contains_reportsOnlyExisting_keys() => OnStaThread(() =>
    {
        _store.Seed(Docked, (Key<MainWindow>(), new WindowPosition(1, 1, 1, 1)));

        var repository = Open();

        repository.Contains(new MainWindow()).Should().BeTrue();
        repository.Contains(new NeverSeen()).Should().BeFalse();
        repository.TryGet(new NeverSeen(), out _).Should().BeFalse();
    });

    [Fact]
    public void Unreadable_storeIsTreatedAs_empty() => OnStaThread(() =>
    {
        _store.OnRead = _ => throw new IOException("the file is locked");

        var repository = Open();

        repository.Contains(new MainWindow()).Should().BeFalse();
        repository.Keys.Should().BeEmpty();
    });

    [Fact]
    public void Unexpected_storeErrorsAreNot_swallowed() => OnStaThread(() =>
    {
        _store.OnRead = _ => throw new InvalidOperationException("misconfigured");

        var repository = Open();

        FluentActions.Invoking(() => repository.Contains(new MainWindow()))
            .Should().Throw<InvalidOperationException>();
    });

    [Fact]
    public void Null_arguments_throw() => OnStaThread(() =>
    {
        var repository = Open();

        FluentActions.Invoking(() => new WindowPositionRepository(null!, false, () => _layout))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new WindowPositionRepository(_store, false, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => repository.Set(null!, default))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => repository.TryGet(null!, out _))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => repository.Contains(null!))
            .Should().Throw<ArgumentNullException>();
    });

    private static string Key<T>() => typeof(T).FullName!;

    private sealed class MainWindow : Window;
    private sealed class LogWindow : Window;
    private sealed class NeverSeen : Window;
    private sealed class A : Window;
    private sealed class B : Window;
    private sealed class C : Window;

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
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        failure?.Throw();
    }

}
