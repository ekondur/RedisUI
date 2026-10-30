using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RedisUI.Infra;
using RedisUI.Models;
using RedisUI.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace RedisUI
{
    public class RedisUIMiddleware : IDisposable
    {
        private const int DefaultPageSize = 10;
        private const string RequestFailedMessage = "RedisUI request failed.";
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly RequestDelegate _next;
        private readonly RedisUISettings _settings;
        private readonly PathString _basePath;
        private readonly Dictionary<string, (IRedisUIDataProvider Provider, bool Owned, bool ReadOnly, string Environment)> _connections = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<RedisUIAuditEntry> _audit = new();
        private readonly ILogger<RedisUIMiddleware> _logger;

        public RedisUIMiddleware(RequestDelegate next, RedisUISettings settings, ILogger<RedisUIMiddleware>? logger = null)
        {
            _next = next;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? NullLogger<RedisUIMiddleware>.Instance;
            _basePath = new PathString(NormalizePath(settings.Path));
            settings.Path = _basePath.Value ?? "/redis";
            AddConnection("default", settings, settings.ReadOnly, settings.Environment);
            foreach (var profile in settings.Connections)
            {
                if (string.IsNullOrWhiteSpace(profile.Name) || _connections.ContainsKey(profile.Name))
                    throw new ArgumentException("Connection names must be unique and nonempty; 'default' is reserved.");
                var profileSettings = new RedisUISettings
                {
                    ConnectionString = profile.ConnectionString, ConfigurationOptions = profile.ConfigurationOptions,
                    ConnectionMultiplexer = profile.ConnectionMultiplexer, DataProvider = profile.DataProvider,
                    MaxScanIterationsPerPage = settings.MaxScanIterationsPerPage, MetadataConcurrency = settings.MetadataConcurrency,
                    ValuePreviewPageSize = settings.ValuePreviewPageSize, MaxValuePreviewPageSize = settings.MaxValuePreviewPageSize,
                    StringPreviewBytes = settings.StringPreviewBytes, MaxStringPreviewBytes = settings.MaxStringPreviewBytes
                };
                AddConnection(profile.Name, profileSettings, settings.ReadOnly || profile.ReadOnly, profile.Environment);
            }
        }

        private void AddConnection(string name, RedisUISettings settings, bool readOnly, string environment) =>
            _connections.Add(name, (settings.DataProvider ?? new RedisUIDataProvider(settings), settings.DataProvider == null, readOnly, environment));

        private bool Allowed(HttpContext context, string connection, int database, RedisUIOperation operation) =>
            (!_connections[connection].ReadOnly || operation is RedisUIOperation.View or RedisUIOperation.Audit) &&
            (_settings.AuthorizeOperation?.Invoke(context, new RedisUIAccess(connection, database, operation)) ?? true);

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(_basePath, out var remainingPath))
            {
                await _next(context);
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
            var subPath = (remainingPath.Value?.TrimEnd('/') ?? string.Empty).ToLowerInvariant();
            if (subPath is not ("" or "/statistics" or "/value" or "/keys" or "/metrics" or "/audit"))
            {
                context.Response.StatusCode = 404;
                return;
            }
            if (_settings.AuthorizationFilter != null && !_settings.AuthorizationFilter.Authorize(context))
            {
                context.Response.StatusCode = 403;
                return;
            }
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
                !(HttpMethods.IsPost(context.Request.Method) && subPath == ""))
            {
                context.Response.StatusCode = 405;
                context.Response.Headers.Allow = subPath == "" ? "GET, HEAD, POST" : "GET, HEAD";
                return;
            }
            if (!TryParseInt(context.Request.Query["db"].ToString(), 0, out var currentDb))
            {
                await WritePlainTextAsync(context, 400, "Invalid 'db' query parameter.");
                return;
            }
            var connection = context.Request.Query["connection"].ToString();
            if (connection.Length == 0) connection = "default";
            if (!_connections.TryGetValue(connection, out var selected))
            {
                await WritePlainTextAsync(context, 400, "Unknown connection.");
                return;
            }
            if (!Allowed(context, connection, currentDb, RedisUIOperation.View))
            {
                context.Response.StatusCode = 403;
                return;
            }
            var provider = selected.Provider;
            try
            {
                if (subPath == "/value")
                {
                    await TryHandleValuePreviewAsync(context, currentDb, provider);
                    return;
                }
                if (subPath == "/audit")
                {
                    if (!Allowed(context, connection, currentDb, RedisUIOperation.Audit)) { context.Response.StatusCode = 403; return; }
                    await WriteJsonAsync(context, _audit.ToArray().Reverse().Where(x => x.Connection == connection && x.Database == currentDb));
                    return;
                }
                if (subPath == "/metrics")
                {
                    var stats = await provider.GetStatisticsAsync(context.RequestAborted);
                    double? Number(string key) => stats.AllInfo.TryGetValue(key, out var value) &&
                        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;
                    var hits = Number("keyspace_hits");
                    var misses = Number("keyspace_misses");
                    await WriteJsonAsync(context, new { timestamp = DateTimeOffset.UtcNow, memoryBytes = Number("used_memory"),
                        operationsPerSecond = Number("instantaneous_ops_per_sec"), hits, misses, evictedKeys = Number("evicted_keys") });
                    return;
                }
                if (!TryParseLong(context.Request.Query["page"].ToString(), 0, out var cursor))
                { await WritePlainTextAsync(context, 400, "Invalid 'page' query parameter."); return; }
                if (!TryParseInt(context.Request.Query["offset"].ToString(), 0, out var pageOffset))
                { await WritePlainTextAsync(context, 400, "Invalid 'offset' query parameter."); return; }
                if (!TryParsePageSize(context.Request.Query["size"].ToString(), out var pageSize))
                { await WritePlainTextAsync(context, 400, "Invalid 'size' query parameter."); return; }
                if (HttpMethods.IsPost(context.Request.Method))
                {
                    if (!IsJsonRequest(context.Request.ContentType))
                    { await WritePlainTextAsync(context, 415, "RedisUI mutations require 'application/json'."); return; }
                    if (!ValidateAntiForgeryToken(context))
                    { await WritePlainTextAsync(context, 400, "Invalid CSRF token."); return; }
                    if (await TryHandleMutationAsync(context, currentDb, connection, provider)) context.Response.StatusCode = 204;
                    return;
                }
                if (subPath == "/keys")
                {
                    var page = await GetPageAsync(provider, currentDb, cursor, pageSize, pageOffset, context.Request.Query["key"].ToString(), context.RequestAborted);
                    await WriteJsonAsync(context, new { keys = page.Keys.Take(pageSize).Select(key => new { key.Name, key.KeyType, key.ValueSizeBytes, key.TTLSeconds }),
                        nextCursor = page.NextCursor.ToString(System.Globalization.CultureInfo.InvariantCulture), page.NextPageOffset,
                        databaseSize = await provider.GetDatabaseSizeAsync(currentDb, context.RequestAborted) });
                    return;
                }
                var keyspaces = await provider.GetKeyspacesAsync(context.RequestAborted);
                var layout = new LayoutModel
                {
                    DbList = keyspaces.Select(x => x.Db).Append("db0").Distinct().ToList(), CurrentDb = currentDb,
                    DbSize = await provider.GetDatabaseSizeAsync(currentDb, context.RequestAborted),
                    AntiForgeryToken = EnsureAntiForgeryToken(context), Connection = connection, Environment = selected.Environment,
                    CanEdit = Allowed(context, connection, currentDb, RedisUIOperation.Edit),
                    CanDelete = Allowed(context, connection, currentDb, RedisUIOperation.Delete),
                    CanAudit = Allowed(context, connection, currentDb, RedisUIOperation.Audit),
                    CanEditElements = provider is IRedisUIEditorProvider
                };
                if (subPath == "/statistics")
                {
                    var statistics = await provider.GetStatisticsAsync(context.RequestAborted);
                    statistics.Keyspaces = keyspaces.ToList();
                    layout.Section = Statistics.Build(statistics);
                }
                else
                {
                    var page = await GetPageAsync(provider, currentDb, cursor, pageSize, pageOffset, context.Request.Query["key"].ToString(), context.RequestAborted);
                    layout.Section = Main.Build(page.Keys.Take(pageSize).ToList(), page.NextCursor, page.NextPageOffset, _settings);
                }
                await WriteHtmlAsync(context, Layout.Build(layout, _settings));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("RedisUI request failed on {Connection} ({ExceptionType}).", connection, ex.GetType().Name);
                await WritePlainTextAsync(context, 500, RequestFailedMessage);
            }
        }

        private static Task<KeyPageModel> GetPageAsync(IRedisUIDataProvider provider, int database, long cursor,
            int size, int offset, string pattern, CancellationToken cancellationToken) =>
            provider is IRedisUIKeyPageProvider pages ? pages.GetKeysAsync(database, cursor, size, offset, pattern, cancellationToken) :
            provider.GetKeysAsync(database, cursor, size, pattern, cancellationToken);

        public void Dispose()
        {
            foreach (var connection in _connections.Values.Where(x => x.Owned)) connection.Provider.Dispose();
        }

        private static bool IsJsonRequest(string? contentType) =>
            !string.IsNullOrWhiteSpace(contentType) &&
            contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/redis";
            }

            var normalized = path.StartsWith('/') ? path : "/" + path;
            return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
        }

        private static bool TryParseInt(string? value, int defaultValue, out int result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = defaultValue;
                return true;
            }

            return int.TryParse(value, out result) && result >= 0;
        }

        private static bool TryParseLong(string? value, long defaultValue, out long result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = defaultValue;
                return true;
            }

            return long.TryParse(value, out result) && result >= 0;
        }

        private bool TryParsePageSize(string? value, out int pageSize)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                pageSize = Math.Min(DefaultPageSize, Math.Max(1, _settings.MaxPageSize));
                return true;
            }

            if (int.TryParse(value, out pageSize) && pageSize > 0 && pageSize <= Math.Max(1, _settings.MaxPageSize))
            {
                return true;
            }

            pageSize = 0;
            return false;
        }

        private bool TryParsePreviewCount(string? value, out int count)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                count = 0;
                return true;
            }

            var maxCount = Math.Max(
                Math.Max(1, _settings.MaxValuePreviewPageSize),
                Math.Max(1, _settings.MaxStringPreviewBytes));

            if (int.TryParse(value, out count) && count > 0 && count <= maxCount)
            {
                return true;
            }

            count = 0;
            return false;
        }

        private string EnsureAntiForgeryToken(HttpContext context)
        {
            if (!context.Request.Cookies.TryGetValue(_settings.AntiForgeryCookieName, out var antiForgeryToken) || string.IsNullOrWhiteSpace(antiForgeryToken))
            {
                antiForgeryToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            }

            context.Response.Cookies.Append(
                _settings.AntiForgeryCookieName,
                antiForgeryToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Strict,
                    Secure = context.Request.IsHttps,
                    Path = _basePath.Value
                });

            return antiForgeryToken;
        }

        private bool ValidateAntiForgeryToken(HttpContext context)
        {
            if (!context.Request.Cookies.TryGetValue(_settings.AntiForgeryCookieName, out var cookieToken) || string.IsNullOrWhiteSpace(cookieToken))
            {
                return false;
            }

            if (!context.Request.Headers.TryGetValue(_settings.AntiForgeryHeaderName, out var headerToken) || string.IsNullOrWhiteSpace(headerToken))
            {
                return false;
            }

            var cookieBytes = Encoding.UTF8.GetBytes(cookieToken);
            var headerBytes = Encoding.UTF8.GetBytes(headerToken.ToString());

            return cookieBytes.Length == headerBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(cookieBytes, headerBytes);
        }

        private async Task TryHandleValuePreviewAsync(HttpContext context, int currentDb, IRedisUIDataProvider provider)
        {
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = "GET, HEAD";
                return;
            }

            var key = context.Request.Query["key"].ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "The 'key' query parameter is required.").ConfigureAwait(false);
                return;
            }

            if (!TryParseLong(context.Request.Query["offset"].ToString(), 0, out var offset))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Invalid 'offset' query parameter.").ConfigureAwait(false);
                return;
            }

            if (!TryParsePreviewCount(context.Request.Query["count"].ToString(), out var count))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Invalid 'count' query parameter.").ConfigureAwait(false);
                return;
            }

            if (provider is not IRedisUIValuePreviewProvider valuePreviewProvider)
            {
                await WritePlainTextAsync(context, StatusCodes.Status501NotImplemented, "The configured RedisUI data provider does not support lazy value previews.").ConfigureAwait(false);
                return;
            }

            try
            {
                var preview = await valuePreviewProvider
                    .GetKeyPreviewAsync(currentDb, key, offset, count, context.Request.Query["cursor"].ToString(), context.RequestAborted)
                    .ConfigureAwait(false);

                if (preview == null)
                {
                    await WritePlainTextAsync(context, StatusCodes.Status404NotFound, "Redis key was not found.").ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(context, preview).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await WritePlainTextAsync(context, StatusCodes.Status500InternalServerError, RequestFailedMessage).ConfigureAwait(false);
            }
        }

        private async Task<bool> TryHandleMutationAsync(HttpContext context, int database, string connection, IRedisUIDataProvider provider)
        {
            PostModel? model;
            try { model = await JsonSerializer.DeserializeAsync<PostModel>(context.Request.Body, JsonOptions, context.RequestAborted); }
            catch (JsonException) { await WritePlainTextAsync(context, 400, "Request body was not valid JSON."); return false; }
            if (model == null) { await WritePlainTextAsync(context, 400, "Request body is required."); return false; }
            var deleting = !string.IsNullOrEmpty(model.DelKey) || (!string.IsNullOrEmpty(model.ElementKey) && model.ElementAction == "delete");
            var editing = !string.IsNullOrEmpty(model.InsertKey) || !string.IsNullOrEmpty(model.SetExpiryKey) ||
                (!string.IsNullOrEmpty(model.ElementKey) && model.ElementAction != "delete");
            if ((!deleting && !editing)) { await WritePlainTextAsync(context, 400, "An operation is required."); return false; }
            // Authorize the entire request before making any changes, including legacy combined requests.
            if ((deleting && !Allowed(context, connection, database, RedisUIOperation.Delete)) ||
                (editing && !Allowed(context, connection, database, RedisUIOperation.Edit)))
            { await WritePlainTextAsync(context, 403, "This operation is not permitted."); return false; }
            var type = model.InsertType?.ToLowerInvariant() ?? "string";
            var mode = model.ExpiryMode;
            if (model.InsertTTLSeconds.HasValue && mode == "keep") mode = "set"; // legacy clients
            Dictionary<string, string>? streamFields = null;
            double score = 0;
            try
            {
                if (mode is not ("keep" or "set" or "persist")) throw new ArgumentException("Invalid expiry mode.");
                if (!string.IsNullOrEmpty(model.InsertKey))
                {
                    if (model.InsertValue == null) throw new ArgumentException("Value is required.");
                    if (mode == "set" && (!model.InsertTTLSeconds.HasValue || model.InsertTTLSeconds <= 0 || model.InsertTTLSeconds > 315360000))
                        throw new ArgumentException("TTL must be between 1 and 315360000 seconds.");
                    if (type is not ("string" or "list" or "set" or "hash" or "sortedset" or "stream")) throw new ArgumentException("Invalid value type.");
                    if (type == "hash" && string.IsNullOrEmpty(model.InsertField)) throw new ArgumentException("Hash insert requires a field name.");
                    if (type == "sortedset" && (!double.TryParse(model.InsertScore, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out score) || !double.IsFinite(score)))
                        throw new ArgumentException("Sorted set insert requires a finite score.");
                    if (model.ValidateJson) { using var document = JsonDocument.Parse(model.InsertValue); }
                    if (type == "stream")
                    {
                        streamFields = JsonSerializer.Deserialize<Dictionary<string, string>>(model.InsertValue);
                        if (streamFields == null || streamFields.Count == 0 || streamFields.Values.Any(x => x == null)) throw new ArgumentException("Stream requires a JSON object of string fields.");
                    }
                }
                if (model.ExpireSeconds.HasValue && (model.ExpireSeconds < 1 || model.ExpireSeconds > 315360000)) throw new ArgumentException("Expiry must be between 1 and 315360000 seconds.");
                if (!string.IsNullOrEmpty(model.ElementKey))
                {
                    if (provider is not IRedisUIEditorProvider) { await WritePlainTextAsync(context, 501, "Provider does not support element editing."); return false; }
                    if (model.ElementType is not ("hash" or "list" or "set" or "sortedset" or "stream") || model.ElementAction is not ("update" or "delete") ||
                        (model.ElementType == "stream" && model.ElementAction != "delete") || !double.IsFinite(model.ElementScore)) throw new ArgumentException("Invalid element operation.");
                    if (model.ElementType == "list" && (!long.TryParse(model.ElementSelector, out var index) || index < 0 || model.ExpectedValue == null)) throw new ArgumentException("List edits require an index and expected value.");
                    if (model.ElementType == "stream" && !System.Text.RegularExpressions.Regex.IsMatch(model.ElementSelector, @"^\d+-\d+$")) throw new ArgumentException("Invalid stream entry ID.");
                }
                if (!string.IsNullOrEmpty(model.InsertKey) && type == "string" && provider is not IRedisUIEditorProvider &&
                    (model.ExpectedValue != null || mode != "keep"))
                { await WritePlainTextAsync(context, 501, "Provider does not support atomic string editing."); return false; }
            }
            catch (JsonException) { await WritePlainTextAsync(context, 400, "Value is not valid JSON."); return false; }
            catch (ArgumentException ex) { await WritePlainTextAsync(context, 400, ex.Message); return false; }

            async Task Run(string operation, string key, Func<Task> action)
            {
                try { await action(); RecordAudit(context, connection, database, operation, key, true); }
                catch { RecordAudit(context, connection, database, operation, key, false); throw; }
            }
            var ct = context.RequestAborted;
            try
            {
                if (!string.IsNullOrEmpty(model.DelKey)) await Run("Delete", model.DelKey, () => provider.DeleteKeyAsync(database, model.DelKey, ct));
                if (!string.IsNullOrEmpty(model.InsertKey)) await Run("Write " + type, model.InsertKey, async () =>
                {
                    switch (type)
                    {
                        case "string":
                            if (provider is IRedisUIEditorProvider editor)
                            {
                                if (!await editor.WriteStringAsync(database, model.InsertKey, model.InsertValue, mode, model.InsertTTLSeconds, model.ExpectedValue, ct)) throw new EditConflictException();
                            }
                            else await provider.SetStringAsync(database, model.InsertKey, model.InsertValue, ct);
                            break;
                        case "list": await provider.ListPushAsync(database, model.InsertKey, model.InsertValue, ct); break;
                        case "set": await provider.SetAddAsync(database, model.InsertKey, model.InsertValue, ct); break;
                        case "hash": await provider.HashSetAsync(database, model.InsertKey, model.InsertField, model.InsertValue, ct); break;
                        case "sortedset": await provider.SortedSetAddAsync(database, model.InsertKey, model.InsertValue, score, ct); break;
                        case "stream": await provider.StreamAddAsync(database, model.InsertKey, streamFields!, ct); break;
                    }
                    if (type != "string" && mode != "keep") await provider.SetExpiryAsync(database, model.InsertKey,
                        mode == "set" ? TimeSpan.FromSeconds(model.InsertTTLSeconds!.Value) : null, ct);
                });
                if (!string.IsNullOrEmpty(model.SetExpiryKey)) await Run("Expiry", model.SetExpiryKey, () => provider.SetExpiryAsync(database,
                    model.SetExpiryKey, model.ExpireSeconds.HasValue ? TimeSpan.FromSeconds(model.ExpireSeconds.Value) : null, ct));
                if (!string.IsNullOrEmpty(model.ElementKey)) await Run(model.ElementAction + " " + model.ElementType + " element", model.ElementKey, async () =>
                {
                    if (!await ((IRedisUIEditorProvider)provider).EditElementAsync(database, model.ElementKey, model.ElementType, model.ElementAction,
                        model.ElementSelector, model.ElementValue, model.ExpectedValue, model.ElementScore, ct)) throw new EditConflictException();
                });
                return true;
            }
            catch (EditConflictException) { await WritePlainTextAsync(context, 409, "The value changed or no longer exists. Refresh before editing again."); return false; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("RedisUI mutation failed on {Connection} ({ExceptionType}).", connection, ex.GetType().Name);
                await WritePlainTextAsync(context, 500, RequestFailedMessage);
                return false;
            }
        }

        private sealed class EditConflictException : Exception { }

        private void RecordAudit(HttpContext context, string connection, int database, string operation, string key, bool succeeded)
        {
            var entry = new RedisUIAuditEntry(DateTimeOffset.UtcNow, context.User.Identity?.Name ?? "anonymous", connection, database, operation, key, succeeded);
            _audit.Enqueue(entry);
            while (_audit.Count > Math.Clamp(_settings.AuditCapacity, 0, 10000)) _audit.TryDequeue(out _);
            try { _settings.AuditSink?.Invoke(entry); }
            catch (Exception ex) { _logger.LogWarning("RedisUI audit sink failed ({ExceptionType}).", ex.GetType().Name); }
        }

        private static async Task WriteHtmlAsync(HttpContext context, string html)
        {
            context.Response.ContentType = "text/html; charset=utf-8";

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.WriteAsync(html).ConfigureAwait(false);
            }
        }

        private static async Task WriteJsonAsync<T>(HttpContext context, T payload)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions)).ConfigureAwait(false);
            }
        }

        private static async Task WritePlainTextAsync(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain; charset=utf-8";

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.WriteAsync(message).ConfigureAwait(false);
            }
        }
    }
}
