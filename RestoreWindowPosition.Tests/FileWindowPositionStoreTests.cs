using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using FluentAssertions;
using Xunit;

// ReSharper disable StringLiteralTypo

namespace RestoreWindowPosition.Tests;

public sealed class FileWindowPositionStoreTests : IDisposable
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
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private string At(string name) => Path.Combine(_directory, name);

    [Fact]
    public void Read_returnsNull_fileMissing() =>
        new FileWindowPositionStore(At("placement.bin")).Read(Docked).Should().BeNull();

    [Fact]
    public void Read_returnsWrittenPayload_payloadExists()
    {
        var store = new FileWindowPositionStore(At("placement.bin"));

        store.Write(Docked, AnotherPayload);

        store.Read(Docked).Should().Equal(AnotherPayload);
    }

    [Fact]
    public void Read_returnsRawPayload_fileIsNotCompressed()
    {
        var path = At("placement.bin");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(path, [1, 2, 3, 4]);

        new FileWindowPositionStore(path).Read(Docked).Should().Equal([1, 2, 3, 4]);
    }

    [Fact]
    public void Write_storesRawPayload_payloadWritten()
    {
        var path = At("placement.bin");
        new FileWindowPositionStore(path).Write(Docked, AnotherPayload);

        var bytes = File.ReadAllBytes(path);
        bytes.Should().Equal(AnotherPayload);
    }

    [Fact]
    public void Read_returnsNull_payloadExceedsLimit()
    {
        var path = At("placement.bin");
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(path, new byte[1024 * 1024 + 1]);

        new FileWindowPositionStore(path).Read(Docked).Should().BeNull();
    }

    [Fact]
    public void Missing_directoriesAre_created()
    {
        var path = At(Path.Combine("nested", "deeper", "placement.bin"));

        new FileWindowPositionStore(path).Write(Docked, SomePayload);

        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public void Default_storeUsesOneFileForAll_layouts()
    {
        var store = new FileWindowPositionStore(At("placement.bin"));

        store.GetFilePath(Undocked).Should().Be(store.GetFilePath(Docked));

        store.Write(Docked, AnotherPayload);

        store.Read(Undocked).Should().Equal(store.Read(Docked));
        Directory.GetFiles(_directory).Should().ContainSingle();
    }

    [Fact]
    public void Per_layoutFileNameIncludesLayout_key() =>
        new FileWindowPositionStore(At("placement.bin"), perMonitorLayout: true)
            .GetFilePath(Docked).Should().Be(At("placement_11111111.bin"));

    [Fact]
    public void Layout_keysAreAlwaysEightHex_digits()
    {
        var store = new FileWindowPositionStore(At("placement.bin"), perMonitorLayout: true);

        store.GetFilePath(42).Should().Be(At("placement_0000002a.bin"));
        store.GetFilePath(uint.MaxValue).Should().Be(At("placement_ffffffff.bin"));
    }

    [Fact]
    public void Per_layoutStoreKeepsLayouts_apart()
    {
        var store = new FileWindowPositionStore(At("placement.bin"), perMonitorLayout: true);

        store.Write(Docked, SomePayload);
        store.Write(Undocked, AnotherPayload);

        store.Read(Docked).Should().Equal(SomePayload);
        store.Read(Undocked).Should().Equal(AnotherPayload);
        Directory.GetFiles(_directory).Should().HaveCount(2);
    }

    [Fact]
    public void Per_layoutFileNameWithoutExtensionStill_works() =>
        new FileWindowPositionStore(At("placement"), perMonitorLayout: true)
            .GetFilePath(Docked).Should().Be(At("placement_11111111"));

    [Fact]
    public void Bare_fileNameStaysRelativeToWorking_directory() =>
        new FileWindowPositionStore("placement.bin", perMonitorLayout: true)
            .GetFilePath(Docked).Should().Be("placement_11111111.bin");

    [Fact]
    public void Writing_replacesRatherThan_appends()
    {
        var store = new FileWindowPositionStore(At("placement.bin"));

        store.Write(Docked, AnotherPayload);
        store.Write(Docked, SomePayload);

        store.Read(Docked).Should().Equal(SomePayload);
    }

    [Fact]
    public void Read_returnsNoPlacement_payloadHasInvalidFormat() => OnStaThread(() =>
    {
        var path = At("placement.bin");
        var store = new FileWindowPositionStore(path);
        store.Write(Docked, "<?xml version=\"1.0\"?><ArrayOfKeyValue />"u8.ToArray());

        var repository = new WindowPositionRepository(store, false, () => Docked);

        repository.Contains(new SampleWindow()).Should().BeFalse();
    });

    [Fact]
    public void Repository_roundTripSurvives_restart() => OnStaThread(() =>
    {
        var store = new FileWindowPositionStore(At("placement.bin"), perMonitorLayout: true);

        var first = new WindowPositionRepository(store, false, () => Docked);
        first.Set(new SampleWindow(), new WindowPosition(-1920, 37, 1024, 768, WindowShowState.Maximized));
        first.Save();

        // A fresh repository stands in for the next run of the application.
        var second = new WindowPositionRepository(store, false, () => Docked);

        second.TryGet(new SampleWindow(), out var position).Should().BeTrue();
        position.Should().Be(new WindowPosition(-1920, 37, 1024, 768, WindowShowState.Maximized));
        File.Exists(At("placement_11111111.bin")).Should().BeTrue();
    });

    [Fact]
    public void Bad_argumentsAre_rejected()
    {
        FluentActions.Invoking(() => new FileWindowPositionStore(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FileWindowPositionStore("   "))
            .Should().Throw<ArgumentException>();

#if !NET7_0_OR_GREATER
        // On .NET 7 and later this parameter is a span, which cannot be null.
        FluentActions.Invoking(() => new FileWindowPositionStore(At("placement.bin")).Write(Docked, null!))
            .Should().Throw<ArgumentNullException>();
#endif
    }

    private sealed class SampleWindow : Window;

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
