namespace RedisUI.Models
{
    internal class PostModel
    {
        public string DelKey { get; set; } = string.Empty;

        public string InsertKey { get; set; } = string.Empty;

        public string InsertType { get; set; } = "string";

        public string InsertValue { get; set; } = string.Empty;

        /// <summary>Hash field name.</summary>
        public string InsertField { get; set; } = string.Empty;

        /// <summary>SortedSet score as a string.</summary>
        public string InsertScore { get; set; } = string.Empty;

        /// <summary>Optional TTL in seconds applied after insert. Null or 0 = no expiry.</summary>
        public long? InsertTTLSeconds { get; set; }

        /// <summary>Key on which to set or remove expiry.</summary>
        public string SetExpiryKey { get; set; } = string.Empty;

        /// <summary>TTL in seconds for SetExpiryKey. Null = PERSIST (remove expiry).</summary>
        public long? ExpireSeconds { get; set; }
    }
}
