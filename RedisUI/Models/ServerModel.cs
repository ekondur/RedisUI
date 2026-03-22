using RedisUI.Helpers;

namespace RedisUI.Models
{
    public class ServerModel
    {
        public string RedisVersion { get; private set; } = string.Empty;

        public string RedisMode { get; private set; } = string.Empty;

        public string TcpPort { get; private set; } = string.Empty;

        public static ServerModel Instance(string input)
        {
            var info = input.ToInfo();

            return new ServerModel
            {
                RedisVersion = info.TryGetValue("redis_version", out var redisVersion) ? redisVersion : string.Empty,
                RedisMode = info.TryGetValue("redis_mode", out var redisMode) ? redisMode : string.Empty,
                TcpPort = info.TryGetValue("tcp_port", out var tcpPort) ? tcpPort : string.Empty
            };
        }
    }
}
