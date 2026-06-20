using RedisUI.Models;

namespace RedisUI.Infra
{
    public interface IRedisUIKeyPageProvider
    {
        Task<KeyPageModel> GetKeysAsync(
            int database,
            long cursor,
            int pageSize,
            int pageOffset,
            string? searchPattern,
            CancellationToken cancellationToken = default);
    }
}
