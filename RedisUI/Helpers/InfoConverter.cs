using System;
using System.Collections.Generic;

namespace RedisUI.Helpers
{
    public static class InfoConverter
    {
        public static Dictionary<string, string> ToInfo(this string input)
        {
            string[] rows = input.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            var attributeMap = new Dictionary<string, string>();

            foreach (string row in rows)
            {
                if (!string.IsNullOrEmpty(row))
                {
                    string[] keyValue = row.Split(':');
                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0];
                        string value = keyValue[1];
                        attributeMap[key] = value;
                    }
                }
            }

            return attributeMap;
        }
    }
}
