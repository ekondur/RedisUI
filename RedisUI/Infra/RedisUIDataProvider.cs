using System.Text;
using System.Text.Json;
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
        private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

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

            var keyNames = TryGetResultArray(innerResult[1])
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
                RedisType.String => CreateStringKeyModel(keyName, keyType, await redisDb.StringGetAsync(keyName).ConfigureAwait(false), "light"),
                RedisType.Hash => CreateStructuredKeyModel(keyName, keyType, await redisDb.HashGetAllAsync(keyName).ConfigureAwait(false), "success"),
                RedisType.List => CreateStructuredKeyModel(keyName, keyType, await redisDb.ListRangeAsync(keyName).ConfigureAwait(false), "warning"),
                RedisType.Set => CreateStructuredKeyModel(keyName, keyType, await redisDb.SetMembersAsync(keyName).ConfigureAwait(false), "primary"),
                RedisType.None => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = string.Empty,
                    Badge = "secondary",
                    ViewerFormat = "text",
                    ValueSizeBytes = 0
                },
                _ => new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = "(preview unavailable for this Redis type)",
                    Badge = "secondary",
                    ViewerFormat = "text",
                    ValueSizeBytes = 0
                }
            };
        }

        private static KeyModel CreateStringKeyModel(string keyName, RedisType keyType, RedisValue value, string badge)
        {
            var formattedValue = FormatRedisValue(value);

            if (formattedValue.IsBinary)
            {
                return new KeyModel
                {
                    Name = keyName,
                    KeyType = keyType.ToString(),
                    Value = string.Empty,
                    Badge = badge,
                    ViewerFormat = "binary",
                    Base64Value = formattedValue.Base64Value,
                    ValueSizeBytes = formattedValue.SizeBytes
                };
            }

            return new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Value = formattedValue.Text,
                Badge = badge,
                ViewerFormat = LooksLikeJson(formattedValue.Text) ? "json" : "text",
                ValueSizeBytes = formattedValue.SizeBytes
            };
        }

        private static KeyModel CreateStructuredKeyModel(string keyName, RedisType keyType, HashEntry[] values, string badge)
        {
            var entries = values.Select(entry => new
            {
                field = FormatValueElement(entry.Name),
                value = FormatValueElement(entry.Value)
            });

            return new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Value = JsonSerializer.Serialize(entries),
                Badge = badge,
                ViewerFormat = "json",
                ValueSizeBytes = values.Sum(entry => GetSizeBytes(entry.Name) + GetSizeBytes(entry.Value))
            };
        }

        private static KeyModel CreateStructuredKeyModel(string keyName, RedisType keyType, RedisValue[] values, string badge)
        {
            var entries = values.Select(FormatValueElement);

            return new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Value = JsonSerializer.Serialize(entries),
                Badge = badge,
                ViewerFormat = "json",
                ValueSizeBytes = values.Sum(GetSizeBytes)
            };
        }

        private static object FormatValueElement(RedisValue value)
        {
            var formatted = FormatRedisValue(value);
            return formatted.IsBinary
                ? new
                {
                    encoding = "base64",
                    bytes = formatted.SizeBytes,
                    value = formatted.Base64Value
                }
                : formatted.Text;
        }

        private static FormattedRedisValue FormatRedisValue(RedisValue value)
        {
            var bytes = GetBytes(value);
            if (bytes.Length == 0)
            {
                return new FormattedRedisValue(string.Empty, string.Empty, false, 0);
            }

            if (TryDecodeText(bytes, out var text))
            {
                return new FormattedRedisValue(text, string.Empty, false, bytes.Length);
            }

            return new FormattedRedisValue(string.Empty, Convert.ToBase64String(bytes), true, bytes.Length);
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

        private static byte[] GetBytes(RedisValue value)
        {
            try
            {
                return ((byte[]?)value) ?? Array.Empty<byte>();
            }
            catch (InvalidCastException)
            {
                return value.HasValue ? Encoding.UTF8.GetBytes(value.ToString()) : Array.Empty<byte>();
            }
        }

        private static long GetSizeBytes(RedisValue value) => GetBytes(value).LongLength;

        private static bool TryDecodeText(byte[] bytes, out string text)
        {
            try
            {
                text = StrictUtf8.GetString(bytes);
                if (text.Any(ch => char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t'))
                {
                    text = string.Empty;
                    return false;
                }

                return true;
            }
            catch (DecoderFallbackException)
            {
                text = string.Empty;
                return false;
            }
        }

        private static bool LooksLikeJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.TrimStart();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) &&
                !trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(value);
                return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
            }
            catch (JsonException)
            {
                return false;
            }
        }

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

        private sealed record FormattedRedisValue(string Text, string Base64Value, bool IsBinary, long SizeBytes);
    }
}
