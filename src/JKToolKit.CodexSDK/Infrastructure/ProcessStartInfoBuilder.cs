using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using JKToolKit.CodexSDK.Models;
using JKToolKit.CodexSDK.StructuredOutputs;

namespace JKToolKit.CodexSDK.Infrastructure;

/// <summary>
/// Builds <see cref="ProcessStartInfo"/> instances for launching the Codex CLI.
/// </summary>
/// <remarks>
/// Encapsulates argument ordering and formatting so it can be unit tested without
/// spinning up real processes.
/// </remarks>
internal static class ProcessStartInfoBuilder
{
    /// <summary>
    /// Creates a configured <see cref="ProcessStartInfo"/> for <c>codex exec</c>.
    /// </summary>
    /// <param name="executablePath">Resolved Codex executable path.</param>
    /// <param name="options">Validated session options.</param>
    /// <returns>Populated <see cref="ProcessStartInfo"/> ready for launch.</returns>
    public static ProcessStartInfo Create(string executablePath, CodexSessionOptions options)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or whitespace.", nameof(executablePath));
        }

        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        ConfigureUtf8RedirectedEncodings(startInfo);

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--cd");
        startInfo.ArgumentList.Add(options.WorkingDirectory);
        AddOptionalModelAndReasoningOverrides(startInfo, options);

        if (options.OutputSchema is { Kind: CodexOutputSchemaKind.File, FilePath: { } schemaPath })
        {
            startInfo.ArgumentList.Add("--output-schema");
            startInfo.ArgumentList.Add(schemaPath);
        }

        AddImages(startInfo, options);

        foreach (var option in options.AdditionalOptions)
        {
            startInfo.ArgumentList.Add(option);
        }

        startInfo.ArgumentList.Add(options.CommandPromptToken);

        return startInfo;
    }

    /// <summary>
    /// Creates a configured <see cref="ProcessStartInfo"/> for <c>codex exec resume</c>.
    /// </summary>
    /// <param name="executablePath">Resolved Codex executable path.</param>
    /// <param name="sessionId">Session identifier to resume.</param>
    /// <param name="options">Validated session options.</param>
    /// <returns>Populated <see cref="ProcessStartInfo"/> ready for launch.</returns>
    public static ProcessStartInfo CreateResume(string executablePath, SessionId sessionId, CodexSessionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var target = options.ResumeTargetOverride ?? CodexResumeTarget.BySelector(sessionId.Value);
        return CreateResume(executablePath, target, options);
    }

    public static ProcessStartInfo CreateResume(string executablePath, CodexResumeTarget target, CodexSessionOptions options)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or whitespace.", nameof(executablePath));
        }

        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        target.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        ConfigureUtf8RedirectedEncodings(startInfo);

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--cd");
        startInfo.ArgumentList.Add(options.WorkingDirectory);
        AddOptionalModelAndReasoningOverrides(startInfo, options);

        if (options.OutputSchema is { Kind: CodexOutputSchemaKind.File, FilePath: { } schemaPath })
        {
            startInfo.ArgumentList.Add("--output-schema");
            startInfo.ArgumentList.Add(schemaPath);
        }

        AddImages(startInfo, options);

        foreach (var option in options.AdditionalOptions)
        {
            startInfo.ArgumentList.Add(option);
        }

        startInfo.ArgumentList.Add("resume");
        if (target.IncludeAllSessions)
        {
            startInfo.ArgumentList.Add("--all");
        }

        if (target.UseMostRecent)
        {
            startInfo.ArgumentList.Add("--last");
        }
        else
        {
            startInfo.ArgumentList.Add(target.Selector!);
        }

        startInfo.ArgumentList.Add(options.CommandPromptToken);

        return startInfo;
    }

    /// <summary>
    /// Creates a configured <see cref="ProcessStartInfo"/> for <c>codex review</c>.
    /// </summary>
    /// <param name="executablePath">Resolved Codex executable path.</param>
    /// <param name="options">Validated review options.</param>
    /// <returns>Populated <see cref="ProcessStartInfo"/> ready for launch.</returns>
    public static ProcessStartInfo CreateReview(string executablePath, CodexReviewOptions options)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or whitespace.", nameof(executablePath));
        }

        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = options.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        ConfigureUtf8RedirectedEncodings(startInfo);

        // `--cd` is a global option (before the subcommand).
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(options.WorkingDirectory);
 
        startInfo.ArgumentList.Add("review");
 
        var useStdinPrompt = !string.IsNullOrWhiteSpace(options.Prompt);

        if (!useStdinPrompt && options.Uncommitted)
        {
            startInfo.ArgumentList.Add("--uncommitted");
        }
        else if (!useStdinPrompt && !string.IsNullOrWhiteSpace(options.BaseBranch))
        {
            startInfo.ArgumentList.Add("--base");
            startInfo.ArgumentList.Add(options.BaseBranch);
        }
        else if (!useStdinPrompt && !string.IsNullOrWhiteSpace(options.CommitSha))
        {
            startInfo.ArgumentList.Add("--commit");
            startInfo.ArgumentList.Add(options.CommitSha);

            if (!string.IsNullOrWhiteSpace(options.Title))
            {
                startInfo.ArgumentList.Add("--title");
                startInfo.ArgumentList.Add(options.Title);
            }
        }
        else if (!useStdinPrompt)
        {
            throw new InvalidOperationException("No review target provided. This should have been rejected by validation.");
        }
 
        foreach (var option in options.AdditionalOptions)
        {
            startInfo.ArgumentList.Add(option);
        }

        if (useStdinPrompt)
        {
            // Prompt-only reviews use stdin (`-`) for instructions.
            startInfo.ArgumentList.Add("-");
        }
 
        return startInfo;
    }

    /// <summary>
    /// Formats the arguments for diagnostic logging.
    /// </summary>
    public static string FormatArguments(ProcessStartInfo startInfo)
    {
        if (startInfo.ArgumentList.Count == 0)
        {
            return startInfo.Arguments;
        }

        return string.Join(" ", startInfo.ArgumentList.Select(QuoteForLogging));
    }

    private static string QuoteForLogging(string value) =>
        value.Any(char.IsWhiteSpace) || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;

    private static void ConfigureUtf8RedirectedEncodings(ProcessStartInfo startInfo)
    {
        // Avoid platform-default codepages (especially on Windows) leaking into JSON-RPC/JSONL streams.
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        startInfo.StandardInputEncoding = utf8NoBom;
        startInfo.StandardOutputEncoding = utf8NoBom;
        startInfo.StandardErrorEncoding = utf8NoBom;
    }

    private static void AddOptionalModelAndReasoningOverrides(ProcessStartInfo startInfo, CodexSessionOptions options)
    {
        if (options.HasExplicitModelOverride)
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(options.Model.Value);
        }

        if (options.HasExplicitReasoningEffortOverride)
        {
            startInfo.ArgumentList.Add("--config");
            startInfo.ArgumentList.Add($"model_reasoning_effort={options.ReasoningEffort.Value}");
        }
    }

    private static void AddImages(ProcessStartInfo startInfo, CodexSessionOptions options)
    {
        foreach (var imagePath in options.Images)
        {
            startInfo.ArgumentList.Add("--image");
            startInfo.ArgumentList.Add(imagePath);
        }
    }
}
