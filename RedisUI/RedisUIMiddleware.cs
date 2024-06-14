using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using StackExchange.Redis;
using RedisUI.Pages;
using System.Linq;
using RedisUI.Models;
using RedisUI.Helpers;
using System;
using System.IO;
using System.Text.Json;

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

            IDatabase redisDb;
            
            if (_settings.ConfigurationOptions != null)
            {
                redisDb = ConnectionMultiplexer.Connect(_settings.ConfigurationOptions).GetDatabase(currentDb);
            }
            else
            {
                redisDb = ConnectionMultiplexer.Connect(_settings.ConnectionString).GetDatabase(currentDb);
            }    

            var dbSize = await redisDb.ExecuteAsync("DBSIZE");

            var keyspace = await redisDb.ExecuteAsync("INFO", "KEYSPACE");
            var keyspaces = keyspace
                .ToString()
                .Replace("# Keyspace", "")
                .Split(new string[] { "\r\n" }, StringSplitOptions.None)
                .Where(item => !string.IsNullOrEmpty(item))
                .Select(KeyspaceModel.Instance)
                .ToList();

            var layoutModel = new LayoutModel
            {
                DbList = keyspaces.Select(x => x.Db).ToList(),
                CurrentDb = currentDb,
                DbSize = dbSize.ToString()
            };

            if (context.Request.Path.ToString() == $"{_settings.Path}/statistics")
            {
                var serverInfo = await redisDb.ExecuteAsync("INFO", "SERVER");
                var memoryInfo = await redisDb.ExecuteAsync("INFO", "MEMORY");
                var statsInfo = await redisDb.ExecuteAsync("INFO", "STATS");
                var allInfo = await redisDb.ExecuteAsync("INFO");

                var model = new StatisticsVm
                {
                    Keyspaces = keyspaces,
                    Server = ServerModel.Instance(serverInfo.ToString()),
                    Memory = MemoryModel.Instance(memoryInfo.ToString()),
                    Stats = StatsModel.Instance(statsInfo.ToString()),
                    AllInfo = allInfo.ToString().ToInfo()
                };

                layoutModel.Section = Statistics.Build(model);

                await context.Response.WriteAsync(Layout.Build(layoutModel, _settings));
                return;
            }

            var page = context.Request.Query["page"].ToString();
            long cursor = string.IsNullOrEmpty(page) ? 0 : long.Parse(page);

            var pageSize = context.Request.Query.TryGetValue("size", out var size) ? size.ToString() : "10";

            var searchKey = context.Request.Query["key"].ToString();

            context.Request.EnableBuffering();
            context.Request.Body.Seek(0, SeekOrigin.Begin);
            using (var stream = new StreamReader(context.Request.Body))
            {
                string body = await stream.ReadToEndAsync();

                if (!string.IsNullOrEmpty(body))
                {
                    var postModel = JsonSerializer.Deserialize<PostModel>(body);

                    if (postModel != null)
                    {
                        await redisDb.ExecuteAsync("DEL", postModel.DelKey);
                    }
                }
            }

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
                    Name = x
                })
                .ToList();

            foreach (var key in keys)
            {
                key.KeyType = await redisDb.KeyTypeAsync(key.Name);
                switch (key.KeyType)
                {
                    case RedisType.String:
                        key.Value = await redisDb.StringGetAsync(key.Name);
                        key.Badge = "light";
                        break;
                    case RedisType.Hash:
                        var hashValue = await redisDb.HashGetAllAsync(key.Name);
                        key.Value = string.Join(", ", hashValue.Select(x => $"{x.Name}: {x.Value}"));
                        key.Badge = "success";
                        break;
                    case RedisType.List:
                        var listValue = await redisDb.ListRangeAsync(key.Name);
                        key.Value = string.Join(", ", listValue.Select(x => x));
                        key.Badge = "warning";
                        break;
                    case RedisType.Set:
                        var setValue = await redisDb.SetMembersAsync(key.Name);
                        key.Value = string.Join(", ", setValue.Select(x => x));
                        key.Badge = "primary";
                        break;
                    case RedisType.None:
                        key.Value = await redisDb.StringGetAsync(key.Name);
                        key.Badge = "secondary";
                        break;
                }
            }

            layoutModel.Section = Main.Build(keys, keys.Count > 0 ? long.Parse((string)innerResult[0]) : 0);

            await context.Response.WriteAsync(Layout.Build(layoutModel, _settings));
        }
    }
}