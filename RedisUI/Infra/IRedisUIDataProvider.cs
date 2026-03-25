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

        Task ListPushAsync(int database, string key, string element, CancellationToken cancellationToken = default);

        Task SetAddAsync(int database, string key, string member, CancellationToken cancellationToken = default);

        Task HashSetAsync(int database, string key, string field, string value, CancellationToken cancellationToken = default);

        Task SortedSetAddAsync(int database, string key, string member, double score, CancellationToken cancellationToken = default);

        Task StreamAddAsync(int database, string key, System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>> fields, CancellationToken cancellationToken = default);

        Task SetExpiryAsync(int database, string key, TimeSpan? expiry, CancellationToken cancellationToken = default);
    }
}
