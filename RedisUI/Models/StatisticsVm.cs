using System.Collections.Generic;

namespace RedisUI.Models
{
    public class StatisticsVm
    {
        public ServerModel Server { get; set; } = new();

        public MemoryModel Memory { get; set; } = new();

        public StatsModel Stats { get; set; } = new();

        public Dictionary<string, string> AllInfo { get; set; } = new();

        public List<KeyspaceModel> Keyspaces { get; set; } = new();
    }
}
