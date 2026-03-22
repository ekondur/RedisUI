using RedisUI.Models;

namespace RedisUI.Infra
{
    public interface IRedisUIDataProvider : IDisposable
    {
        Task<string> GetDatabaseSizeAsync(int database, CancellationToken cancellationToken = default);

        Task<KeyPageModel> GetKeysAsync(int database, long cursor, int pageSize, string? searchPattern, CancellationToken cancellationToken = default);

        Task<StatisticsVm> GetStatisticsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<KeyspaceModel>> GetKeyspacesAsync(CancellationToken cancellationToken = default);

        Task DeleteKeyAsync(int database, string key, CancellationToken cancellationToken = default);

        Task SetStringAsync(int database, string key, string value, CancellationToken cancellationToken = default);
    }
}
