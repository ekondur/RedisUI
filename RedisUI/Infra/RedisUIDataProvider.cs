using System.Globalization;
using System.Text;
using System.Text.Json;
using RedisUI.Helpers;
using RedisUI.Models;
using StackExchange.Redis;

namespace RedisUI.Infra
{
    public sealed class RedisUIDataProvider : IRedisUIDataProvider, IRedisUIKeyPageProvider, IRedisUIValuePreviewProvider
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
            return await GetKeysAsync(database, cursor, pageSize, 0, searchPattern, cancellationToken).ConfigureAwait(false);
        }

        public async Task<KeyPageModel> GetKeysAsync(int database, long cursor, int pageSize, int pageOffset, string? searchPattern, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            var keys = new List<KeyModel>(pageSize);
            var scanCursor = cursor;
            var scanOffset = Math.Max(0, pageOffset);
            var scanIterations = 0;
            var maxScanIterations = Math.Max(1, _settings.MaxScanIterationsPerPage);

            while (keys.Count < pageSize && scanIterations < maxScanIterations)
            {
                scanIterations++;
                RedisResult result;

                if (string.IsNullOrWhiteSpace(searchPattern))
                {
                    result = await redisDb.ExecuteAsync("SCAN", scanCursor.ToString(CultureInfo.InvariantCulture), "COUNT", pageSize.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }
                else
                {
                    result = await redisDb.ExecuteAsync("SCAN", scanCursor.ToString(CultureInfo.InvariantCulture), "MATCH", searchPattern, "COUNT", pageSize.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
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
                var effectiveOffset = Math.Min(scanOffset, keyNames.Count);
                var availableKeyNames = keyNames.Skip(effectiveOffset).ToList();
                var remainingPageSize = pageSize - keys.Count;
                var selectedKeyNames = availableKeyNames.Take(remainingPageSize).ToList();

                foreach (var keyName in selectedKeyNames)
                {
                    keys.Add(await BuildKeyMetadataAsync(redisDb, keyName).ConfigureAwait(false));
                }

                if (availableKeyNames.Count > selectedKeyNames.Count)
                {
                    return new KeyPageModel
                    {
                        Keys = keys,
                        NextCursor = scanCursor,
                        NextPageOffset = effectiveOffset + selectedKeyNames.Count
                    };
                }

                if (!long.TryParse(innerResult[0].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nextCursor))
                {
                    nextCursor = 0;
                }

                if (keys.Count >= pageSize)
                {
                    return new KeyPageModel
                    {
                        Keys = keys,
                        NextCursor = nextCursor,
                        NextPageOffset = 0
                    };
                }

                if (nextCursor == 0)
                {
                    return new KeyPageModel
                    {
                        Keys = keys,
                        NextCursor = 0,
                        NextPageOffset = 0
                    };
                }

                scanCursor = nextCursor;
                scanOffset = 0;
            }

            return new KeyPageModel
            {
                Keys = keys,
                NextCursor = scanCursor,
                NextPageOffset = scanOffset
            };
        }

        public async Task<KeyValuePreviewModel?> GetKeyPreviewAsync(
            int database,
            string key,
            long offset,
            int count,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            var keyType = await redisDb.KeyTypeAsync(key).ConfigureAwait(false);
            if (keyType == RedisType.None)
            {
                return null;
            }

            var ttl = await redisDb.KeyTimeToLiveAsync(key).ConfigureAwait(false);
            KeyValuePreviewModel preview = keyType switch
            {
                RedisType.String => await CreateStringPreviewAsync(redisDb, key, keyType, offset, count).ConfigureAwait(false),
                RedisType.Hash => await CreateHashPreviewAsync(redisDb, key, keyType, offset, count, cursor).ConfigureAwait(false),
                RedisType.List => await CreateListPreviewAsync(redisDb, key, keyType, offset, count).ConfigureAwait(false),
                RedisType.Set => await CreateSetPreviewAsync(redisDb, key, keyType, offset, count, cursor).ConfigureAwait(false),
                RedisType.SortedSet => await CreateSortedSetPreviewAsync(redisDb, key, keyType, offset, count).ConfigureAwait(false),
                RedisType.Stream => await CreateStreamPreviewAsync(redisDb, key, keyType, offset, count, cursor).ConfigureAwait(false),
                _ => CreateUnsupportedPreview(key, keyType)
            };

            preview.TTLSeconds = ttl.HasValue ? (long)Math.Ceiling(ttl.Value.TotalSeconds) : (long?)null;
            return preview;
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

        public async Task ListPushAsync(int database, string key, string element, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.ListRightPushAsync(key, element).ConfigureAwait(false);
        }

        public async Task SetAddAsync(int database, string key, string member, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.SetAddAsync(key, member).ConfigureAwait(false);
        }

        public async Task HashSetAsync(int database, string key, string field, string value, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.HashSetAsync(key, field, value).ConfigureAwait(false);
        }

        public async Task SortedSetAddAsync(int database, string key, string member, double score, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.SortedSetAddAsync(key, member, score).ConfigureAwait(false);
        }

        public async Task StreamAddAsync(int database, string key, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            var nameValues = fields.Select(f => new NameValueEntry(f.Key, f.Value)).ToArray();
            await redisDb.StreamAddAsync(key, nameValues).ConfigureAwait(false);
        }

        public async Task SetExpiryAsync(int database, string key, TimeSpan? expiry, CancellationToken cancellationToken = default)
        {
            var redisDb = GetDatabase(database);
            await redisDb.KeyExpireAsync(key, expiry).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_ownedConnection != null && _ownedConnection.IsValueCreated)
            {
                _ownedConnection.Value.Dispose();
            }
        }

        private async Task<KeyModel> BuildKeyMetadataAsync(IDatabase redisDb, string keyName)
        {
            var keyType = await redisDb.KeyTypeAsync(keyName).ConfigureAwait(false);
            var ttl = await redisDb.KeyTimeToLiveAsync(keyName).ConfigureAwait(false);

            return new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Badge = GetBadge(keyType),
                ViewerFormat = keyType == RedisType.String ? "text" : "json",
                ValueSizeBytes = await GetKeySizeBytesAsync(redisDb, keyName, keyType).ConfigureAwait(false),
                TTLSeconds = ttl.HasValue ? (long)Math.Ceiling(ttl.Value.TotalSeconds) : (long?)null
            };
        }

        private async Task<KeyValuePreviewModel> CreateStringPreviewAsync(IDatabase redisDb, string keyName, RedisType keyType, long offset, int requestedCount)
        {
            var totalBytes = await redisDb.StringLengthAsync(keyName).ConfigureAwait(false);
            var count = GetStringPreviewCount(requestedCount);
            var safeOffset = ClampOffset(offset, totalBytes);
            var value = RedisValue.Null;

            if (safeOffset < totalBytes)
            {
                var end = Math.Min(totalBytes - 1, safeOffset + count - 1);
                value = await redisDb.StringGetRangeAsync(keyName, safeOffset, end).ConfigureAwait(false);
            }

            var model = CreateStringKeyModel(keyName, keyType, value, GetBadge(keyType));
            var nextOffset = Math.Min(totalBytes, safeOffset + count);
            var isFullValue = safeOffset == 0 && nextOffset >= totalBytes;
            if (model.ViewerFormat == "json" && !isFullValue)
            {
                model.ViewerFormat = "text";
            }

            return ToPreview(
                model,
                totalBytes,
                safeOffset,
                nextOffset,
                count,
                totalBytes,
                nextOffset < totalBytes,
                "offset",
                "bytes");
        }

        private async Task<KeyValuePreviewModel> CreateListPreviewAsync(IDatabase redisDb, string keyName, RedisType keyType, long offset, int requestedCount)
        {
            var totalItems = await redisDb.ListLengthAsync(keyName).ConfigureAwait(false);
            var count = GetCollectionPreviewCount(requestedCount);
            var safeOffset = ClampOffset(offset, totalItems);
            var values = Array.Empty<RedisValue>();

            if (safeOffset < totalItems)
            {
                var end = Math.Min(totalItems - 1, safeOffset + count - 1);
                values = await redisDb.ListRangeAsync(keyName, safeOffset, end).ConfigureAwait(false);
            }

            var model = CreateStructuredKeyModel(keyName, keyType, values, GetBadge(keyType));
            var nextOffset = Math.Min(totalItems, safeOffset + values.LongLength);

            return ToPreview(
                model,
                await GetKeySizeBytesAsync(redisDb, keyName, keyType).ConfigureAwait(false),
                safeOffset,
                nextOffset,
                count,
                totalItems,
                nextOffset < totalItems,
                "offset",
                "items");
        }

        private async Task<KeyValuePreviewModel> CreateHashPreviewAsync(IDatabase redisDb, string keyName, RedisType keyType, long offset, int requestedCount, string? cursor)
        {
            var totalItems = await redisDb.HashLengthAsync(keyName).ConfigureAwait(false);
            var count = GetCollectionPreviewCount(requestedCount);
            var currentCursor = ParseRedisCursor(cursor);
            var result = await redisDb.ExecuteAsync("HSCAN", keyName, currentCursor.ToString(CultureInfo.InvariantCulture), "COUNT", count.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            var innerResult = TryGetResultArray(result);
            var nextCursor = innerResult.Length > 0 ? innerResult[0].ToString() ?? "0" : "0";
            var entryResults = innerResult.Length > 1 ? TryGetResultArray(innerResult[1]) : Array.Empty<RedisResult>();
            var entries = new List<HashEntry>(entryResults.Length / 2);

            for (var index = 0; index + 1 < entryResults.Length; index += 2)
            {
                entries.Add(new HashEntry(ToRedisValue(entryResults[index]), ToRedisValue(entryResults[index + 1])));
            }

            var model = CreateStructuredKeyModel(keyName, keyType, entries.ToArray(), GetBadge(keyType));
            var safeOffset = Math.Max(0, offset);
            var nextOffset = Math.Min(totalItems, safeOffset + entries.Count);

            return ToPreview(
                model,
                await GetKeySizeBytesAsync(redisDb, keyName, keyType).ConfigureAwait(false),
                safeOffset,
                nextOffset,
                count,
                totalItems,
                nextCursor != "0",
                "cursor",
                "items",
                cursor ?? string.Empty,
                nextCursor == "0" ? string.Empty : nextCursor);
        }

        private async Task<KeyValuePreviewModel> CreateSetPreviewAsync(IDatabase redisDb, string keyName, RedisType keyType, long offset, int requestedCount, string? cursor)
        {
            var totalItems = await redisDb.SetLengthAsync(keyName).ConfigureAwait(false);
            var count = GetCollectionPreviewCount(requestedCount);
            var currentCursor = ParseRedisCursor(cursor);
            var result = await redisDb.ExecuteAsync("SSCAN", keyName, currentCursor.ToString(CultureInfo.InvariantCulture), "COUNT", count.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            var innerResult = TryGetResultArray(result);
            var nextCursor = innerResult.Length > 0 ? innerResult[0].ToString() ?? "0" : "0";
            var valueResults = innerResult.Length > 1 ? TryGetResultArray(innerResult[1]) : Array.Empty<RedisResult>();
            var values = valueResults.Select(ToRedisValue).ToArray();

            var model = CreateStructuredKeyModel(keyName, keyType, values, GetBadge(keyType));
            var safeOffset = Math.Max(0, offset);
            var nextOffset = Math.Min(totalItems, safeOffset + values.LongLength);

            return ToPreview(
                model,
                await GetKeySizeBytesAsync(redisDb, keyName, keyType).ConfigureAwait(false),
                safeOffset,
                nextOffset,
                count,
                totalItems,
                nextCursor != "0",
                "cursor",
                "items",
                cursor ?? string.Empty,
                nextCursor == "0" ? string.Empty : nextCursor);
        }

        private async Task<KeyValuePreviewModel> CreateSortedSetPreviewAsync(IDatabase redisDb, string keyName, RedisType keyType, long offset, int requestedCount)
        {
            var totalItems = await redisDb.SortedSetLengthAsync(keyName).ConfigureAwait(false);
            var count = GetCollectionPreviewCount(requestedCount);
            var safeOffset = ClampOffset(offset, totalItems);
            var entries = Array.Empty<SortedSetEntry>();

            if (safeOffset < totalItems)
            {
                var end = Math.Min(totalItems - 1, safeOffset + count - 1);
                entries = await redisDb.SortedSetRangeByRankWithScoresAsync(keyName, safeOffset, end).ConfigureAwait(false);
            }

            var model = CreateSortedSetKeyModel(keyName, keyType, entries);
            var nextOffset = Math.Min(totalItems, safeOffset + entries.LongLength);

            return ToPreview(
                model,
                await GetKeySizeBytesAsync(redisDb, keyName, keyType).ConfigureAwait(false),
                safeOffset,
                nextOffset,
                count,
                totalItems,
                nextOffset < totalItems,
                "offset",
                "items");
        }

        private async Task<KeyValuePreviewModel> CreateStreamPreviewAsync(IDatabase redisDb, string keyName, RedisType keyType, long offset, int requestedCount, string? cursor)
        {
            var totalItems = await redisDb.StreamLengthAsync(keyName).ConfigureAwait(false);
            var count = GetCollectionPreviewCount(requestedCount);
            RedisValue? minId = string.IsNullOrWhiteSpace(cursor) ? null : "(" + cursor;
            var entries = await redisDb.StreamRangeAsync(keyName, minId, null, count + 1, Order.Ascending).ConfigureAwait(false);
            var pageEntries = entries.Length > count ? entries.Take(count).ToArray() : entries;
            var hasMore = entries.Length > count;
            var nextCursor = hasMore && pageEntries.Length > 0 ? pageEntries[^1].Id.ToString() : string.Empty;
            var model = CreateStreamKeyModel(keyName, keyType, pageEntries);
            var safeOffset = Math.Max(0, offset);
            var nextOffset = Math.Min(totalItems, safeOffset + pageEntries.LongLength);

            return ToPreview(
                model,
                await GetKeySizeBytesAsync(redisDb, keyName, keyType).ConfigureAwait(false),
                safeOffset,
                nextOffset,
                count,
                totalItems,
                hasMore,
                "cursor",
                "items",
                cursor ?? string.Empty,
                nextCursor);
        }

        private static KeyValuePreviewModel CreateUnsupportedPreview(string keyName, RedisType keyType)
        {
            var model = new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Value = "(preview unavailable for this Redis type)",
                Badge = GetBadge(keyType),
                ViewerFormat = "text",
                ValueSizeBytes = 0
            };

            return ToPreview(model, 0, 0, 0, 0, 0, false, "offset", "items");
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

        private static KeyModel CreateSortedSetKeyModel(string keyName, RedisType keyType, SortedSetEntry[] entries)
        {
            var serialized = entries.Select(e => new
            {
                member = FormatValueElement(e.Element),
                score = e.Score
            });

            return new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Value = JsonSerializer.Serialize(serialized),
                Badge = "info",
                ViewerFormat = "json",
                ValueSizeBytes = entries.Sum(e => GetSizeBytes(e.Element))
            };
        }

        private static KeyModel CreateStreamKeyModel(string keyName, RedisType keyType, StreamEntry[] entries)
        {
            var serialized = entries.Select(e => new
            {
                id = e.Id.ToString(),
                fields = e.Values.Select(v => new
                {
                    name = v.Name.ToString(),
                    value = FormatValueElement(v.Value)
                })
            });

            return new KeyModel
            {
                Name = keyName,
                KeyType = keyType.ToString(),
                Value = JsonSerializer.Serialize(serialized),
                Badge = "dark",
                ViewerFormat = "json",
                ValueSizeBytes = entries.Sum(e => e.Values.Sum(v => GetSizeBytes(v.Value)))
            };
        }

        private async Task<long> GetKeySizeBytesAsync(IDatabase redisDb, string keyName, RedisType keyType)
        {
            if (keyType == RedisType.String)
            {
                return await redisDb.StringLengthAsync(keyName).ConfigureAwait(false);
            }

            try
            {
                return ToInt64(await redisDb.ExecuteAsync("MEMORY", "USAGE", keyName).ConfigureAwait(false));
            }
            catch (RedisServerException)
            {
                return 0;
            }
        }

        private int GetCollectionPreviewCount(int requestedCount)
        {
            var defaultCount = Math.Max(1, _settings.ValuePreviewPageSize);
            var maxCount = Math.Max(1, _settings.MaxValuePreviewPageSize);
            var count = requestedCount > 0 ? requestedCount : defaultCount;

            return Math.Min(Math.Max(1, count), maxCount);
        }

        private int GetStringPreviewCount(int requestedCount)
        {
            var defaultCount = Math.Max(1, _settings.StringPreviewBytes);
            var maxCount = Math.Max(1, _settings.MaxStringPreviewBytes);
            var count = requestedCount > 0 ? requestedCount : defaultCount;

            return Math.Min(Math.Max(1, count), maxCount);
        }

        private static long ClampOffset(long offset, long totalItems) =>
            totalItems <= 0 ? 0 : Math.Min(Math.Max(0, offset), totalItems);

        private static long ParseRedisCursor(string? cursor) =>
            long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0
                ? parsed
                : 0;

        private static KeyValuePreviewModel ToPreview(
            KeyModel model,
            long valueSizeBytes,
            long offset,
            long nextOffset,
            int count,
            long totalItems,
            bool hasMore,
            string pageMode,
            string pagingUnit,
            string cursor = "",
            string nextCursor = "")
        {
            return new KeyValuePreviewModel
            {
                Name = model.Name,
                KeyType = model.KeyType,
                Value = model.Value,
                Base64Value = model.Base64Value,
                ViewerFormat = model.ViewerFormat,
                ValueSizeBytes = valueSizeBytes,
                Offset = offset,
                NextOffset = nextOffset,
                Count = count,
                TotalItems = totalItems,
                PagingUnit = pagingUnit,
                PageMode = pageMode,
                Cursor = cursor,
                NextCursor = nextCursor,
                HasMore = hasMore
            };
        }

        private static string GetBadge(RedisType keyType) =>
            keyType switch
            {
                RedisType.String => "light",
                RedisType.Hash => "success",
                RedisType.List => "warning",
                RedisType.Set => "primary",
                RedisType.SortedSet => "info",
                RedisType.Stream => "dark",
                _ => "secondary"
            };

        private static RedisValue ToRedisValue(RedisResult result)
        {
            try
            {
                return (RedisValue)result;
            }
            catch (InvalidCastException)
            {
                return result.ToString() ?? RedisValue.Null;
            }
        }

        private static long ToInt64(RedisResult result)
        {
            try
            {
                return (long)result;
            }
            catch (InvalidCastException)
            {
                return long.TryParse(result.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;
            }
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
