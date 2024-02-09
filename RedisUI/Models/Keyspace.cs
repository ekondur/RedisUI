using System.Collections.Generic;

namespace RedisUI.Models
{
    public class Keyspace
    {
        public string Db { get; set; }
        public string Keys { get; set; }
        public string Expires { get; set; }
        public string Avg_Ttl { get; set; }

        public static Keyspace ToKeyspace(string input)
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

            return new Keyspace
            {
                Db = database,
                Keys = attributeMap["keys"],
                Expires = attributeMap["expires"],
                Avg_Ttl = attributeMap["avg_ttl"]
            };
        }
    }
}
