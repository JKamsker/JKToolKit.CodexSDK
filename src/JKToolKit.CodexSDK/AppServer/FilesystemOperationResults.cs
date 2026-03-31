using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>fs/readFile</c>.
/// </summary>
public sealed class FsReadFileOptions
{
    /// <summary>
    /// Gets or sets the file path to read.
    /// </summary>
    public required string Path { get; set; }
}

/// <summary>
/// Result returned by <c>fs/readFile</c>.
/// </summary>
public sealed record class FsReadFileResult
{
    /// <summary>
    /// Gets the file contents encoded as base64.
    /// </summary>
    public required string DataBase64 { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/writeFile</c>.
/// </summary>
public sealed class FsWriteFileOptions
{
    /// <summary>
    /// Gets or sets the destination file path.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the file contents encoded as base64.
    /// </summary>
    public required string DataBase64 { get; set; }
}

/// <summary>
/// Result returned by <c>fs/writeFile</c>.
/// </summary>
public sealed record class FsWriteFileResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/createDirectory</c>.
/// </summary>
public sealed class FsCreateDirectoryOptions
{
    /// <summary>
    /// Gets or sets the directory path to create.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets whether parent directories should also be created.
    /// </summary>
    public bool? Recursive { get; set; }
}

/// <summary>
/// Result returned by <c>fs/createDirectory</c>.
/// </summary>
public sealed record class FsCreateDirectoryResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/getMetadata</c>.
/// </summary>
public sealed class FsGetMetadataOptions
{
    /// <summary>
    /// Gets or sets the path to inspect.
    /// </summary>
    public required string Path { get; set; }
}

/// <summary>
/// Result returned by <c>fs/getMetadata</c>.
/// </summary>
public sealed record class FsGetMetadataResult
{
    /// <summary>
    /// Gets whether the path resolves to a file.
    /// </summary>
    public bool IsFile { get; init; }

    /// <summary>
    /// Gets whether the path resolves to a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Gets the file creation time in Unix milliseconds when available.
    /// </summary>
    public long CreatedAtMs { get; init; }

    /// <summary>
    /// Gets the file modification time in Unix milliseconds when available.
    /// </summary>
    public long ModifiedAtMs { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/readDirectory</c>.
/// </summary>
public sealed class FsReadDirectoryOptions
{
    /// <summary>
    /// Gets or sets the directory path to enumerate.
    /// </summary>
    public required string Path { get; set; }
}

/// <summary>
/// A directory entry returned by <c>fs/readDirectory</c>.
/// </summary>
public sealed record class FsDirectoryEntry
{
    /// <summary>
    /// Gets the direct child entry name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets whether the entry resolves to a file.
    /// </summary>
    public bool IsFile { get; init; }

    /// <summary>
    /// Gets whether the entry resolves to a directory.
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// Gets the raw JSON payload for the entry.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Result returned by <c>fs/readDirectory</c>.
/// </summary>
public sealed record class FsReadDirectoryResult
{
    /// <summary>
    /// Gets the returned directory entries.
    /// </summary>
    public required IReadOnlyList<FsDirectoryEntry> Entries { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/remove</c>.
/// </summary>
public sealed class FsRemoveOptions
{
    /// <summary>
    /// Gets or sets the file or directory path to remove.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets whether directory removal should recurse.
    /// </summary>
    public bool? Recursive { get; set; }

    /// <summary>
    /// Gets or sets whether missing paths should be ignored.
    /// </summary>
    public bool? Force { get; set; }
}

/// <summary>
/// Result returned by <c>fs/remove</c>.
/// </summary>
public sealed record class FsRemoveResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>fs/copy</c>.
/// </summary>
public sealed class FsCopyOptions
{
    /// <summary>
    /// Gets or sets the source path to copy from.
    /// </summary>
    public required string SourcePath { get; set; }

    /// <summary>
    /// Gets or sets the destination path to copy to.
    /// </summary>
    public required string DestinationPath { get; set; }

    /// <summary>
    /// Gets or sets whether directory copies should recurse.
    /// </summary>
    public bool? Recursive { get; set; }
}

/// <summary>
/// Result returned by <c>fs/copy</c>.
/// </summary>
public sealed record class FsCopyResult
{
    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
