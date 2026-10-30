using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RedisUI.Models;
using RedisUI.Pages;
using Xunit;

namespace RedisUI.Tests;

public partial class MiddlewareTests
{
    [Fact]
    public async Task ExpiredContinuation_Returns410ForApi_AndRestartsHtmlScanWithSameFilters()
    {
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings
        { DataProvider = new FakeRedisUIDataProvider { PageException = new RedisUI.Infra.RedisUIPageExpiredException() } });
        var api = CreateContext("/redis/keys?db=2&key=users%3A*&page=42&offset=123");
        await middleware.InvokeAsync(api);
        Assert.Equal(410, api.Response.StatusCode);
        var html = CreateContext("/redis?db=2&key=users%3A*&page=42&offset=123");
        await middleware.InvokeAsync(html);
        Assert.Equal(302, html.Response.StatusCode);
        var location = html.Response.Headers.Location.ToString();
        Assert.Contains("db=2", location);
        Assert.Contains("key=users", location);
        Assert.DoesNotContain("page=", location);
        Assert.DoesNotContain("offset=", location);
    }

    private static DefaultHttpContext Mutation(string body, string query = "")
    {
        var context = CreateContext("/redis" + query, "POST");
        SetJsonBody(context, body);
        context.Request.Headers.Cookie = "RedisUI.AntiForgery=test-token";
        context.Request.Headers["X-RedisUI-CSRF"] = "test-token";
        return context;
    }

    [Theory]
    [InlineData("{\"DelKey\":\"secret\"}")]
    [InlineData("{\"InsertKey\":\"secret\",\"InsertValue\":\"new\"}")]
    [InlineData("{\"SetExpiryKey\":\"secret\",\"ExpireSeconds\":60}")]
    [InlineData("{\"ElementKey\":\"secret\",\"ElementType\":\"hash\",\"ElementAction\":\"delete\",\"ElementSelector\":\"field\"}")]
    public async Task ReadOnly_RejectsEveryMutation(string body)
    {
        var provider = new FakeRedisUIDataProvider();
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = provider, ReadOnly = true });
        var context = Mutation(body);
        await middleware.InvokeAsync(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Null(provider.LastDeletedKey);
        Assert.Null(provider.LastInsertedKey);
        Assert.False(provider.ElementCalled);
        var read = CreateContext("/redis/value?key=secret");
        await middleware.InvokeAsync(read);
        Assert.Equal(200, read.Response.StatusCode);
    }

    [Fact]
    public async Task CombinedRequest_AuthorizesAllOperationsBeforeChangingAnything()
    {
        var provider = new FakeRedisUIDataProvider();
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings
        { DataProvider = provider, AuthorizeOperation = (_, access) => access.Operation != RedisUIOperation.Edit });
        var context = Mutation("""{"DelKey":"old","InsertKey":"new","InsertValue":"v"}""");
        await middleware.InvokeAsync(context);
        Assert.Equal(403, context.Response.StatusCode);
        Assert.Null(provider.LastDeletedKey);
    }

    [Fact]
    public async Task ConnectionSelection_RoutesToCorrectProvider_AndHonorsReadOnly()
    {
        var first = new FakeRedisUIDataProvider();
        var second = new FakeRedisUIDataProvider();
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings
        {
            DataProvider = first,
            Connections = [new RedisUIConnection { Name = "production", DataProvider = second, ReadOnly = true }]
        });
        var read = CreateContext("/redis/value?connection=production&db=2&key=test");
        await middleware.InvokeAsync(read);
        Assert.Equal(2, second.LastPreviewDatabase);
        Assert.Null(first.LastPreviewDatabase);
        var write = Mutation("""{"DelKey":"test"}""", "?connection=production");
        await middleware.InvokeAsync(write);
        Assert.Equal(403, write.Response.StatusCode);
        var unknown = CreateContext("/redis?connection=missing");
        await middleware.InvokeAsync(unknown);
        Assert.Equal(400, unknown.Response.StatusCode);
    }

    [Theory]
    [InlineData("/keys")]
    [InlineData("/value?key=x")]
    [InlineData("/metrics")]
    [InlineData("/audit")]
    public async Task ViewPermission_ProtectsEveryReadEndpoint(string path)
    {
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings
        { DataProvider = new FakeRedisUIDataProvider(), AuthorizeOperation = (_, access) => access.Operation != RedisUIOperation.View });
        var context = CreateContext("/redis" + path);
        await middleware.InvokeAsync(context);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("keep")]
    [InlineData("set")]
    [InlineData("persist")]
    public async Task StringEdit_PassesExpiryPolicyAndExpectedValue(string mode)
    {
        var provider = new FakeRedisUIDataProvider();
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = provider });
        var context = Mutation(JsonSerializer.Serialize(new { InsertKey = "test", InsertValue = "", ExpiryMode = mode,
            InsertTTLSeconds = mode == "set" ? (long?)60 : null, ExpectedValue = "old" }));
        await middleware.InvokeAsync(context);
        Assert.Equal(204, context.Response.StatusCode);
        Assert.Equal(mode, provider.LastExpiryMode);
        Assert.Equal("old", provider.LastExpectedValue);
        Assert.Equal("", provider.LastInsertedValue);
    }

    [Theory]
    [InlineData("{\"InsertKey\":\"x\",\"InsertValue\":\"bad\",\"ValidateJson\":true}")]
    [InlineData("{\"InsertKey\":\"x\",\"InsertValue\":\"v\",\"ExpiryMode\":\"set\",\"InsertTTLSeconds\":-1}")]
    [InlineData("{\"InsertKey\":\"x\",\"InsertValue\":\"v\",\"InsertType\":\"invalid\"}")]
    [InlineData("{\"InsertKey\":\"x\",\"InsertValue\":\"v\",\"InsertType\":\"sortedset\",\"InsertScore\":\"NaN\"}")]
    public async Task Validation_HappensBeforeAnyMutation(string body)
    {
        var provider = new FakeRedisUIDataProvider();
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = provider });
        var context = Mutation(body.Insert(1, "\"DelKey\":\"old\","));
        await middleware.InvokeAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Null(provider.LastDeletedKey);
        Assert.Null(provider.LastInsertedKey);
    }

    [Fact]
    public async Task Conflict_Returns409_AndAuditExcludesValues()
    {
        var provider = new FakeRedisUIDataProvider { WriteConflict = true };
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = provider });
        var context = Mutation("""{"InsertKey":"key","InsertValue":"super-secret","ExpectedValue":"old-secret"}""");
        await middleware.InvokeAsync(context);
        Assert.Equal(409, context.Response.StatusCode);
        var audit = CreateContext("/redis/audit");
        await middleware.InvokeAsync(audit);
        var body = await ReadBodyAsync(audit);
        Assert.Contains("\"succeeded\":false", body);
        Assert.DoesNotContain("secret", body);
    }

    [Fact]
    public async Task Audit_IsBoundedScopedAndSeparatelyAuthorized()
    {
        var entries = new List<RedisUIAuditEntry>();
        var settings = new RedisUISettings { DataProvider = new FakeRedisUIDataProvider(), AuditCapacity = 1, AuditSink = entries.Add };
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, settings);
        await middleware.InvokeAsync(Mutation("""{"DelKey":"old"}"""));
        await middleware.InvokeAsync(Mutation("""{"DelKey":"latest"}"""));
        var audit = CreateContext("/redis/audit");
        await middleware.InvokeAsync(audit);
        Assert.DoesNotContain("old", await ReadBodyAsync(audit));
        Assert.Equal(2, entries.Count);
        var other = CreateContext("/redis/audit?db=1");
        await middleware.InvokeAsync(other);
        Assert.Equal("[]", await ReadBodyAsync(other));
        settings.AuthorizeOperation = (_, access) => access.Operation != RedisUIOperation.Audit;
        var denied = CreateContext("/redis/audit");
        await middleware.InvokeAsync(denied);
        Assert.Equal(403, denied.Response.StatusCode);
    }

    [Fact]
    public async Task KeysApi_PreservesLargeCursorWithoutJavascriptPrecisionLoss()
    {
        var provider = new FakeRedisUIDataProvider { KeyPage = new KeyPageModel { NextCursor = 9007199254740993, NextPageOffset = 2 } };
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = provider });
        var context = CreateContext("/redis/keys");
        await middleware.InvokeAsync(context);
        Assert.Contains("\"nextCursor\":\"9007199254740993\"", await ReadBodyAsync(context));
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task Metrics_ReturnsNullForUnavailableCounters()
    {
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = new FakeRedisUIDataProvider() });
        var context = CreateContext("/redis/metrics");
        await middleware.InvokeAsync(context);
        Assert.Contains("\"hits\":null", await ReadBodyAsync(context));
    }

    [Fact]
    public async Task BrowserFixture_UsesActualEmbeddedAssets()
    {
        var output = Environment.GetEnvironmentVariable("REDISUI_BROWSER_FIXTURE");
        if (string.IsNullOrEmpty(output)) return;
        using var middleware = CreateMiddleware(_ => Task.CompletedTask, new RedisUISettings { DataProvider = new FakeRedisUIDataProvider() });
        var context = CreateContext("/redis");
        await middleware.InvokeAsync(context);
        await File.WriteAllTextAsync(output, await ReadBodyAsync(context));
        var stats = CreateContext("/redis/statistics");
        await middleware.InvokeAsync(stats);
        await File.WriteAllTextAsync(output + ".statistics.html", await ReadBodyAsync(stats));
    }
}
