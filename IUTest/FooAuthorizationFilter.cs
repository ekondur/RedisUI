using RedisUI;

namespace IUTest
{
    public class FooAuthorizationFilter : IRedisAuthorizationFilter
    {
        private readonly bool _isDevelopment;

        public FooAuthorizationFilter(bool isDevelopment)
        {
            _isDevelopment = isDevelopment;
        }

        public bool Authorize(HttpContext context)
        {
            return _isDevelopment || (context.User.Identity != null && context.User.Identity.IsAuthenticated);
        }
    }
}
