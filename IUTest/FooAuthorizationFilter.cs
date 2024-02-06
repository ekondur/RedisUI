using RedisUI;

namespace IUTest
{
    public class FooAuthorizationFilter : IRedisAuthorizationFilter
    {
        public bool Authorize(HttpContext context)
        {
            return true;
            //return context.User.Identity != null && context.User.Identity.IsAuthenticated;
        }
    }
}
