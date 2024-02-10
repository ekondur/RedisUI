using StackExchange.Redis;

namespace RedisUI.Models
{
    public class KeyModel
    {
        public string Name { get; set; }

        public RedisType KeyType { get; set; }

        public RedisValue Value { get; set; }

        public string Badge { get; set; }
    }
}
