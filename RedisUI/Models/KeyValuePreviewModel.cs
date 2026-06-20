namespace RedisUI.Models
{
    public class KeyValuePreviewModel
    {
        public string Name { get; set; } = string.Empty;

        public string KeyType { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Base64Value { get; set; } = string.Empty;

        public string ViewerFormat { get; set; } = "text";

        public long ValueSizeBytes { get; set; }

        public long? TTLSeconds { get; set; }

        public long Offset { get; set; }

        public long NextOffset { get; set; }

        public int Count { get; set; }

        public long TotalItems { get; set; }

        public string PagingUnit { get; set; } = "items";

        public string PageMode { get; set; } = "offset";

        public string Cursor { get; set; } = string.Empty;

        public string NextCursor { get; set; } = string.Empty;

        public bool HasMore { get; set; }
    }
}
