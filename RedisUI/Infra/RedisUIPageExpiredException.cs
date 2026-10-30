namespace RedisUI.Infra;

/// <summary>The buffered key page is no longer available; restart scanning from cursor zero.</summary>
public sealed class RedisUIPageExpiredException : Exception
{
    public RedisUIPageExpiredException() : base("This key page has expired. Restart the search from the first page.") { }
}
