using System.Diagnostics;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Protocol;
using Microsoft.Extensions.Logging;

namespace JKToolKit.CodexSDK.Infrastructure.Internal;

internal static class CodexProcessStartInfoFactory
{
    private const string CodexHomeEnvVar = "CODEX_HOME";

    internal static ProcessStartInfo CreateSessionStartInfo(
        ICodexPathProvider pathProvider,
        ILogger logger,
        CodexSessionOptions options,
        CodexClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        options.Validate();
        clientOptions.Validate();

        var codexPath = pathProvider.GetCodexExecutablePath(
            options.CodexBinaryPath ?? clientOptions.CodexExecutablePath);

        logger.LogDebug("Using Codex executable at: {Path}", codexPath);

        var startInfo = ProcessStartInfoBuilder.Create(codexPath, options);
        ApplyCodexHome(startInfo, clientOptions);
        return startInfo;
    }

    internal static ProcessStartInfo CreateResumeStartInfo(
        ICodexPathProvider pathProvider,
        ILogger logger,
        CodexResumeTarget target,
        CodexSessionOptions options,
        CodexClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        options.Validate();
        clientOptions.Validate();
        target.Validate();

        var codexPath = pathProvider.GetCodexExecutablePath(
            options.CodexBinaryPath ?? clientOptions.CodexExecutablePath);

        logger.LogDebug("Using Codex executable at: {Path}", codexPath);

        var startInfo = ProcessStartInfoBuilder.CreateResume(codexPath, target, options);
        ApplyCodexHome(startInfo, clientOptions);
        return startInfo;
    }

    internal static ProcessStartInfo CreateReviewStartInfo(
        ICodexPathProvider pathProvider,
        ILogger logger,
        CodexReviewOptions options,
        CodexClientOptions clientOptions)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clientOptions);

        options.Validate();
        clientOptions.Validate();

        var codexPath = pathProvider.GetCodexExecutablePath(
            options.CodexBinaryPath ?? clientOptions.CodexExecutablePath);

        logger.LogDebug("Using Codex executable at: {Path}", codexPath);

        var startInfo = ProcessStartInfoBuilder.CreateReview(codexPath, options);
        ApplyCodexHome(startInfo, clientOptions);
        return startInfo;
    }

    private static void ApplyCodexHome(ProcessStartInfo startInfo, CodexClientOptions clientOptions)
    {
        if (string.IsNullOrWhiteSpace(clientOptions.CodexHomeDirectory))
        {
            return;
        }

        CodexHomeDirectoryHelpers.EnsureExists(clientOptions.CodexHomeDirectory);
        startInfo.Environment[CodexHomeEnvVar] = clientOptions.CodexHomeDirectory;
    }
}
