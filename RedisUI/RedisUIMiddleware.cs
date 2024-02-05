using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using StackExchange.Redis;
using RedisUI.Pages;
using System.Linq;

namespace RedisUI
{
    public class RedisUIMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _path;
        private readonly string _connString;

        public RedisUIMiddleware(RequestDelegate next, string connString, string path)
        {
            _next = next;
            _path = path;
            _connString = connString;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.ToString().StartsWith(_path))
            {
                var db = context.Request.Query["db"].ToString();
                int currentDb = string.IsNullOrEmpty(db) ? 0 : int.Parse(db);

                IDatabase redisDb = ConnectionMultiplexer.Connect(_connString).GetDatabase(currentDb);
             
                int pageSize = 10;
                var page = context.Request.Query["page"].ToString();

                long cursor = string.IsNullOrEmpty(page) ? 0 : long.Parse(page);

                var searchKey = context.Request.Query["key"].ToString();

                RedisResult result = null;
                if (string.IsNullOrEmpty(searchKey))
                {
                    result = redisDb.Execute("SCAN", cursor.ToString(), "COUNT", pageSize.ToString());
                }
                else
                {
                    result = redisDb.Execute("SCAN", cursor.ToString(), "MATCH", searchKey, "COUNT", pageSize.ToString());
                }

                var innerResult = (RedisResult[])result;
                var keys = (string[])innerResult[1];

                var keyValues = keys.ToDictionary(x => x, null);

                foreach (var key in keys)
                {
                    keyValues[key] = redisDb.StringGet(key);
                }

                await context.Response.WriteAsync(MainLayout.Build(MainPage.Build(keyValues, keyValues.Count > 0 ? long.Parse((string)innerResult[0]) : 0)));
                return;
            }

            // Call the next delegate/middleware in the pipeline.
            await _next(context);
        }
    }
}