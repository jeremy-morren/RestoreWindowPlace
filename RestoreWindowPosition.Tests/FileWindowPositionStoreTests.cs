using System.IO;
using FluentAssertions;
using Xunit;

namespace RestoreWindowPosition.Tests;

public class FileWindowPositionStoreTests : IDisposable
{
    private const uint Docked = 0x11111111;
    private const uint Undocked = 0x22222222;

    private static readonly byte[] SomePayload = [WindowPositionFormat.Magic, WindowPositionFormat.Version, 0];
    private static readonly byte[] AnotherPayload = [WindowPositionFormat.Magic, WindowPositionFormat.Version, 0, 9, 9];

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "RestoreWindowPosition.Tests",
        Guid.NewGuid().ToString("n"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string At(string name) => Path.Combine(_directory, name);

    [Fact]
    public void A_store_with_no_file_yet_reads_as_empty() =>
        new FileWindowPositionStore(At("placement.config")).Read(Docked).Should().BeNull();

    [Fact]
    public void What_is_written_is_what_is_read_back()
    {
        var store = new FileWindowPositionStore(At("placement.config"));

        store.Write(Docked, AnotherPayload);

        store.Read(Docked).Should().Equal(AnotherPayload);
    }

    [Fact]
    public void The_payload_reaches_the_file_byte_for_byte()
    {
        var path = At("placement.config");
        new FileWindowPositionStore(path).Write(Docked, AnotherPayload);

        File.ReadAllBytes(path).Should().Equal(AnotherPayload);
    }

    [Fact]
    public void Missing_directories_are_created()
    {
        var path = At(Path.Combine("nested", "deeper", "placement.config"));

        new FileWindowPositionStore(path).Write(Docked, SomePayload);

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void One_file_serves_every_arrangement_by_default()
    {
        var store = new FileWindowPositionStore(At("placement.config"));

        store.GetFilePath(Undocked).Should().Be(store.GetFilePath(Docked));

        store.Write(Docked, AnotherPayload);

        store.Read(Undocked).Should().Equal(store.Read(Docked));
        Directory.GetFiles(_directory).Should().ContainSingle();
    }

    [Fact]
    public void Per_arrangement_stores_name_the_file_after_the_arrangement() =>
        new FileWindowPositionStore(At("placement.config"), perMonitorLayout: true)
            .GetFilePath(Docked).Should().Be(At("placement.11111111.config"));

    [Fact]
    public void An_arrangement_key_is_always_eight_hex_digits()
    {
        var store = new FileWindowPositionStore(At("placement.config"), perMonitorLayout: true);

        store.GetFilePath(42).Should().Be(At("placement.0000002a.config"));
        store.GetFilePath(uint.MaxValue).Should().Be(At("placement.ffffffff.config"));
    }

    [Fact]
    public void Per_arrangement_stores_keep_arrangements_apart()
    {
        var store = new FileWindowPositionStore(At("placement.config"), perMonitorLayout: true);

        store.Write(Docked, SomePayload);
        store.Write(Undocked, AnotherPayload);

        store.Read(Docked).Should().Equal(SomePayload);
        store.Read(Undocked).Should().Equal(AnotherPayload);
        Directory.GetFiles(_directory).Should().HaveCount(2);
    }

    [Fact]
    public void A_per_arrangement_path_without_an_extension_still_works() =>
        new FileWindowPositionStore(At("placement"), perMonitorLayout: true)
            .GetFilePath(Docked).Should().Be(At("placement.11111111"));

    [Fact]
    public void A_bare_file_name_stays_relative_to_the_working_directory() =>
        new FileWindowPositionStore("placement.config", perMonitorLayout: true)
            .GetFilePath(Docked).Should().Be("placement.11111111.config");

    [Fact]
    public void Writing_replaces_rather_than_appends()
    {
        var store = new FileWindowPositionStore(At("placement.config"));

        store.Write(Docked, AnotherPayload);
        store.Write(Docked, SomePayload);

        store.Read(Docked).Should().Equal(SomePayload);
    }

    [Fact]
    public void A_full_round_trip_through_the_repository_survives_a_restart()
    {
        var store = new FileWindowPositionStore(At("placement.config"), perMonitorLayout: true);

        var first = new WindowPositionRepository(store, () => Docked);
        first.Set("MainWindow", new WindowPosition(-1920, 37, 1024, 768, WindowShowState.Maximized));
        first.Save();

        // A fresh repository stands in for the next run of the application.
        var second = new WindowPositionRepository(store, () => Docked);

        second.TryGet("MainWindow", out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(-1920, 37, 1024, 768, WindowShowState.Maximized));
        File.Exists(At("placement.11111111.config")).Should().BeTrue();
    }

    [Fact]
    public void A_file_of_something_else_entirely_is_ignored_rather_than_fatal()
    {
        var path = At("placement.config");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "<?xml version=\"1.0\"?><ArrayOfKeyValue />");

        var repository = new WindowPositionRepository(new FileWindowPositionStore(path), () => Docked);

        repository.Keys.Should().BeEmpty();
    }

    [Fact]
    public void Bad_arguments_are_rejected()
    {
        FluentActions.Invoking(() => new FileWindowPositionStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FileWindowPositionStore("   "))
            .Should().Throw<ArgumentException>();

#if !NET7_0_OR_GREATER
        // On .NET 7 and later this parameter is a span, which cannot be null.
        FluentActions.Invoking(() => new FileWindowPositionStore(At("placement.config")).Write(Docked, null!))
            .Should().Throw<ArgumentNullException>();
#endif
    }
}
