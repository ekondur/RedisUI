namespace RedisUI.Models
{
    public class KeyPageModel
    {
        public long NextCursor { get; set; }

        /// <summary>Opaque provider continuation. Pass it back unchanged; zero means no buffered keys.</summary>
        public int NextPageOffset { get; set; }

        public List<KeyModel> Keys { get; set; } = new();
    }
}
