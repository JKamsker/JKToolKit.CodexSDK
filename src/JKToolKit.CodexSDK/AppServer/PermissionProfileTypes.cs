using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Options for <c>permissionProfile/list</c>.
/// </summary>
public sealed class PermissionProfileListOptions
{
    /// <summary>
    /// Gets or sets an optional pagination cursor.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Gets or sets an optional page size.
    /// </summary>
    public int? Limit { get; set; }

    /// <summary>
    /// Gets or sets the working directory used to resolve project config layers.
    /// </summary>
    public string? Cwd { get; set; }
}

/// <summary>
/// Represents a permission profile available to the app-server.
/// </summary>
public sealed record class PermissionProfileSummary
{
    /// <summary>
    /// Gets the profile identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the optional user-facing description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the raw profile payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}

/// <summary>
/// Represents a page returned by <c>permissionProfile/list</c>.
/// </summary>
public sealed record class PermissionProfileListPage
{
    /// <summary>
    /// Gets the profiles returned for this page.
    /// </summary>
    public required IReadOnlyList<PermissionProfileSummary> Profiles { get; init; }

    /// <summary>
    /// Gets the next cursor token, if any.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Gets the raw JSON payload.
    /// </summary>
    public required JsonElement Raw { get; init; }
}
