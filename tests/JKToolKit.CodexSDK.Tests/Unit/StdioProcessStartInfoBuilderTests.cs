using JKToolKit.CodexSDK.Infrastructure.Stdio;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class StdioProcessStartInfoBuilderTests
{
    [Fact]
    public void Create_SetsUtf8Encodings_AndEnsuresCodexHomeDirectoryExists()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"codex-stdio-tests-{Guid.NewGuid():N}");
        var codexHome = Path.Combine(tempRoot, "codex-home");

        Directory.Exists(codexHome).Should().BeFalse();

        try
        {
            var startInfo = StdioProcessStartInfoBuilder.Create(new ProcessLaunchOptions
            {
                ResolvedFileName = "codex",
                Arguments = new[] { "app-server" },
                WorkingDirectory = tempRoot,
                Environment = new Dictionary<string, string>
                {
                    ["CODEX_HOME"] = codexHome,
                    ["SOME_OTHER_ENV"] = "value"
                }
            });

            AssertUtf8Encodings(startInfo);
            Directory.Exists(codexHome).Should().BeTrue();
            startInfo.Environment["CODEX_HOME"].Should().Be(codexHome);
            startInfo.Environment["SOME_OTHER_ENV"].Should().Be("value");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static void AssertUtf8Encodings(ProcessStartInfo startInfo)
    {
        startInfo.StandardInputEncoding?.WebName.Should().Be("utf-8");
        startInfo.StandardOutputEncoding?.WebName.Should().Be("utf-8");
        startInfo.StandardErrorEncoding?.WebName.Should().Be("utf-8");
    }
}

