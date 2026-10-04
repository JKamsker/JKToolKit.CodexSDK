using JKToolKit.CodexSDK.Exec;
using JKToolKit.CodexSDK.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace JKToolKit.CodexSDK.Tests.Unit;

public sealed class SessionFilterRecordUpdateTests
{
    [Fact]
    public async Task OpenEndedRanges_AreValidAtConstructionAndConsumption()
    {
        var date = DateTimeOffset.UtcNow;
        await ConsumeWithEmptyDirectory(new SessionFilter(FromDate: date));
        await ConsumeWithEmptyDirectory(new SessionFilter(ToDate: date));
        await ConsumeWithEmptyDirectory(SessionFilter.None);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 2)]
    [InlineData(true, -2)]
    [InlineData(false, -2)]
    public async Task WithUpdate_CanMoveRangeInEitherAssignmentOrder(bool startFirst, int days)
    {
        var start = DateTimeOffset.UtcNow;
        var original = new SessionFilter(start, start.AddDays(1));
        var from = start.AddDays(days);
        var to = from.AddDays(1);
        var updated = startFirst
            ? original with { FromDate = from, ToDate = to }
            : original with { ToDate = to, FromDate = from };
        Assert.Equal(from, updated.FromDate);
        Assert.Equal(to, updated.ToDate);
        Assert.Equal(start, original.FromDate);
        Assert.Equal(start.AddDays(1), original.ToDate);
        await ConsumeWithEmptyDirectory(updated);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Initializer_AllowsEitherDateAssignmentOrder(bool startFirst)
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(1);
        var filter = startFirst
            ? new SessionFilter { FromDate = from, ToDate = to }
            : new SessionFilter { ToDate = to, FromDate = from };
        Assert.Equal(from, filter.FromDate);
        Assert.Equal(to, filter.ToDate);
        await ConsumeWithEmptyDirectory(filter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task InvalidFinalRange_IsRejectedAtClientAndLocatorEvenWithNoSessions(int construction)
    {
        var date = DateTimeOffset.UtcNow;
        var filter = construction switch
        {
            0 => new SessionFilter { FromDate = date, ToDate = date.AddTicks(-1) },
            1 => new SessionFilter { ToDate = date.AddTicks(-1), FromDate = date },
            2 => new SessionFilter(date, date) with { FromDate = date.AddTicks(1) },
            _ => new SessionFilter(date, date) with { ToDate = date.AddTicks(-1) }
        };
        using var client = new CodexClient();
        Assert.Equal("ToDate", Assert.Throws<ArgumentException>(() => client.ListSessionsAsync(filter)).ParamName);
        var error = await Assert.ThrowsAsync<ArgumentException>(() => ConsumeWithEmptyDirectory(filter));
        Assert.Equal("ToDate", error.ParamName);
    }

    private static async Task ConsumeWithEmptyDirectory(SessionFilter filter)
    {
        var root = Path.Combine(Path.GetTempPath(), "filter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var locator = new CodexSessionLocator(new RealFileSystem(), NullLogger<CodexSessionLocator>.Instance);
            await foreach (var session in locator.ListSessionsAsync(root, filter, default))
                Assert.Fail("An empty directory must not contain any sessions.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
