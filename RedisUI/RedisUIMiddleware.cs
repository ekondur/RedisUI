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

namespace RedisUI
{
    public class RedisUIMiddleware : IDisposable
    {
        private const int DefaultPageSize = 10;
        private readonly RequestDelegate _next;
        private readonly RedisUISettings _settings;
        private readonly IRedisUIDataProvider _dataProvider;
        private readonly bool _ownsDataProvider;
        private readonly PathString _basePath;

        public RedisUIMiddleware(RequestDelegate next, RedisUISettings settings)
        {
            _next = next;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _basePath = new PathString(NormalizePath(_settings.Path));
            _settings.Path = _basePath.Value ?? "/redis";

            if (_settings.DataProvider != null)
            {
                _dataProvider = _settings.DataProvider;
                _ownsDataProvider = false;
            }
            else
            {
                _dataProvider = new RedisUIDataProvider(_settings);
                _ownsDataProvider = true;
            }
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments(_basePath, out var remainingPath))
            {
                await _next(context);
                return;
            }

            var subPath = remainingPath.Value?.TrimEnd('/') ?? string.Empty;
            if (subPath.Length > 0 && !string.Equals(subPath, "/statistics", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (_settings.AuthorizationFilter != null && !_settings.AuthorizationFilter.Authorize(context))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method) &&
                !HttpMethods.IsHead(context.Request.Method) &&
                !HttpMethods.IsPost(context.Request.Method))
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                context.Response.Headers.Allow = "GET, HEAD, POST";
                return;
            }

            if (!TryParseInt(context.Request.Query["db"].ToString(), 0, out var currentDb))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Invalid 'db' query parameter.");
                return;
            }

            if (!TryParseLong(context.Request.Query["page"].ToString(), 0, out var cursor))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Invalid 'page' query parameter.");
                return;
            }

            if (!TryParsePageSize(context.Request.Query["size"].ToString(), out var pageSize))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Invalid 'size' query parameter.");
                return;
            }

            if (HttpMethods.IsPost(context.Request.Method))
            {
                if (!IsJsonRequest(context.Request.ContentType))
                {
                    await WritePlainTextAsync(context, StatusCodes.Status415UnsupportedMediaType, "RedisUI mutations require 'application/json'.");
                    return;
                }

                if (!ValidateAntiForgeryToken(context))
                {
                    await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Invalid CSRF token.");
                    return;
                }

                if (!await TryHandleMutationAsync(context, currentDb).ConfigureAwait(false))
                {
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }

            var antiForgeryToken = EnsureAntiForgeryToken(context);
            var keyspaces = await _dataProvider.GetKeyspacesAsync(context.RequestAborted).ConfigureAwait(false);
            var layoutModel = new LayoutModel
            {
                DbList = new System.Collections.Generic.List<string>(System.Linq.Enumerable.Select(keyspaces, x => x.Db)),
                CurrentDb = currentDb,
                DbSize = await _dataProvider.GetDatabaseSizeAsync(currentDb, context.RequestAborted).ConfigureAwait(false),
                AntiForgeryToken = antiForgeryToken
            };

            if (string.Equals(subPath, "/statistics", StringComparison.OrdinalIgnoreCase))
            {
                var statistics = await _dataProvider.GetStatisticsAsync(context.RequestAborted).ConfigureAwait(false);
                statistics.Keyspaces = new System.Collections.Generic.List<KeyspaceModel>(keyspaces);
                layoutModel.Section = Statistics.Build(statistics);
                await WriteHtmlAsync(context, Layout.Build(layoutModel, _settings)).ConfigureAwait(false);
                return;
            }

            var searchKey = context.Request.Query["key"].ToString();
            var keyPage = await _dataProvider.GetKeysAsync(currentDb, cursor, pageSize, searchKey, context.RequestAborted).ConfigureAwait(false);

            layoutModel.Section = Main.Build(keyPage.Keys, keyPage.NextCursor);
            await WriteHtmlAsync(context, Layout.Build(layoutModel, _settings)).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_ownsDataProvider)
            {
                _dataProvider.Dispose();
            }
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
                pageSize = DefaultPageSize;
                return true;
            }

            if (int.TryParse(value, out pageSize) && pageSize > 0 && pageSize <= Math.Max(1, _settings.MaxPageSize))
            {
                return true;
            }

            pageSize = 0;
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

        private async Task<bool> TryHandleMutationAsync(HttpContext context, int currentDb)
        {
            context.Request.EnableBuffering();
            context.Request.Body.Seek(0, SeekOrigin.Begin);

            using var stream = new StreamReader(context.Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
            var body = await stream.ReadToEndAsync().ConfigureAwait(false);
            context.Request.Body.Seek(0, SeekOrigin.Begin);

            if (string.IsNullOrWhiteSpace(body))
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Request body is required.").ConfigureAwait(false);
                return false;
            }

            PostModel? postModel;
            try
            {
                postModel = JsonSerializer.Deserialize<PostModel>(body);
            }
            catch (JsonException)
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Request body was not valid JSON.").ConfigureAwait(false);
                return false;
            }

            if (postModel == null)
            {
                await WritePlainTextAsync(context, StatusCodes.Status400BadRequest, "Request body was empty.").ConfigureAwait(false);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(postModel.DelKey))
            {
                await _dataProvider.DeleteKeyAsync(currentDb, postModel.DelKey, context.RequestAborted).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(postModel.InsertKey) && !string.IsNullOrWhiteSpace(postModel.InsertValue))
            {
                await _dataProvider.SetStringAsync(currentDb, postModel.InsertKey, postModel.InsertValue, context.RequestAborted).ConfigureAwait(false);
            }

            return true;
        }

        private static async Task WriteHtmlAsync(HttpContext context, string html)
        {
            context.Response.ContentType = "text/html; charset=utf-8";

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await context.Response.WriteAsync(html).ConfigureAwait(false);
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
