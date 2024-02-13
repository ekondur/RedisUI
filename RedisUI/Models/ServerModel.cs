using RedisUI.Helpers;

namespace RedisUI.Models
{
    public class ServerModel
    {
        public string RedisVersion { get; private set; }
        public string RedisMode { get; private set; }
        public string TcpPort { get; private set; }

        public static ServerModel Instance(string input)
        {
            var info = input.ToInfo();

            return new ServerModel
            {
                RedisVersion = info["redis_version"],
                RedisMode = info["redis_mode"],
                TcpPort = info["tcp_port"]
            };
        }
    }
}
