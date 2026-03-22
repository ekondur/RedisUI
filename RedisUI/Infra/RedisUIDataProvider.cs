using RedisUI.Helpers;
using RedisUI.Models;
using StackExchange.Redis;

namespace RedisUI.Infra
{
    public sealed class RedisUIDataProvider : IRedisUIDataProvider
    {
        private readonly RedisUISettings _settings;
        private readonly Lazy<IConnectionMultiplexer>? _ownedConnection;
        private readonly IConnectionMultiplexer? _externalConnection;

        public RedisUIDataProvider(RedisUISettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (_settings.ConnectionMultiplexer != null)
            {
                _externalConnection = _settings.ConnectionMultiplexer;
            }
            else
            {
                _ownedConnection = new Lazy<IConnectionMultiplexer>(CreateConnection, LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }

        public async Task<string> GetDatabaseSizeAsync(int database, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            var dbSize = await redisDb.ExecuteAsync("DBSIZE").ConfigureAwait(false);
            return dbSize.ToString();
        }

        public async Task<KeyPageModel> GetKeysAsync(int database, long cursor, int pageSize, string? searchPattern, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            RedisResult result;

            if (string.IsNullOrWhiteSpace(searchPattern))
            {
                result = await redisDb.ExecuteAsync("SCAN", cursor.ToString(), "COUNT", pageSize.ToString()).ConfigureAwait(false);
            }
            else
            {
                result = await redisDb.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", searchPattern, "COUNT", pageSize.ToString()).ConfigureAwait(false);
            }

            var innerResult = TryGetResultArray(result);
            if (innerResult.Length < 2)
            {
                return new KeyPageModel();
            }

            var keyResults = TryGetResultArray(innerResult[1]);
            var keyNames = keyResults
                .Select(x => x.ToString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            var keys = new List<KeyModel>(keyNames.Count);

            foreach (var keyName in keyNames)
            {
                keys.Add(await BuildKeyAsync(redisDb, keyName).ConfigureAwait(false));
            }

            return new KeyPageModel
            {
                Keys = keys,
                NextCursor = keys.Count > 0 && long.TryParse(innerResult[0].ToString(), out var nextCursor)
                    ? nextCursor
                    : 0
            };
        }

        public async Task<StatisticsVm> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(0);
            var keyspaces = await GetKeyspacesAsync(cancellationToken).ConfigureAwait(false);
            var serverInfo = await redisDb.ExecuteAsync("INFO", "SERVER").ConfigureAwait(false);
            var memoryInfo = await redisDb.ExecuteAsync("INFO", "MEMORY").ConfigureAwait(false);
            var statsInfo = await redisDb.ExecuteAsync("INFO", "STATS").ConfigureAwait(false);
            var allInfo = await redisDb.ExecuteAsync("INFO").ConfigureAwait(false);

            return new StatisticsVm
            {
                Keyspaces = keyspaces.ToList(),
                Server = ServerModel.Instance(serverInfo.ToString()),
                Memory = MemoryModel.Instance(memoryInfo.ToString()),
                Stats = StatsModel.Instance(statsInfo.ToString()),
                AllInfo = allInfo.ToString().ToInfo()
            };
        }

        public async Task<IReadOnlyList<KeyspaceModel>> GetKeyspacesAsync(CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(0);
            var keyspace = await redisDb.ExecuteAsync("INFO", "KEYSPACE").ConfigureAwait(false);

            return keyspace
                .ToString()
                .Replace("# Keyspace", string.Empty)
                .Split(new[] { "\r\n" }, StringSplitOptions.None)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(KeyspaceModel.Instance)
                .Where(model => !string.IsNullOrWhiteSpace(model.Db))
                .ToList();
        }

        public async Task DeleteKeyAsync(int database, string key, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.ExecuteAsync("DEL", key).ConfigureAwait(false);
        }

        public async Task SetStringAsync(int database, string key, string value, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.ExecuteAsync("SET", key, value).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_ownedConnection != null && _ownedConnection.IsValueCreated)
            {
                _ownedConnection.Value.Dispose();
            }
        }

        private async Task<KeyModel> BuildKeyAsync(IDatabase redisDb, string keyName)
        {
            var keyType = await redisDb.KeyTypeAsync(keyName).ConfigureAwait(false);

            return keyType switch
            {
                RedisType.String => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = (await redisDb.StringGetAsync(keyName).ConfigureAwait(false)).ToString(),
                    Badge = "light"
                },
                RedisType.Hash => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = string.Join(", ", (await redisDb.HashGetAllAsync(keyName).ConfigureAwait(false)).Select(x => $"{x.Name}: {x.Value}")),
                    Badge = "success"
                },
                RedisType.List => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = string.Join(", ", (await redisDb.ListRangeAsync(keyName).ConfigureAwait(false)).Select(x => x.ToString())),
                    Badge = "warning"
                },
                RedisType.Set => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = string.Join(", ", (await redisDb.SetMembersAsync(keyName).ConfigureAwait(false)).Select(x => x.ToString())),
                    Badge = "primary"
                },
                RedisType.None => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = string.Empty,
                    Badge = "secondary"
                },
                _ => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = "(preview unavailable for this Redis type)",
                    Badge = "secondary"
                }
            };
        }

        private IConnectionMultiplexer CreateConnection()
        {
            if (_settings.ConfigurationOptions != null)
            {
                return ConnectionMultiplexer.Connect(_settings.ConfigurationOptions);
            }

            return ConnectionMultiplexer.Connect(_settings.ConnectionString);
        }

        private IDatabase GetDatabase(int database) =>
            (_externalConnection ?? _ownedConnection!.Value).GetDatabase(database);

        private static RedisResult[] TryGetResultArray(RedisResult value)
        {
            try
            {
                return ((RedisResult[])value)!;
            }
            catch (InvalidCastException)
            {
                return Array.Empty<RedisResult>();
            }
        }
    }
}
