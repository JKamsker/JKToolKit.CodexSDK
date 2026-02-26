using System.Diagnostics;

using System.Text;
using JKToolKit.CodexSDK.Infrastructure.Internal;

namespace JKToolKit.CodexSDK.Infrastructure.Stdio;

internal static class StdioProcessStartInfoBuilder
{
    public static ProcessStartInfo Create(ProcessLaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ResolvedFileName))
            throw new ArgumentException("ResolvedFileName cannot be empty or whitespace.", nameof(options));

        var startInfo = new ProcessStartInfo
        {
            FileName = options.ResolvedFileName,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        startInfo.StandardInputEncoding = utf8NoBom;
        startInfo.StandardOutputEncoding = utf8NoBom;
        startInfo.StandardErrorEncoding = utf8NoBom;

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        foreach (var argument in options.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in options.Environment)
        {
            startInfo.Environment[key] = value;
        }

        if (options.Environment.TryGetValue("CODEX_HOME", out var codexHomeDirectory))
        {
            CodexHomeDirectoryHelpers.EnsureExists(codexHomeDirectory);
        }

        return startInfo;
    }
}

