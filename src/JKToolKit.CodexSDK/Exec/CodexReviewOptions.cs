using System;
using System.Collections.Generic;
using System.IO;

namespace JKToolKit.CodexSDK.Exec;

/// <summary>
/// Represents configuration options for running a non-interactive code review via <c>codex review</c>.
/// </summary>
public class CodexReviewOptions
{
    private string? _workingDirectory;
    private string? _prompt;
    private string? _commitSha;
    private string? _baseBranch;
    private bool _uncommitted;
    private string? _title;
    private IReadOnlyList<string> _additionalOptions = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the working directory containing the repository to review.
    /// </summary>
    public string WorkingDirectory
    {
        get => _workingDirectory ?? throw new InvalidOperationException("WorkingDirectory is required but has not been set.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Working directory cannot be empty or whitespace.", nameof(WorkingDirectory));
            _workingDirectory = value;
        }
    }

    /// <summary>
    /// Gets or sets optional custom review instructions.
    /// </summary>
    /// <remarks>
    /// When set, the SDK passes <c>-</c> as the prompt argument and writes this text to stdin.
    /// </remarks>
    public string? Prompt
    {
        get => _prompt;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Prompt cannot be empty or whitespace when provided.", nameof(Prompt));
            _prompt = value;
        }
    }

    /// <summary>
    /// Gets or sets the commit SHA to review via <c>--commit</c>.
    /// </summary>
    public string? CommitSha
    {
        get => _commitSha;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("CommitSha cannot be empty or whitespace when provided.", nameof(CommitSha));
            _commitSha = value;
        }
    }

    /// <summary>
    /// Gets or sets the base branch to review against via <c>--base</c>.
    /// </summary>
    public string? BaseBranch
    {
        get => _baseBranch;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("BaseBranch cannot be empty or whitespace when provided.", nameof(BaseBranch));
            _baseBranch = value;
        }
    }

    /// <summary>
    /// Gets or sets whether to review staged/unstaged/untracked changes via <c>--uncommitted</c>.
    /// </summary>
    public bool Uncommitted
    {
        get => _uncommitted;
        set => _uncommitted = value;
    }

    /// <summary>
    /// Gets or sets an optional title to display in the review summary via <c>--title</c>.
    /// </summary>
    public string? Title
    {
        get => _title;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Title cannot be empty or whitespace when provided.", nameof(Title));
            _title = value;
        }
    }

    /// <summary>
    /// Gets or sets additional command-line options to pass directly to <c>codex review</c>.
    /// </summary>
    /// <remarks>
    /// Use this to pass <c>--config</c>, <c>--enable</c>, <c>--disable</c>, or any newer flags not
    /// represented by this type.
    /// </remarks>
    public IReadOnlyList<string> AdditionalOptions
    {
        get => _additionalOptions;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _additionalOptions = value;
        }
    }

    /// <summary>
    /// Gets or sets the path to the Codex CLI executable for this specific review.
    /// </summary>
    public string? CodexBinaryPath { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="CodexReviewOptions"/>.
    /// </summary>
    public CodexReviewOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="CodexReviewOptions"/> with the required working directory.
    /// </summary>
    public CodexReviewOptions(string workingDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    /// <summary>
    /// Validates that required fields are set and the option combination is coherent.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(_workingDirectory))
            throw new InvalidOperationException("WorkingDirectory is required and cannot be empty.");

        if (!Directory.Exists(_workingDirectory))
            throw new InvalidOperationException($"WorkingDirectory '{_workingDirectory}' does not exist.");

        if (_prompt is not null && string.IsNullOrWhiteSpace(_prompt))
            throw new InvalidOperationException("Prompt cannot be empty or whitespace when provided.");

        if (_commitSha is not null && string.IsNullOrWhiteSpace(_commitSha))
            throw new InvalidOperationException("CommitSha cannot be empty or whitespace when provided.");

        if (_baseBranch is not null && string.IsNullOrWhiteSpace(_baseBranch))
            throw new InvalidOperationException("BaseBranch cannot be empty or whitespace when provided.");

        if (_title is not null && string.IsNullOrWhiteSpace(_title))
            throw new InvalidOperationException("Title cannot be empty or whitespace when provided.");

        var targets = 0;
        if (!string.IsNullOrWhiteSpace(_commitSha)) targets++;
        if (!string.IsNullOrWhiteSpace(_baseBranch)) targets++;
        if (_uncommitted) targets++;
        if (targets > 1)
        {
            throw new InvalidOperationException("Only one of CommitSha, BaseBranch, or Uncommitted can be specified.");
        }
    }

    /// <summary>
    /// Creates a copy of the current options.
    /// </summary>
    public CodexReviewOptions Clone()
    {
        Validate();

        return new CodexReviewOptions(WorkingDirectory)
        {
            Prompt = Prompt,
            CommitSha = CommitSha,
            BaseBranch = BaseBranch,
            Uncommitted = Uncommitted,
            Title = Title,
            AdditionalOptions = new List<string>(AdditionalOptions),
            CodexBinaryPath = CodexBinaryPath
        };
    }
}

