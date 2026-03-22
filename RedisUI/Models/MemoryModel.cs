using System.Collections.Generic;
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
                UsedMemory = ParseValue(info, "used_memory"),
                UsedMemoryPeak = ParseValue(info, "used_memory_peak"),
                UsedMemoryLua = ParseValue(info, "used_memory_lua")
            };
        }

        private static long ParseValue(IReadOnlyDictionary<string, string> info, string key) =>
            info.TryGetValue(key, out var value) && long.TryParse(value, out var parsed)
                ? parsed
                : 0;
    }
}
