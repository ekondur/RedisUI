using System.Collections.Generic;

namespace RedisUI.Models
{
    public class StatisticsVm
    {
        public ServerModel Server { get; set; }
        public MemoryModel Memory { get; set; }
        public StatsModel Stats { get; set; }
        public Dictionary<string, string> AllInfo { get; set; }
        public List<KeyspaceModel> Keyspaces { get; set; }

        public StatisticsVm()
        {
            Keyspaces = new List<KeyspaceModel>();
        }
    }
}
