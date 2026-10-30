using RedisUI.Infra;
using StackExchange.Redis;
using Xunit;

namespace RedisUI.Tests;

public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("REDISUI_TEST_REDIS")))
            Skip = "Set REDISUI_TEST_REDIS to run against a real Redis 6.0.6+ server.";
    }
}

public class RedisIntegrationTests
{
    private sealed class Scope : IAsyncDisposable
    {
        public IConnectionMultiplexer Connection { get; }
        public RedisUIDataProvider Provider { get; }
        public IDatabase Db => Connection.GetDatabase();
        private readonly List<RedisKey> _keys = new();
        private readonly string _prefix = "redisui-test:" + Guid.NewGuid().ToString("N") + ":";
        public string Pattern => _prefix + "*";
        public Scope()
        {
            Connection = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("REDISUI_TEST_REDIS")!);
            Provider = new RedisUIDataProvider(new RedisUISettings { ConnectionMultiplexer = Connection, MaxScanIterationsPerPage = 2 });
        }
        public string Key(string suffix)
        {
            var key = _prefix + suffix;
            _keys.Add(key);
            return key;
        }
        public async ValueTask DisposeAsync()
        {
            // Never flush a database: only remove this test's individually registered keys.
            try { if (_keys.Count > 0) await Db.KeyDeleteAsync(_keys.ToArray()); }
            finally { Provider.Dispose(); Connection.Dispose(); }
        }
    }

    [RedisFact]
    public async Task StringWrites_KeepSetAndRemoveTtl_AtomicallyRejectStaleValue()
    {
        await using var scope = new Scope();
        var key = scope.Key("string");
        await scope.Db.StringSetAsync(key, "before", TimeSpan.FromMinutes(2));
        await scope.Provider.SetStringAsync(0, key, "after");
        Assert.InRange((await scope.Db.KeyTimeToLiveAsync(key))!.Value.TotalSeconds, 110, 120);
        Assert.False(await scope.Provider.WriteStringAsync(0, key, "wrong", "persist", null, "stale"));
        Assert.Equal("after", await scope.Db.StringGetAsync(key));
        Assert.NotNull(await scope.Db.KeyTimeToLiveAsync(key));
        Assert.True(await scope.Provider.WriteStringAsync(0, key, "", "set", 60, "after"));
        Assert.Equal("", await scope.Db.StringGetAsync(key));
        Assert.InRange((await scope.Db.KeyTimeToLiveAsync(key))!.Value.TotalSeconds, 50, 60);
        Assert.True(await scope.Provider.WriteStringAsync(0, key, "persistent", "persist", null, ""));
        Assert.Null(await scope.Db.KeyTimeToLiveAsync(key));
    }

    [RedisFact]
    public async Task ScanPages_ReturnAllMatchingKeysWithinPageSize()
    {
        await using var scope = new Scope();
        var expected = Enumerable.Range(0, 37).Select(x => scope.Key(x.ToString())).ToArray();
        await Task.WhenAll(expected.Select(key => scope.Db.StringSetAsync(key, "test")));
        var seen = new HashSet<string>(); long cursor = 0; var offset = 0; var iterations = 0;
        do
        {
            var page = await scope.Provider.GetKeysAsync(0, cursor, 3, offset, scope.Pattern);
            Assert.InRange(page.Keys.Count, 0, 3);
            foreach (var key in page.Keys) { Assert.Contains(key.Name, expected); seen.Add(key.Name); }
            cursor = page.NextCursor; offset = page.NextPageOffset;
            Assert.True(++iterations < 10000, "Scan did not terminate within the test budget.");
        } while (cursor != 0 || offset != 0);
        Assert.Equal(expected.OrderBy(x => x), seen.OrderBy(x => x));
    }

    [RedisFact]
    public async Task Collections_EditAndDeleteIndividualElementsWithoutReplacingKeys()
    {
        await using var scope = new Scope();
        var hash = scope.Key("hash"); var list = scope.Key("list"); var set = scope.Key("set"); var sorted = scope.Key("sorted"); var stream = scope.Key("stream");
        await scope.Db.HashSetAsync(hash, "field", "old");
        await scope.Db.KeyExpireAsync(hash, TimeSpan.FromMinutes(2));
        Assert.True(await scope.Provider.EditElementAsync(0, hash, "hash", "update", "field", "new", null, 0));
        Assert.Equal("new", await scope.Db.HashGetAsync(hash, "field"));
        Assert.NotNull(await scope.Db.KeyTimeToLiveAsync(hash));
        Assert.True(await scope.Provider.EditElementAsync(0, hash, "hash", "delete", "field", "", null, 0));
        await scope.Db.ListRightPushAsync(list, new RedisValue[] { "a", "b", "b", "c" });
        Assert.False(await scope.Provider.EditElementAsync(0, list, "list", "update", "1", "new", "stale", 0));
        Assert.True(await scope.Provider.EditElementAsync(0, list, "list", "update", "1", "new", "b", 0));
        Assert.True(await scope.Provider.EditElementAsync(0, list, "list", "delete", "2", "", "b", 0));
        Assert.Equal(new RedisValue[] { "a", "new", "c" }, await scope.Db.ListRangeAsync(list));
        await scope.Db.SetAddAsync(set, "old");
        await scope.Db.KeyExpireAsync(set, TimeSpan.FromMinutes(2));
        Assert.True(await scope.Provider.EditElementAsync(0, set, "set", "update", "old", "new", null, 0));
        Assert.NotNull(await scope.Db.KeyTimeToLiveAsync(set));
        Assert.True(await scope.Provider.EditElementAsync(0, set, "set", "update", "new", "new", null, 0));
        Assert.True(await scope.Db.SetContainsAsync(set, "new"));
        Assert.False(await scope.Db.SetContainsAsync(set, "old"));
        Assert.True(await scope.Provider.EditElementAsync(0, set, "set", "delete", "new", "", null, 0));
        await scope.Db.SortedSetAddAsync(sorted, "old", 1);
        await scope.Db.KeyExpireAsync(sorted, TimeSpan.FromMinutes(2));
        Assert.True(await scope.Provider.EditElementAsync(0, sorted, "sortedset", "update", "old", "new", null, 2.5));
        Assert.NotNull(await scope.Db.KeyTimeToLiveAsync(sorted));
        Assert.Equal(2.5, await scope.Db.SortedSetScoreAsync(sorted, "new"));
        Assert.True(await scope.Provider.EditElementAsync(0, sorted, "sortedset", "delete", "new", "", null, 0));
        var id = await scope.Db.StreamAddAsync(stream, "field", "value");
        Assert.True(await scope.Provider.EditElementAsync(0, stream, "stream", "delete", id.ToString(), "", null, 0));
        Assert.Equal(0, await scope.Db.StreamLengthAsync(stream));
    }

    [RedisFact]
    public async Task StringPreview_IsBoundedAndCanBePaged()
    {
        await using var scope = new Scope();
        var key = scope.Key("preview");
        await scope.Db.StringSetAsync(key, new string('x', 20000));
        var first = await scope.Provider.GetKeyPreviewAsync(0, key, 0, 64, null);
        Assert.NotNull(first);
        Assert.True(first.HasMore);
        Assert.Equal(64, first.Value.Length);
        var last = await scope.Provider.GetKeyPreviewAsync(0, key, 19968, 64, null);
        Assert.NotNull(last);
        Assert.False(last.HasMore);
        Assert.Equal(32, last.Value.Length);
    }
}
