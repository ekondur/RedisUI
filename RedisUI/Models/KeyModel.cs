namespace RedisUI.Models
{
    public class KeyModel
    {
        public string Name { get; set; } = string.Empty;

        public string KeyType { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Badge { get; set; } = string.Empty;

        public string ViewerFormat { get; set; } = "text";

        public string Base64Value { get; set; } = string.Empty;

        public long ValueSizeBytes { get; set; }

        /// <summary>Remaining TTL in seconds, or null if the key has no expiry.</summary>
        public long? TTLSeconds { get; set; }
    }
}
