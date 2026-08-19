using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a user-visible thread section.
/// </summary>
public sealed record class ThreadSectionDescriptor
{
    /// <summary>
    /// Gets the stable section identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the section display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets optional synchronized presentation metadata for the section.
    /// </summary>
    public ThreadSectionAppearance? Appearance { get; init; }

    /// <summary>
    /// Gets the raw section payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Optional visual presentation for a custom thread section.
/// </summary>
public sealed record class ThreadSectionAppearance
{
    /// <summary>
    /// Gets the color token or value returned by upstream.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Gets the icon token returned by upstream.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// Gets the raw appearance payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>threadSection/list</c>.
/// </summary>
public sealed class ThreadSectionListOptions
{
    /// <summary>
    /// Gets or sets an optional cursor for paging.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }
}

/// <summary>
/// Represents a page returned by <c>threadSection/list</c>.
/// </summary>
public sealed record class ThreadSectionListPage
{
    /// <summary>
    /// Gets the returned sections.
    /// </summary>
    public required IReadOnlyList<ThreadSectionDescriptor> Sections { get; init; }

    /// <summary>
    /// Gets the next cursor token, if any.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw list payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Options for <c>threadSection/create</c>.
/// </summary>
public sealed class ThreadSectionCreateOptions
{
    /// <summary>
    /// Gets or sets the section display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets optional synchronized presentation metadata for the section.
    /// </summary>
    public ThreadSectionAppearanceOptions? Appearance { get; set; }
}

/// <summary>
/// Options for <c>threadSection/update</c>.
/// </summary>
public sealed class ThreadSectionUpdateOptions
{
    /// <summary>
    /// Gets or sets the section identifier.
    /// </summary>
    public required string SectionId { get; set; }

    /// <summary>
    /// Gets or sets the updated section display name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets replacement section appearance. Set <see cref="ClearAppearance"/> to clear it.
    /// </summary>
    public ThreadSectionAppearanceOptions? Appearance { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether section appearance should be cleared.
    /// </summary>
    public bool ClearAppearance { get; set; }
}

/// <summary>
/// Appearance values sent to thread section create/update requests.
/// </summary>
public sealed class ThreadSectionAppearanceOptions
{
    /// <summary>
    /// Gets or sets the color token or value.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets the icon token.
    /// </summary>
    public string? Icon { get; set; }
}

/// <summary>
/// Options for <c>threadSection/delete</c>.
/// </summary>
public sealed class ThreadSectionDeleteOptions
{
    /// <summary>
    /// Gets or sets the section identifier.
    /// </summary>
    public required string SectionId { get; set; }
}

/// <summary>
/// Options for <c>thread/section/move</c>.
/// </summary>
public sealed class ThreadSectionMoveOptions
{
    /// <summary>
    /// Gets or sets the thread identifier to move.
    /// </summary>
    public required string ThreadId { get; set; }

    /// <summary>
    /// Gets or sets the destination section identifier. Null removes the thread from its section.
    /// </summary>
    public string? SectionId { get; set; }

    /// <summary>
    /// Gets or sets the thread identifier to insert before. Null appends to the destination section.
    /// </summary>
    public string? BeforeThreadId { get; set; }
}

/// <summary>
/// Result returned by thread section mutation requests.
/// </summary>
public sealed record class ThreadSectionResult
{
    /// <summary>
    /// Gets the section returned by create or update operations.
    /// </summary>
    public ThreadSectionDescriptor? Section { get; init; }

    /// <summary>
    /// Gets the raw response payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
