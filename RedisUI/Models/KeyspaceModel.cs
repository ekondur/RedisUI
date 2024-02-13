using System.Collections.Generic;

namespace RedisUI.Models
{
    public class KeyspaceModel
    {
        public string Db { get; private set; }
        public string Keys { get; private set; }
        public string Expires { get; private set; }
        public string Avg_Ttl { get; private set; }

        public static KeyspaceModel Instance(string input)
        {
            string[] parts = input.Split(':');
            string database = parts[0].TrimStart('d', 'b');
            string[] attributes = parts[1].Split(',');

            var attributeMap = new Dictionary<string, string>();

            foreach (string attribute in attributes)
            {
                string[] keyValue = attribute.Split('=');
                string key = keyValue[0];
                string value = keyValue[1];
                attributeMap[key] = value;
            }

            return new KeyspaceModel
            {
                Db = database,
                Keys = attributeMap["keys"],
                Expires = attributeMap["expires"],
                Avg_Ttl = attributeMap["avg_ttl"]
            };
        }
    }
}
