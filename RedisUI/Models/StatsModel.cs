using RedisUI.Helpers;

namespace RedisUI.Models
{
    public class StatsModel
    {
        public string TotalConnectionsReceived { get; private set; } = string.Empty;

        public string TotalCommandsProcessed { get; private set; } = string.Empty;

        public string ExpiredKeys { get; private set; } = string.Empty;

        public static StatsModel Instance(string input)
        {
            var info = input.ToInfo();

            return new StatsModel
            {
                TotalConnectionsReceived = info.TryGetValue("total_connections_received", out var connections) ? connections : string.Empty,
                TotalCommandsProcessed = info.TryGetValue("total_commands_processed", out var commands) ? commands : string.Empty,
                ExpiredKeys = info.TryGetValue("expired_keys", out var expired) ? expired : string.Empty
            };
        }
    }
}
