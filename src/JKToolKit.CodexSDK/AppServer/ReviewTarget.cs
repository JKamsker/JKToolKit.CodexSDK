using System.Text.Json;

namespace JKToolKit.CodexSDK.AppServer;

/// <summary>
/// Represents a review target shape for <c>review/start</c>.
/// </summary>
public abstract record ReviewTarget
{
    private static readonly JsonSerializerOptions SerializerOptions = CodexAppServerClient.CreateDefaultSerializerOptions();

    private protected ReviewTarget() { }

    /// <summary>
    /// Review uncommitted changes in the current workspace.
    /// </summary>
    public sealed record UncommittedChanges : ReviewTarget;

    /// <summary>
    /// Review the diff against a base branch.
    /// </summary>
    public sealed record BaseBranch : ReviewTarget
    {
        /// <summary>
        /// Initializes a new instance of <see cref="BaseBranch"/>.
        /// </summary>
        public BaseBranch(string branch)
        {
            ArgumentNullException.ThrowIfNull(branch);
            Branch = branch;
        }

        /// <summary>
        /// Gets the base branch name.
        /// </summary>
        public string Branch { get; }
    }

    /// <summary>
    /// Review the diff for a specific commit.
    /// </summary>
    public sealed record Commit : ReviewTarget
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Commit"/>.
        /// </summary>
        public Commit(string sha, string? title)
        {
            ArgumentNullException.ThrowIfNull(sha);
            Sha = sha;
            Title = title;
        }

        /// <summary>
        /// Gets the commit SHA.
        /// </summary>
        public string Sha { get; }

        /// <summary>
        /// Gets an optional human-readable title (e.g. commit subject).
        /// </summary>
        public string? Title { get; }
    }

    /// <summary>
    /// Review using custom instructions.
    /// </summary>
    public sealed record Custom : ReviewTarget
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Custom"/>.
        /// </summary>
        public Custom(string instructions)
        {
            ArgumentNullException.ThrowIfNull(instructions);
            Instructions = instructions;
        }

        /// <summary>
        /// Gets the free-form reviewer instructions.
        /// </summary>
        public string Instructions { get; }
    }

    /// <summary>
    /// Use a raw/opaque wire target shape for maximum forward compatibility.
    /// </summary>
    public sealed record Raw : ReviewTarget
    {
        /// <summary>
        /// Initializes a new instance of <see cref="Raw"/>.
        /// </summary>
        public Raw(JsonElement wire) => Wire = wire;

        /// <summary>
        /// Gets the raw/opaque wire target shape.
        /// </summary>
        public JsonElement Wire { get; }
    }

    internal JsonElement ToWire() =>
        this switch
        {
            UncommittedChanges => JsonSerializer.SerializeToElement(new { type = "uncommittedChanges" }, SerializerOptions),
            BaseBranch b => JsonSerializer.SerializeToElement(new { type = "baseBranch", branch = b.Branch }, SerializerOptions),
            Commit c => JsonSerializer.SerializeToElement(new { type = "commit", sha = c.Sha, title = c.Title }, SerializerOptions),
            Custom c => JsonSerializer.SerializeToElement(new { type = "custom", instructions = c.Instructions }, SerializerOptions),
            Raw r => r.Wire,
            _ => throw new InvalidOperationException($"Unknown {nameof(ReviewTarget)} variant: {GetType().Name}")
        };
}
