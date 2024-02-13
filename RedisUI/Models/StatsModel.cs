using RedisUI.Helpers;

namespace RedisUI.Models
{
    public class StatsModel
    {
        public string TotalConnectionsReceived { get; private set; }
        public string TotalCommandsProcessed { get; private set; }
        public string ExpiredKeys { get; private set; }

        public static StatsModel Instance(string input)
        {
            var info = input.ToInfo();

            return new StatsModel
            {
                TotalConnectionsReceived = info["total_connections_received"],
                TotalCommandsProcessed = info["total_commands_processed"],
                ExpiredKeys = info["expired_keys"]
            };
        }

    }
}
