using System.Security.Cryptography;
using RedisUI.Models;

namespace RedisUI.Infra;

/// <summary>Consumes each SCAN response once and retains overflow for subsequent UI pages.</summary>
internal sealed class RedisKeyPager : IDisposable
{
    internal sealed record ScanBatch(long NextCursor, string[] Keys);
    private sealed record Continuation(int Database, string Pattern, long NextCursor, string[] Keys,
        DateTimeOffset ExpiresAt, long SizeBytes);

    private readonly Dictionary<int, Continuation> _continuations = new();
    private readonly Queue<int> _insertionOrder = new();
    private long _usedBytes;
    private readonly object _cacheLock = new();
    private readonly long _capacityBytes;
    private readonly TimeSpan _lifetime;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly RedisUISettings _settings;

    public RedisKeyPager(RedisUISettings settings, long capacityBytes = 16 * 1024 * 1024,
        TimeSpan? lifetime = null, Func<DateTimeOffset>? utcNow = null)
    {
        _settings = settings;
        _capacityBytes = capacityBytes;
        _lifetime = lifetime ?? TimeSpan.FromMinutes(5);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<KeyPageModel> ReadAsync(int database, long cursor, int pageSize, int pageOffset,
        string? searchPattern, Func<long, int, Task<ScanBatch>> scan, Func<string, Task<KeyModel>> metadata,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var pattern = string.IsNullOrWhiteSpace(searchPattern) ? string.Empty : searchPattern;
        var remaining = Array.Empty<string>();
        var complete = false;
        if (pageOffset != 0)
        {
            // The offset is an opaque continuation ID, never a position in a repeated SCAN.
            Continuation state;
            lock (_cacheLock)
            {
                if (!_continuations.TryGetValue(pageOffset, out var saved) ||
                    saved.ExpiresAt <= _utcNow() || saved.Database != database || saved.Pattern != pattern || saved.NextCursor != cursor)
                    throw new RedisUIPageExpiredException();
                state = saved;
            }
            remaining = state.Keys;
            complete = cursor == 0;
        }

        var keys = new List<KeyModel>(pageSize);
        var iterations = 0;
        while (keys.Count < pageSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (remaining.Length == 0)
            {
                if (complete || iterations >= Math.Max(1, _settings.MaxScanIterationsPerPage)) break;
                var batch = await scan(cursor, pageSize).ConfigureAwait(false);
                iterations++;
                cursor = batch.NextCursor;
                complete = cursor == 0;
                remaining = batch.Keys;
                if (remaining.Length == 0) continue;
            }

            var take = Math.Min(pageSize - keys.Count, remaining.Length);
            foreach (var batch in remaining.Take(take).Chunk(Math.Clamp(_settings.MetadataConcurrency, 1, 64)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var items = await Task.WhenAll(batch.Select(metadata)).ConfigureAwait(false);
                keys.AddRange(items.Where(key => key.KeyType != "None"));
            }
            // Skip consumed names even if they expired while fetching metadata.
            remaining = remaining[take..];
        }

        return new KeyPageModel
        {
            Keys = keys,
            NextCursor = cursor,
            NextPageOffset = remaining.Length == 0 ? 0 : Remember(database, pattern, cursor, remaining)
        };
    }

    private int Remember(int database, string pattern, long cursor, string[] remaining)
    {
        // Account for UTF-16 key names, array references and entry overhead. Only names are cached.
        var bytes = 256L + pattern.Length * 2L + remaining.Sum(key => 32L + key.Length * 2L);
        if (bytes > _capacityBytes) throw new InvalidOperationException("SCAN overflow exceeds the key page buffer capacity.");
        lock (_cacheLock)
        {
            var now = _utcNow();
            while (_insertionOrder.Count > 0 &&
                (_usedBytes + bytes > _capacityBytes || _continuations[_insertionOrder.Peek()].ExpiresAt <= now))
            {
                var oldest = _insertionOrder.Dequeue();
                _usedBytes -= _continuations[oldest].SizeBytes;
                _continuations.Remove(oldest);
            }
            int id;
            do { id = RandomNumberGenerator.GetInt32(1, int.MaxValue); }
            while (_continuations.ContainsKey(id));

            // Evict old continuations before inserting so the newly returned token is usable.
            _continuations.Add(id, new Continuation(database, pattern, cursor, remaining, now + _lifetime, bytes));
            _insertionOrder.Enqueue(id);
            _usedBytes += bytes;
            return id;
        }
    }

    public void Dispose()
    {
        lock (_cacheLock)
        {
            _continuations.Clear();
            _insertionOrder.Clear();
            _usedBytes = 0;
        }
    }
}
