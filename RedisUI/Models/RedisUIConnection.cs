using RedisUI.Infra;
using StackExchange.Redis;

namespace RedisUI;

/// <summary>A server-side connection profile. Credentials are never sent to the browser.</summary>
public sealed class RedisUIConnection
{
    public string Name { get; set; } = string.Empty;
    public string Environment { get; set; } = "Unspecified";
    public string ConnectionString { get; set; } = "localhost";
    public ConfigurationOptions? ConfigurationOptions { get; set; }
    public IConnectionMultiplexer? ConnectionMultiplexer { get; set; }
    public IRedisUIDataProvider? DataProvider { get; set; }
    public bool ReadOnly { get; set; }
}
