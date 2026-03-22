namespace RedisUI.Models
{
    public class KeyPageModel
    {
        public long NextCursor { get; set; }

        public List<KeyModel> Keys { get; set; } = new();
    }
}
