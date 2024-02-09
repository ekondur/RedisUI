using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using StackExchange.Redis;
using RedisUI.Pages;
using System.Linq;
using RedisUI.Models;

namespace RedisUI
{
    public class RedisUIMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RedisUISettings _settings;

        public RedisUIMiddleware(RequestDelegate next, RedisUISettings settings)
        {
            _next = next;
            _settings = settings;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.ToString().StartsWith(_settings.Path))
            {
                await _next(context);
                return;
            }

            if (_settings.AuthorizationFilter != null && !_settings.AuthorizationFilter.Authorize(context))
            {
                context.Response.StatusCode = 403;
                return;
            }

            var db = context.Request.Query["db"].ToString();
            int currentDb = string.IsNullOrEmpty(db) ? 0 : int.Parse(db);

            IDatabase redisDb = ConnectionMultiplexer.Connect(_settings.ConnectionString).GetDatabase(currentDb);

            var dbSize = await redisDb.ExecuteAsync("DBSIZE");

            if (context.Request.Path.ToString() == $"{_settings.Path}/statistics")
            {
                var keyspace = await redisDb.ExecuteAsync("INFO", "KEYSPACE");
                var keyspaces = keyspace
                    .ToString()
                    .Replace("# Keyspace", "")
                    .Split("\r\n")
                    .Where(item => !string.IsNullOrEmpty(item))
                    .Select(item => Keyspace.ToKeyspace(item))
                    .ToList();

                await context.Response.WriteAsync(MainLayout.Build(Statistics.Build(keyspaces), dbSize.ToString(), currentDb, _settings));
                return;
            }

            var page = context.Request.Query["page"].ToString();
            long cursor = string.IsNullOrEmpty(page) ? 0 : long.Parse(page);

            var pageSize = context.Request.Query.TryGetValue("size", out var size) ? size.ToString() : "10";

            var searchKey = context.Request.Query["key"].ToString();

            RedisResult result;
            if (string.IsNullOrEmpty(searchKey))
            {
                result = await redisDb.ExecuteAsync("SCAN", cursor.ToString(), "COUNT", pageSize.ToString());
            }
            else
            {
                result = await redisDb.ExecuteAsync("SCAN", cursor.ToString(), "MATCH", searchKey, "COUNT", pageSize.ToString());
            }

            var innerResult = (RedisResult[])result;
            var keys = ((string[])innerResult[1])
                .Select(x => new KeyModel
                {
                    KeyName = x
                })
                .ToList();

            foreach (var key in keys)
            {
                key.KeyType = await redisDb.KeyTypeAsync(key.KeyName);
                key.Value = await redisDb.StringGetAsync(key.KeyName);
            }

            await context.Response.WriteAsync(MainLayout.Build(MainPage.Build(keys, keys.Count > 0 ? long.Parse((string)innerResult[0]) : 0), dbSize.ToString(), currentDb, _settings));
        }
    }
}