using StackExchange.Redis;

namespace RedisUI.Models
{
    public class KeyModel
    {
        public string KeyName { get; set; }

        public RedisType KeyType { get; set; }

        public RedisValue Value { get; set; }
    }
}
