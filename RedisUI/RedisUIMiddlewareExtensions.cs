using Microsoft.AspNetCore.Builder;

namespace RedisUI
{
    public static class RedisUIMiddlewareExtensions
    {
        /// <summary>
        /// Adds Redis UI middleware to the request pipeline.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IApplicationBuilder UseRedisUI(this IApplicationBuilder builder)
        {
            return UseRedisUI(builder, new RedisUISettings());
        }

        /// <summary>
        /// Adds RedisUI middleware to the request pipeline.
        /// </summary>
        /// <param name="builder">The <c>IApplicationBuilder</c> instance.</param>
        /// <param name="settings">The <c>RedisUISettings</c> instance.</param>
        /// <returns>The updated <c>IApplicationBuilder</c> instance.</returns>
        public static IApplicationBuilder UseRedisUI(this IApplicationBuilder builder, RedisUISettings settings)
        {
            return builder.UseMiddleware<RedisUIMiddleware>(settings);
        }
    }
}
