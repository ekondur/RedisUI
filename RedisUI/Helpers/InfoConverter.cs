using System;
using System.Collections.Generic;

namespace RedisUI.Helpers
{
    public static class InfoConverter
    {
        public static Dictionary<string, string> ToInfo(this string input)
        {
            var rows = input.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var attributeMap = new Dictionary<string, string>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row) || row.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = row.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = row[..separatorIndex];
                var value = row[(separatorIndex + 1)..];
                attributeMap[key] = value;
            }

            return attributeMap;
        }
    }
}
