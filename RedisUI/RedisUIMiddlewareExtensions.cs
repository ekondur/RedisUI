using Microsoft.AspNetCore.Builder;

namespace RedisUI
{
    public static class RedisUIMiddlewareExtensions
    {
        public static IApplicationBuilder UseRedisUI(this IApplicationBuilder builder, 
            string connStr, string path = "/redis")
        {
            return builder.UseMiddleware<RedisUIMiddleware>(connStr, path);
        }
    }
}
