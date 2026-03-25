using System.Text;
using Microsoft.AspNetCore.Http;
using RedisUI;
using RedisUI.Infra;
using RedisUI.Models;
using RedisUI.Pages;
using Xunit;

namespace RedisUI.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_PassesThrough_ForNonRedisPath()
    {
        var provider = new FakeRedisUIDataProvider();
        var nextCalled = false;
        var middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new RedisUISettings { DataProvider = provider });

        var context = CreateContext("/health");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotMatchSimilarPrefix()
    {
        var nextCalled = false;
        var middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new RedisUISettings { DataProvider = new FakeRedisUIDataProvider() });

        var context = CreateContext("/redisfoo");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsForbidden_WhenAuthorizationFails()
    {
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new RedisUISettings
            {
                DataProvider = new FakeRedisUIDataProvider(),
                AuthorizationFilter = new DenyAuthorizationFilter()
            });

        var context = CreateContext("/redis");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_ReturnsBadRequest_ForInvalidPageParameter()
    {
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new RedisUISettings { DataProvider = new FakeRedisUIDataProvider() });

        var context = CreateContext("/redis?page=oops");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("Invalid 'page' query parameter.", await ReadBodyAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_RejectsPostWithoutCsrfToken()
    {
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new RedisUISettings { DataProvider = new FakeRedisUIDataProvider() });

        var context = CreateContext("/redis", method: "POST");
        SetJsonBody(context, """{"DelKey":"users:1"}""");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("Invalid CSRF token.", await ReadBodyAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_AllowsPostWithCsrfToken_AndMutates()
    {
        var provider = new FakeRedisUIDataProvider();
        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new RedisUISettings { DataProvider = provider });

        var context = CreateContext("/redis", method: "POST");
        SetJsonBody(context, """{"DelKey":"users:1","InsertKey":"users:2","InsertValue":"ok"}""");
        context.Request.Headers["Cookie"] = "RedisUI.AntiForgery=test-token";
        context.Request.Headers["X-RedisUI-CSRF"] = "test-token";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal("users:1", provider.LastDeletedKey);
        Assert.Equal("users:2", provider.LastInsertedKey);
        Assert.Equal("ok", provider.LastInsertedValue);
    }

    [Fact]
    public async Task InvokeAsync_EncodesKeyContent_InMainPage()
    {
        var provider = new FakeRedisUIDataProvider
        {
            KeyPage = new KeyPageModel
            {
                NextCursor = 0,
                Keys =
                [
                    new KeyModel
                    {
                        Name = "<script>alert('x')</script>",
                        KeyType = "String",
                        Value = "\"</script><img src=x onerror=alert(1)>",
                        Badge = "light"
                    }
                ]
            }
        };

        var middleware = CreateMiddleware(
            _ => Task.CompletedTask,
            new RedisUISettings { DataProvider = provider });

        var context = CreateContext("/redis");

        await middleware.InvokeAsync(context);

        var html = await ReadBodyAsync(context);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("&lt;script&gt;alert(&#x27;x&#x27;)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert('x')</script>", html);
        Assert.DoesNotContain("\"</script><img src=x onerror=alert(1)>", html);
    }

    [Fact]
    public void Statistics_Build_EncodesDynamicValues()
    {
        var model = new StatisticsVm
        {
            Server = ServerModel.Instance("redis_version:<b>7</b>\r\nredis_mode:standalone\r\ntcp_port:6379"),
            Memory = MemoryModel.Instance("used_memory:1024\r\nused_memory_peak:2048\r\nused_memory_lua:0"),
            Stats = StatsModel.Instance("total_connections_received:1\r\ntotal_commands_processed:2\r\nexpired_keys:3"),
            Keyspaces = [KeyspaceModel.Instance("db0:keys=1,expires=0,avg_ttl=0")],
            AllInfo = new Dictionary<string, string> { ["unsafe"] = "<img src=x onerror=1>" }
        };

        var html = Statistics.Build(model);

        Assert.Contains("&lt;b&gt;7&lt;/b&gt;", html);
        Assert.DoesNotContain("<img src=x onerror=1>", html);
    }

    private static RedisUIMiddleware CreateMiddleware(RequestDelegate next, RedisUISettings settings) =>
        new(next, settings);

    private static DefaultHttpContext CreateContext(string pathAndQuery, string method = "GET")
    {
        var context = new DefaultHttpContext();
        var path = pathAndQuery;
        var query = string.Empty;
        var separatorIndex = pathAndQuery.IndexOf('?');

        if (separatorIndex >= 0)
        {
            path = pathAndQuery[..separatorIndex];
            query = pathAndQuery[separatorIndex..];
        }

        context.Request.Method = method;
        context.Request.Scheme = "https";
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static void SetJsonBody(DefaultHttpContext context, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(bytes);
        context.Request.ContentLength = bytes.Length;
    }

    private static async Task<string> ReadBodyAsync(DefaultHttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed class DenyAuthorizationFilter : IRedisAuthorizationFilter
    {
        public bool Authorize(HttpContext context) => false;
    }

    private sealed class FakeRedisUIDataProvider : IRedisUIDataProvider
    {
        public KeyPageModel KeyPage { get; set; } = new()
        {
            NextCursor = 0,
            Keys =
            [
                new KeyModel
                {
                    Name = "users:1",
                    KeyType = "String",
                    Value = "value",
                    Badge = "light"
                }
            ]
        };

        public string DatabaseSize { get; set; } = "1";

        public IReadOnlyList<KeyspaceModel> Keyspaces { get; set; } =
        [
            KeyspaceModel.Instance("db0:keys=1,expires=0,avg_ttl=0")
        ];

        public string? LastDeletedKey { get; private set; }

        public string? LastInsertedKey { get; private set; }

        public string? LastInsertedValue { get; private set; }

        public Task DeleteKeyAsync(int database, string key, CancellationToken cancellationToken = default)
        {
            LastDeletedKey = key;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }

        public Task<string> GetDatabaseSizeAsync(int database, CancellationToken cancellationToken = default) =>
            Task.FromResult(DatabaseSize);

        public Task<KeyPageModel> GetKeysAsync(int database, long cursor, int pageSize, string? searchPattern, CancellationToken cancellationToken = default) =>
            Task.FromResult(KeyPage);

        public Task<IReadOnlyList<KeyspaceModel>> GetKeyspacesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Keyspaces);

        public Task<StatisticsVm> GetStatisticsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new StatisticsVm
            {
                Server = ServerModel.Instance("redis_version:7\r\nredis_mode:standalone\r\ntcp_port:6379"),
                Memory = MemoryModel.Instance("used_memory:1024\r\nused_memory_peak:2048\r\nused_memory_lua:0"),
                Stats = StatsModel.Instance("total_connections_received:1\r\ntotal_commands_processed:2\r\nexpired_keys:3"),
                AllInfo = new Dictionary<string, string> { ["role"] = "master" },
                Keyspaces = Keyspaces.ToList()
            });

        public Task SetStringAsync(int database, string key, string value, CancellationToken cancellationToken = default)
        {
            LastInsertedKey = key;
            LastInsertedValue = value;
            return Task.CompletedTask;
        }

        public Task ListPushAsync(int database, string key, string element, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetAddAsync(int database, string key, string member, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task HashSetAsync(int database, string key, string field, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SortedSetAddAsync(int database, string key, string member, double score, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StreamAddAsync(int database, string key, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SetExpiryAsync(int database, string key, TimeSpan? expiry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
