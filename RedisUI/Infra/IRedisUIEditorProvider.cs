namespace RedisUI.Infra;

/// <summary>Optional advanced editing capability for custom providers.</summary>
public interface IRedisUIEditorProvider
{
    Task<bool> WriteStringAsync(int database, string key, string value, string expiryMode,
        long? ttlSeconds, string? expectedValue, CancellationToken cancellationToken = default);
    Task<bool> EditElementAsync(int database, string key, string type, string action,
        string selector, string value, string? expectedValue, double score,
        CancellationToken cancellationToken = default);
}
