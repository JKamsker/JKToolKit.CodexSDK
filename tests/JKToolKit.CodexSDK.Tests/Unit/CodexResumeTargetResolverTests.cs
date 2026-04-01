using FluentAssertions;
using JKToolKit.CodexSDK.Abstractions;
using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Exec.Internal;
using JKToolKit.CodexSDK.Exec.Protocol;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class CodexResumeTargetResolverTests
{
    [Fact]
    public async Task TryResolveAsync_ReturnsNewestSession_ForMostRecentTarget()
    {
        var older = CreateSession("id-1", "older", "C:\\repo", DateTimeOffset.Parse("2026-04-01T08:00:00Z"));
        var newer = CreateSession("id-2", "newer", "C:\\repo", DateTimeOffset.Parse("2026-04-01T09:00:00Z"));
        var locator = new RecordingSessionLocator(older, newer);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            "C:\\sessions",
            CodexResumeTarget.MostRecent(),
            workingDirectory: "C:\\repo",
            modelProvider: null,
            CancellationToken.None);

        resolved.Should().Be(newer);
        locator.LastFilter?.WorkingDirectory.Should().Be("C:\\repo");
    }

    [Fact]
    public async Task TryResolveAsync_PrefersMostRecentlyUpdatedSession_ForMostRecentTarget()
    {
        var newerCreated = CreateSession(
            "id-1",
            "newer-created",
            "C:\\repo",
            DateTimeOffset.Parse("2026-04-01T09:00:00Z"),
            updatedAt: DateTimeOffset.Parse("2026-04-01T09:00:00Z"));
        var olderCreatedButLatestUpdated = CreateSession(
            "id-2",
            "latest-updated",
            "C:\\repo",
            DateTimeOffset.Parse("2026-04-01T08:00:00Z"),
            updatedAt: DateTimeOffset.Parse("2026-04-01T10:00:00Z"));
        var locator = new RecordingSessionLocator(newerCreated, olderCreatedButLatestUpdated);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            "C:\\sessions",
            CodexResumeTarget.MostRecent(),
            workingDirectory: "C:\\repo",
            modelProvider: null,
            CancellationToken.None);

        resolved.Should().Be(olderCreatedButLatestUpdated);
    }

    [Fact]
    public async Task TryResolveAsync_PrefersExactIdMatch_OverHumanLabelMatch()
    {
        var idMatch = CreateSession("shared", "friendly", "C:\\repo", DateTimeOffset.Parse("2026-04-01T09:00:00Z"));
        var labelMatch = CreateSession("other", "shared", "C:\\repo", DateTimeOffset.Parse("2026-04-01T10:00:00Z"));
        var locator = new RecordingSessionLocator(labelMatch, idMatch);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            "C:\\sessions",
            CodexResumeTarget.BySelector("shared"),
            workingDirectory: "C:\\repo",
            modelProvider: null,
            CancellationToken.None);

        resolved.Should().Be(idMatch);
    }

    [Fact]
    public async Task TryResolveAsync_DropsWorkingDirectoryFilter_WhenIncludeAllSessionsIsRequested()
    {
        var outsideCwd = CreateSession("id-2", "remote-thread", "D:\\other", DateTimeOffset.Parse("2026-04-01T10:00:00Z"));
        var locator = new RecordingSessionLocator(outsideCwd);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            "C:\\sessions",
            CodexResumeTarget.BySelector("remote-thread", includeAllSessions: true),
            workingDirectory: "C:\\repo",
            modelProvider: null,
            CancellationToken.None);

        resolved.Should().Be(outsideCwd);
        locator.LastFilter.Should().BeNull();
    }

    [Fact]
    public async Task TryResolveAsync_DoesNotTreatThreadNamesAsCaseInsensitive()
    {
        var exactCase = CreateSession("id-1", "CaseSensitive", "C:\\repo", DateTimeOffset.Parse("2026-04-01T09:00:00Z"));
        var locator = new RecordingSessionLocator(exactCase);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            "C:\\sessions",
            CodexResumeTarget.BySelector("casesensitive"),
            workingDirectory: "C:\\repo",
            modelProvider: null,
            CancellationToken.None);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task TryResolveAsync_FiltersMostRecentByModelProvider()
    {
        var openAi = CreateSession(
            "id-openai",
            "openai",
            "C:\\repo",
            DateTimeOffset.Parse("2026-04-01T09:00:00Z"),
            updatedAt: DateTimeOffset.Parse("2026-04-01T09:00:00Z"),
            modelProvider: "openai");
        var custom = CreateSession(
            "id-custom",
            "custom",
            "C:\\repo",
            DateTimeOffset.Parse("2026-04-01T10:00:00Z"),
            updatedAt: DateTimeOffset.Parse("2026-04-01T10:00:00Z"),
            modelProvider: "custom");
        var locator = new RecordingSessionLocator(custom, openAi);

        var resolved = await CodexResumeTargetResolver.TryResolveAsync(
            locator,
            "C:\\sessions",
            CodexResumeTarget.MostRecent(),
            workingDirectory: "C:\\repo",
            modelProvider: "openai",
            CancellationToken.None);

        resolved.Should().Be(openAi);
        locator.LastFilter?.ModelProvider.Should().Be("openai");
    }

    private static CodexSessionInfo CreateSession(
        string id,
        string label,
        string cwd,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt = null,
        string? modelProvider = null) =>
        new(SessionId.Parse(id), $"C:\\sessions\\{id}.jsonl", createdAt, cwd, Model: null, HumanLabel: label, UpdatedAt: updatedAt, ModelProvider: modelProvider);

    private sealed class RecordingSessionLocator(params CodexSessionInfo[] sessions) : ICodexSessionLocator
    {
        private readonly IReadOnlyList<CodexSessionInfo> _sessions = sessions
            .OrderByDescending(session => session.UpdatedAt ?? session.CreatedAt)
            .ThenByDescending(session => session.CreatedAt)
            .ToArray();

        public SessionFilter? LastFilter { get; private set; }

        public Task<string> WaitForNewSessionFileAsync(string sessionsRoot, DateTimeOffset startTime, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> FindSessionLogAsync(SessionId sessionId, string sessionsRoot, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> WaitForSessionLogByIdAsync(SessionId sessionId, string sessionsRoot, TimeSpan timeout, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> ValidateLogFileAsync(string logFilePath, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<CodexSessionInfo> ListSessionsAsync(
            string sessionsRoot,
            SessionFilter? filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            LastFilter = filter;

            foreach (var session in _sessions)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (filter is not null &&
                    !string.IsNullOrWhiteSpace(filter.WorkingDirectory) &&
                    !string.Equals(filter.WorkingDirectory, session.WorkingDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (filter is not null &&
                    !string.IsNullOrWhiteSpace(filter.ModelProvider) &&
                    !string.Equals(filter.ModelProvider, session.ModelProvider, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return session;
                await Task.Yield();
            }
        }
    }
}
