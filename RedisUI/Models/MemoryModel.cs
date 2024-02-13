using RedisUI.Helpers;

namespace RedisUI.Models
{
    public class MemoryModel
    {
        public long UsedMemory { get; private set; }
        public long UsedMemoryPeak { get; private set; }
        public long UsedMemoryLua { get; private set; }

        public static MemoryModel Instance(string input)
        {
            var info = input.ToInfo();

            return new MemoryModel
            {
                UsedMemory = long.Parse(info["used_memory"]),
                UsedMemoryPeak = long.Parse(info["used_memory_peak"]),
                UsedMemoryLua = long.Parse(info["used_memory_lua"])
            };
        }
    }
}
