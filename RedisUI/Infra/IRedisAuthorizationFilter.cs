using Microsoft.AspNetCore.Http;

namespace RedisUI
{
    public interface IRedisAuthorizationFilter
    {
        bool Authorize(HttpContext context);
    }
}
