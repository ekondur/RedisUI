using RedisUI.Models;

namespace RedisUI.Infra
{
    public interface IRedisUIValuePreviewProvider
    {
        Task<KeyValuePreviewModel?> GetKeyPreviewAsync(
            int database,
            string key,
            long offset,
            int count,
            string? cursor,
            CancellationToken cancellationToken = default);
    }
}
