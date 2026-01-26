using System.Diagnostics;

namespace NCodexSDK.Infrastructure.Stdio;

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

        return startInfo;
    }
}

