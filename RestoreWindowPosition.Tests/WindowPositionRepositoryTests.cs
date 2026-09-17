using System.IO;
using FluentAssertions;
using Xunit;

namespace RestoreWindowPosition.Tests;

public class WindowPositionRepositoryTests
{
    private const uint Docked = 0x11111111;
    private const uint Undocked = 0x22222222;

    private readonly FakeWindowPositionStore _store = new();
    private uint _layout = Docked;

    private WindowPositionRepository Open() => new(_store, () => _layout);

    [Fact]
    public void Nothing_is_written_until_Save_is_called()
    {
        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(1, 2, 3, 4));

        _store.WriteKeys.Should().BeEmpty();

        repository.Save();

        _store.WriteKeys.Should().Equal(Docked);
        _store.Peek(Docked)["MainWindow"].Should().Be(new WindowPosition(1, 2, 3, 4));
    }

    [Fact]
    public void The_monitor_layout_key_is_what_reaches_the_store()
    {
        Open().Save();

        _store.ReadKeys.Should().AllBeEquivalentTo(Docked);
        _store.WriteKeys.Should().Equal(Docked);
    }

    [Fact]
    public void Saved_placements_are_read_back_on_the_next_run()
    {
        _store.Seed(Docked, ("MainWindow", new WindowPosition(10, 20, 800, 600, WindowShowState.Maximized)));

        Open().TryGet("MainWindow", out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(10, 20, 800, 600, WindowShowState.Maximized));
    }

    [Fact]
    public void Each_monitor_arrangement_keeps_its_own_placements()
    {
        _store.Seed(Docked, ("MainWindow", new WindowPosition(-1920, 0, 800, 600)));
        _store.Seed(Undocked, ("MainWindow", new WindowPosition(100, 100, 640, 480)));

        Open().TryGet("MainWindow", out var docked).Should().BeTrue();

        _layout = Undocked;
        Open().TryGet("MainWindow", out var undocked).Should().BeTrue();

        docked.Should().Be(new WindowPosition(-1920, 0, 800, 600));
        undocked.Should().Be(new WindowPosition(100, 100, 640, 480));
    }

    [Fact]
    public void An_unsaved_placement_wins_over_the_stored_one()
    {
        _store.Seed(Docked, ("MainWindow", new WindowPosition(1, 1, 1, 1)));

        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(2, 2, 2, 2));

        repository.TryGet("MainWindow", out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(2, 2, 2, 2));
    }

    [Fact]
    public void Saving_preserves_entries_written_by_someone_else_in_the_meantime()
    {
        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(1, 1, 1, 1));

        // A second window, or a second instance of the application, saves first.
        _store.Seed(Docked, ("LogWindow", new WindowPosition(9, 9, 9, 9)));

        repository.Save();

        _store.Peek(Docked).Should().Equal(new Dictionary<string, WindowPosition>
        {
            ["MainWindow"] = new(1, 1, 1, 1),
            ["LogWindow"] = new(9, 9, 9, 9),
        });
    }

    [Fact]
    public void Saving_twice_keeps_the_placements_recorded_this_session()
    {
        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(1, 1, 1, 1));
        repository.Save();
        repository.Save();

        _store.Peek(Docked)["MainWindow"].Should().Be(new WindowPosition(1, 1, 1, 1));
    }

    [Fact]
    public void Undocking_before_saving_writes_against_the_new_arrangement()
    {
        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(500, 400, 800, 600));

        _layout = Undocked;
        repository.Save();

        _store.WriteKeys.Should().Equal(Undocked);
        repository.MonitorLayoutKey.Should().Be(Undocked);
        _store.Peek(Docked).Should().BeEmpty();
    }

    [Fact]
    public void Undocking_leaves_the_old_arrangement_untouched()
    {
        _store.Seed(Docked, ("LogWindow", new WindowPosition(9, 9, 9, 9)));

        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(1, 1, 1, 1));

        _layout = Undocked;
        repository.Save();

        _store.Peek(Docked)["LogWindow"].Should().Be(new WindowPosition(9, 9, 9, 9));
        _store.Peek(Undocked).Should().NotContainKey("LogWindow");
    }

    [Fact]
    public void Reload_discards_what_has_not_been_saved()
    {
        _store.Seed(Docked, ("MainWindow", new WindowPosition(1, 1, 1, 1)));

        var repository = Open();
        repository.Set("MainWindow", new WindowPosition(2, 2, 2, 2));
        repository.Reload();

        repository.TryGet("MainWindow", out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(1, 1, 1, 1));
    }

    [Fact]
    public void Reload_picks_up_a_change_of_arrangement()
    {
        var repository = Open();

        _layout = Undocked;
        repository.Reload();

        repository.MonitorLayoutKey.Should().Be(Undocked);
    }

    [Fact]
    public void Keys_reports_the_stored_and_the_unsaved_together_without_repeating()
    {
        _store.Seed(Docked, ("A", new WindowPosition(1, 1, 1, 1)), ("B", new WindowPosition(2, 2, 2, 2)));

        var repository = Open();
        repository.Set("B", new WindowPosition(3, 3, 3, 3));
        repository.Set("C", new WindowPosition(4, 4, 4, 4));

        repository.Keys.Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public void Contains_only_reports_keys_that_are_there()
    {
        _store.Seed(Docked, ("MainWindow", new WindowPosition(1, 1, 1, 1)));

        var repository = Open();

        repository.Contains("MainWindow").Should().BeTrue();
        repository.Contains("NeverSeen").Should().BeFalse();
        repository.TryGet("NeverSeen", out _).Should().BeFalse();
    }

    [Fact]
    public void An_unreadable_store_does_not_stop_the_application_starting()
    {
        _store.OnRead = _ => throw new IOException("the file is locked");

        var repository = Open();

        repository.Contains("MainWindow").Should().BeFalse();
        repository.Keys.Should().BeEmpty();
    }

    [Fact]
    public void A_store_that_fails_for_any_other_reason_is_not_swallowed()
    {
        _store.OnRead = _ => throw new InvalidOperationException("misconfigured");

        var open = () => Open();

        open.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var repository = Open();

        FluentActions.Invoking(() => new WindowPositionRepository(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new WindowPositionRepository(_store, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => repository.Set(null!, default))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => repository.TryGet(null!, out _))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => repository.Contains(null!))
            .Should().Throw<ArgumentNullException>();
    }
}
