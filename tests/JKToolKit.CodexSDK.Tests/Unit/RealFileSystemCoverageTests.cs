using FluentAssertions;
using JKToolKit.CodexSDK.Infrastructure;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class RealFileSystemCoverageTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyPaths_ReturnFalseOrRejectInvalidOperation(string? path)
    {
        var fs = new RealFileSystem();
        fs.FileExists(path!).Should().BeFalse();
        fs.DirectoryExists(path!).Should().BeFalse();
        Assert.Throws<ArgumentNullException>(() => fs.GetFiles(path!, "*"));
        Assert.Throws<ArgumentNullException>(() => fs.GetFiles(Path.GetTempPath(), path!));
        Assert.Throws<ArgumentException>(() => fs.OpenRead(path!));
        Assert.Throws<ArgumentNullException>(() => fs.GetFileCreationTimeUtc(path!));
        Assert.Throws<ArgumentNullException>(() => fs.GetFileSize(path!));
    }

    [Fact]
    public void Filesystem_EnumeratesRecursivelyAndPermitsConcurrentWriter()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(root, "nested"));
        try
        {
            var path = Path.Combine(root, "nested", "session.jsonl");
            using var writer = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            writer.Write([1, 2, 3]);
            writer.Flush();
            var fs = new RealFileSystem();
            fs.DirectoryExists(root).Should().BeTrue();
            fs.FileExists(path).Should().BeTrue();
            fs.GetFiles(root, "*.jsonl").Should().Equal(path);
            fs.GetFileSize(path).Should().Be(3);
            fs.GetFileCreationTimeUtc(path).Should().Be(File.GetCreationTimeUtc(path));
            using var reader = fs.OpenRead(path);
            reader.ReadByte().Should().Be(1);
            reader.CanWrite.Should().BeFalse();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void MissingPaths_PreservePathAndUnderlyingException()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fs = new RealFileSystem();
        Assert.Throws<DirectoryNotFoundException>(() => fs.GetFiles(path, "*")).InnerException.Should().BeOfType<DirectoryNotFoundException>();
        var open = Assert.Throws<FileNotFoundException>(() => fs.OpenRead(path));
        open.FileName.Should().Be(path);
        open.InnerException.Should().BeOfType<FileNotFoundException>();
        var size = Assert.Throws<FileNotFoundException>(() => fs.GetFileSize(path));
        size.FileName.Should().Be(path);
        size.InnerException.Should().BeOfType<FileNotFoundException>();
    }

    [UnixPermissionFact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void DeniedDirectoryPreservesUnauthorizedAccessFailures()
    {
        var directory = Directory.CreateTempSubdirectory("codex-denied-");
        var path = Path.Combine(directory.FullName, "file.jsonl");
        File.WriteAllText(path, "contents");
        var originalMode = File.GetUnixFileMode(directory.FullName);
        try
        {
            File.SetUnixFileMode(directory.FullName, UnixFileMode.None);
            var fs = new RealFileSystem();
            Assert.Throws<UnauthorizedAccessException>(() => fs.GetFiles(directory.FullName, "*")).InnerException.Should().BeOfType<UnauthorizedAccessException>();
            Assert.Throws<UnauthorizedAccessException>(() => fs.OpenRead(path)).InnerException.Should().BeOfType<UnauthorizedAccessException>();
            Assert.Throws<UnauthorizedAccessException>(() => fs.GetFileSize(path)).InnerException.Should().BeOfType<UnauthorizedAccessException>();
            Assert.Throws<UnauthorizedAccessException>(() => fs.GetFileCreationTimeUtc(path)).InnerException.Should().BeOfType<UnauthorizedAccessException>();
        }
        finally
        {
            File.SetUnixFileMode(directory.FullName, originalMode);
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public void InvalidPaths_WrapIoExceptions()
    {
        var fs = new RealFileSystem();
        Assert.Throws<IOException>(() => fs.GetFiles("\0", "*")).InnerException.Should().BeOfType<ArgumentException>();
        Assert.Throws<IOException>(() => fs.OpenRead("\0")).InnerException.Should().BeOfType<ArgumentException>();
        Assert.Throws<IOException>(() => fs.GetFileSize("\0")).InnerException.Should().BeOfType<ArgumentException>();
        Assert.Throws<IOException>(() => fs.GetFileCreationTimeUtc("\0")).InnerException.Should().BeOfType<ArgumentException>();
        var directory = Directory.CreateTempSubdirectory("codex-open-directory-");
        try
        {
            // A trailing separator changes Windows File.Open's error to path-not-found.
            var directoryPath = Path.TrimEndingDirectorySeparator(directory.FullName);
            Assert.Throws<UnauthorizedAccessException>(() => fs.OpenRead(directoryPath)).InnerException.Should().BeOfType<UnauthorizedAccessException>();
        }
        finally { directory.Delete(); }
    }
}

internal sealed class UnixPermissionFactAttribute : FactAttribute
{
    public UnixPermissionFactAttribute()
    {
        if (OperatingSystem.IsWindows() || Environment.UserName == "root")
            Skip = "Requires a non-root Unix process to enforce directory access bits.";
    }
}
