using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class JsonlTailerBoundaryCoverageTests
{
    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public async Task MaximumLengthLine_IsAcceptedEvenWhenReadContainsNextLine(string newline)
    {
        var line = new string('a', 1024 * 1024);
        await WithFileAsync(line + newline + "next" + newline, async (path, tailer) =>
        {
            var lines = await ReadAsync(tailer, path, new(Follow: false));
            lines.Should().Equal(line, "next");
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    public async Task OversizedLine_IsRejectedWithOrWithoutTerminator(string newline)
    {
        await WithFileAsync(new string('a', 1024 * 1024 + 1) + newline, async (path, tailer) =>
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ReadAsync(tailer, path, new(Follow: false)));
        });
    }

    [Theory]
    [InlineData("last", "last")]
    [InlineData("last\r", "last")]
    public async Task NonFollowingStream_EmitsFinalUnterminatedLine(string content, string expected)
    {
        await WithFileAsync(content, async (path, tailer) => (await ReadAsync(tailer, path, new(Follow: false))).Should().Equal(expected));
    }

    [Fact]
    public async Task MidLineOffsetWithoutFollowingNewline_ProducesNoFragment()
    {
        await WithFileAsync(new string('x', 9000), async (path, tailer) =>
            (await ReadAsync(tailer, path, new(FromByteOffset: 1, Follow: false))).Should().BeEmpty());
    }

    [Fact]
    public async Task TimestampPositioning_LeavesFilteringToEventPipeline()
    {
        await WithFileAsync("first\nsecond\n", async (path, tailer) =>
            (await ReadAsync(tailer, path, new(AfterTimestamp: DateTimeOffset.UtcNow, Follow: false))).Should().Equal("first", "second"));
    }

    [Fact]
    public async Task CreationTimeFailure_DoesNotPreventInitialRead()
    {
        await WithFileAsync("line\n", async (path, _) =>
        {
            var tailer = Create(new UnavailableMetadata());
            (await ReadAsync(tailer, path, new(Follow: false))).Should().Equal("line");
        });
    }

    [Fact]
    public async Task TemporarilyMissingMetadata_RetriesAndRespondsToCancellation()
    {
        await WithFileAsync("", async (path, _) =>
        {
            var fs = new UnavailableMetadata();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await using var reader = Create(fs).TailAsync(path, new(Follow: true), cts.Token).GetAsyncEnumerator();
            var next = reader.MoveNextAsync().AsTask();
            await fs.SizeRequested.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => next);
        });
    }

    private static JsonlTailer Create(IFileSystem fs) => new(fs, NullLogger<JsonlTailer>.Instance,
        Options.Create(new CodexClientOptions { TailPollInterval = TimeSpan.FromMilliseconds(50) }));

    private static async Task<List<string>> ReadAsync(JsonlTailer tailer, string path, EventStreamOptions options)
    {
        var result = new List<string>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await foreach (var line in tailer.TailAsync(path, options, deadline.Token)) result.Add(line);
        return result;
    }

    private static async Task WithFileAsync(string contents, Func<string, JsonlTailer, Task> action)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, contents);
            await action(path, Create(new RealFileSystem()));
        }
        finally { File.Delete(path); }
    }

    private sealed class UnavailableMetadata : IFileSystem
    {
        public TaskCompletionSource SizeRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FileExists(string path) => File.Exists(path);
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public IEnumerable<string> GetFiles(string directory, string searchPattern) => throw new NotSupportedException();
        public Stream OpenRead(string path) => File.OpenRead(path);
        public DateTime GetFileCreationTimeUtc(string path) => throw new IOException("metadata unavailable");
        public long GetFileSize(string path) { SizeRequested.TrySetResult(); throw new FileNotFoundException("rotating"); }
    }
}
