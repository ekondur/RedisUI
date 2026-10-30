using RedisUI.Infra;
using RedisUI.Models;
using Xunit;

namespace RedisUI.Tests;

public class RedisKeyPagerTests
{
    private static Task<KeyModel> Metadata(string name) => Task.FromResult(new KeyModel { Name = name, KeyType = "String" });

    [Theory]
    [InlineData(0L)]
    [InlineData(42L)]
    public async Task Overflow_IsConsumedFromOriginalBatch_EvenIfRepeatedScanWouldChange(long nextCursor)
    {
        using var pager = new RedisKeyPager(new RedisUISettings());
        var firstBatchCalls = 0;
        Task<RedisKeyPager.ScanBatch> Scan(long cursor, int count)
        {
            if (cursor == 0)
            {
                firstBatchCalls++;
                // A repeated SCAN at this cursor would move "d" into the skipped prefix.
                return Task.FromResult(new RedisKeyPager.ScanBatch(nextCursor,
                    firstBatchCalls == 1 ? ["a", "b", "c", "d", "e"] : ["d", "c", "b", "a", "e"]));
            }
            Assert.Equal(42L, cursor);
            return Task.FromResult(new RedisKeyPager.ScanBatch(0, ["f"]));
        }

        var first = await pager.ReadAsync(0, 0, 3, 0, "*", Scan, Metadata);
        Assert.Equal(new[] { "a", "b", "c" }, first.Keys.Select(x => x.Name));
        Assert.NotEqual(0, first.NextPageOffset);
        var second = await pager.ReadAsync(0, first.NextCursor, 3, first.NextPageOffset, "*", Scan, Metadata);
        Assert.Equal(nextCursor == 0 ? new[] { "d", "e" } : new[] { "d", "e", "f" }, second.Keys.Select(x => x.Name));
        Assert.Equal(1, firstBatchCalls);
        Assert.Equal(0, second.NextCursor);
        Assert.Equal(0, second.NextPageOffset);
    }

    [Fact]
    public async Task OversizedBatch_SupportsManyPagesAndReplayWithoutRescanning()
    {
        using var pager = new RedisKeyPager(new RedisUISettings());
        var expected = Enumerable.Range(0, 17).Select(x => x.ToString()).ToArray();
        var calls = 0;
        Task<RedisKeyPager.ScanBatch> Scan(long cursor, int count)
        { calls++; return Task.FromResult(new RedisKeyPager.ScanBatch(0, expected)); }
        var first = await pager.ReadAsync(0, 0, 2, 0, null, Scan, Metadata);
        var seen = first.Keys.Select(x => x.Name).ToList();
        var page = first;
        while (page.NextPageOffset != 0)
        {
            page = await pager.ReadAsync(0, page.NextCursor, 2, page.NextPageOffset, null, Scan, Metadata);
            Assert.InRange(page.Keys.Count, 1, 2);
            seen.AddRange(page.Keys.Select(x => x.Name));
        }
        Assert.Equal(expected, seen);
        var replay = await pager.ReadAsync(0, first.NextCursor, 2, first.NextPageOffset, null, Scan, Metadata);
        Assert.Equal(new[] { "2", "3" }, replay.Keys.Select(x => x.Name));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task EmptyBatches_RespectScanBudget_AndKeepNextCursor()
    {
        using var pager = new RedisKeyPager(new RedisUISettings { MaxScanIterationsPerPage = 2 });
        var cursors = new List<long>();
        Task<RedisKeyPager.ScanBatch> Scan(long cursor, int count)
        { cursors.Add(cursor); return Task.FromResult(new RedisKeyPager.ScanBatch(cursor + 1, [])); }
        var page = await pager.ReadAsync(0, 0, 3, 0, "rare:*", Scan, Metadata);
        Assert.Empty(page.Keys);
        Assert.Equal(new long[] { 0, 1 }, cursors);
        Assert.Equal(2, page.NextCursor);
        Assert.Equal(0, page.NextPageOffset);
    }

    [Fact]
    public async Task ExpiredKeysInBufferedBatch_AreConsumedWithoutDroppingRemainingKeys()
    {
        using var pager = new RedisKeyPager(new RedisUISettings());
        Task<RedisKeyPager.ScanBatch> Scan(long cursor, int count) => Task.FromResult(new RedisKeyPager.ScanBatch(0, ["a", "gone", "b", "c"]));
        var first = await pager.ReadAsync(0, 0, 1, 0, null, Scan, Metadata);
        var next = await pager.ReadAsync(0, first.NextCursor, 2, first.NextPageOffset, null, Scan,
            name => Task.FromResult(new KeyModel { Name = name, KeyType = name == "gone" ? "None" : "String" }));
        Assert.Equal(new[] { "b", "c" }, next.Keys.Select(x => x.Name));
        Assert.Equal(0, next.NextPageOffset);
    }

    [Fact]
    public async Task Continuations_AreScopedToDatabasePatternAndCursor_AndExpireExplicitly()
    {
        var now = DateTimeOffset.UtcNow;
        using var pager = new RedisKeyPager(new RedisUISettings(), utcNow: () => now);
        Task<RedisKeyPager.ScanBatch> Scan(long cursor, int count) => Task.FromResult(new RedisKeyPager.ScanBatch(42, ["a", "b"]));
        var first = await pager.ReadAsync(0, 0, 1, 0, "prefix:*", Scan, Metadata);
        await Assert.ThrowsAsync<RedisUIPageExpiredException>(() => pager.ReadAsync(1, 42, 1, first.NextPageOffset, "prefix:*", Scan, Metadata));
        await Assert.ThrowsAsync<RedisUIPageExpiredException>(() => pager.ReadAsync(0, 42, 1, first.NextPageOffset, "other:*", Scan, Metadata));
        await Assert.ThrowsAsync<RedisUIPageExpiredException>(() => pager.ReadAsync(0, 0, 1, first.NextPageOffset, "prefix:*", Scan, Metadata));
        now += TimeSpan.FromMinutes(6);
        await Assert.ThrowsAsync<RedisUIPageExpiredException>(() => pager.ReadAsync(0, 42, 1, first.NextPageOffset, "prefix:*", Scan, Metadata));
    }

    [Fact]
    public async Task CapacityEviction_RejectsOldToken_ButNewContinuationRemainsUsable()
    {
        using var pager = new RedisKeyPager(new RedisUISettings(), capacityBytes: 400);
        Task<RedisKeyPager.ScanBatch> Scan(long cursor, int count) => Task.FromResult(new RedisKeyPager.ScanBatch(0, ["a", "b"]));
        var first = await pager.ReadAsync(0, 0, 1, 0, null, Scan, Metadata);
        var second = await pager.ReadAsync(0, 0, 1, 0, null, Scan, Metadata);
        await Assert.ThrowsAsync<RedisUIPageExpiredException>(() => pager.ReadAsync(0, 0, 1, first.NextPageOffset, null, Scan, Metadata));
        var last = await pager.ReadAsync(0, 0, 1, second.NextPageOffset, null, Scan, Metadata);
        Assert.Equal("b", Assert.Single(last.Keys).Name);
    }
}
