using FluentAssertions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexSessionThreadNameIndexTests
{
    [Fact]
    public async Task ListSessionsAsync_PrefersThreadNameFromSessionIndex()
    {
        using var fixture = new SessionIndexFixture();
        var sessionId = "11111111-1111-1111-1111-111111111111";
        fixture.WriteSessionLog(
            sessionId,
            "C:\\repo",
            """
            {"type":"session_meta","timestamp":"2026-04-01T10:00:00Z","payload":{"id":"11111111-1111-1111-1111-111111111111","cwd":"C:\\repo","name":"Stale Name"}}
            """
        );
        fixture.WriteSessionIndex(
            """
            {"id":"11111111-1111-1111-1111-111111111111","thread_name":"Renamed Thread","updated_at":"2026-04-01T10:05:00Z"}
            """
        );

        var locator = new CodexSessionLocator(new RealFileSystem(), NullLogger<CodexSessionLocator>.Instance);

        var sessions = await ToListAsync(locator.ListSessionsAsync(fixture.SessionsRoot, filter: null, CancellationToken.None));

        sessions.Should().ContainSingle();
        sessions[0].HumanLabel.Should().Be("Renamed Thread");
    }

    [Fact]
    public async Task TryResolveAsync_UsesSessionIndexNames_WhileRespectingWorkingDirectoryFilter()
    {
        using var fixture = new SessionIndexFixture();
        fixture.WriteSessionLog(
            "11111111-1111-1111-1111-111111111111",
            "D:\\other",
            string.Join(
                Environment.NewLine,
                """{"type":"session_meta","timestamp":"2026-04-01T11:00:00Z","payload":{"id":"11111111-1111-1111-1111-111111111111","cwd":"D:\\other","name":"Old Outside"}}""",
                """{"type":"turn_context","timestamp":"2026-04-01T11:05:00Z","payload":{"cwd":"D:\\other","model":"gpt-5.2-codex"}}"""
            ));
        fixture.WriteSessionLog(
            "22222222-2222-2222-2222-222222222222",
            "C:\\repo",
            string.Join(
                Environment.NewLine,
                """{"type":"session_meta","timestamp":"2026-04-01T10:00:00Z","payload":{"id":"22222222-2222-2222-2222-222222222222","cwd":"C:\\repo","name":"Old Inside"}}""",
                """{"type":"turn_context","timestamp":"2026-04-01T10:05:00Z","payload":{"cwd":"C:\\repo","model":"gpt-5.2-codex"}}"""
            ));
        fixture.WriteSessionIndex(
            string.Join(
                Environment.NewLine,
                """{"id":"11111111-1111-1111-1111-111111111111","thread_name":"Shared Name","updated_at":"2026-04-01T11:06:00Z"}""",
                """{"id":"22222222-2222-2222-2222-222222222222","thread_name":"Shared Name","updated_at":"2026-04-01T10:06:00Z"}"""
            ));

        var locator = new CodexSessionLocator(new RealFileSystem(), NullLogger<CodexSessionLocator>.Instance);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            fixture.SessionsRoot,
            CodexResumeTarget.BySelector("Shared Name"),
            workingDirectory: "C:\\repo",
            modelProvider: null,
            CancellationToken.None);

        resolved.Should().NotBeNull();
        resolved!.Id.Value.Should().Be("22222222-2222-2222-2222-222222222222");
        resolved.HumanLabel.Should().Be("Shared Name");
    }

    [Fact]
    public async Task ListSessionsAsync_UsesExplicitCodexHomeDirectory_WhenSessionsRootIsCustom()
    {
        using var fixture = new SessionIndexFixture(customSessionsRootLeaf: "custom-rollouts");
        var sessionId = "33333333-3333-3333-3333-333333333333";
        fixture.WriteSessionLog(
            sessionId,
            "C:\\repo",
            """
            {"type":"session_meta","timestamp":"2026-04-01T12:00:00Z","payload":{"id":"33333333-3333-3333-3333-333333333333","cwd":"C:\\repo","name":"Old Name"}}
            """
        );
        fixture.WriteSessionIndex(
            """
            {"id":"33333333-3333-3333-3333-333333333333","thread_name":"Renamed With Explicit Home","updated_at":"2026-04-01T12:05:00Z"}
            """
        );

        var locator = new CodexSessionLocator(new RealFileSystem(), NullLogger<CodexSessionLocator>.Instance, fixture.CodexHome);
        var sessions = await ToListAsync(locator.ListSessionsAsync(fixture.SessionsRoot, filter: null, CancellationToken.None));

        sessions.Should().ContainSingle();
        sessions[0].HumanLabel.Should().Be("Renamed With Explicit Home");
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source.ConfigureAwait(false))
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class SessionIndexFixture : IDisposable
    {
        private readonly string _root;

        public SessionIndexFixture(string customSessionsRootLeaf = "sessions")
        {
            _root = Path.Combine(Path.GetTempPath(), $"codex-session-index-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
            SessionsRoot = Path.Combine(_root, customSessionsRootLeaf);
            Directory.CreateDirectory(SessionsRoot);
        }

        public string CodexHome => _root;

        public string SessionsRoot { get; }

        public void WriteSessionLog(string sessionId, string cwd, string body)
        {
            var path = Path.Combine(
                SessionsRoot,
                "2026",
                "04",
                "01",
                $"rollout-2026-04-01T10-00-00-{sessionId}.jsonl");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, body);
        }

        public void WriteSessionIndex(string body)
        {
            var path = Path.Combine(_root, "session_index.jsonl");
            File.WriteAllText(path, body + Environment.NewLine);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_root))
                {
                    Directory.Delete(_root, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup for test fixtures only.
            }
        }
    }
}
