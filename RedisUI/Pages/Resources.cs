using System.Collections.Concurrent;

namespace RedisUI.Pages;

internal static class Resources
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();
    public static string Read(string name) => Cache.GetOrAdd(name, path =>
    {
        using var stream = typeof(Resources).Assembly.GetManifestResourceStream("RedisUI.Assets." + path)
            ?? throw new InvalidOperationException("Missing embedded UI resource: " + path);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}
