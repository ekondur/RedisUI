using System.Collections.Generic;

namespace RedisUI.Models
{
    public class KeyspaceModel
    {
        public string Db { get; private set; } = string.Empty;

        public string Keys { get; private set; } = string.Empty;

        public string Expires { get; private set; } = string.Empty;

        public string Avg_Ttl { get; private set; } = string.Empty;

        public static KeyspaceModel Instance(string input)
        {
            var parts = input.Split(':');
            if (parts.Length < 2)
            {
                return new KeyspaceModel();
            }

            var database = parts[0].TrimStart('d', 'b');
            var attributes = parts[1].Split(',');
            var attributeMap = new Dictionary<string, string>();

            foreach (var attribute in attributes)
            {
                var keyValue = attribute.Split('=');
                if (keyValue.Length < 2)
                {
                    continue;
                }

                attributeMap[keyValue[0]] = keyValue[1];
            }

            return new KeyspaceModel
            {
                Db = database,
                Keys = attributeMap.TryGetValue("keys", out var keys) ? keys : "0",
                Expires = attributeMap.TryGetValue("expires", out var expires) ? expires : "0",
                Avg_Ttl = attributeMap.TryGetValue("avg_ttl", out var avgTtl) ? avgTtl : "0"
            };
        }
    }
}
