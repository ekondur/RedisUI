using System.Collections.Generic;

namespace RedisUI.Models
{
    public class LayoutModel
    {
        public string Section { get; set; } = string.Empty;

        public string DbSize { get; set; } = string.Empty;

        public int CurrentDb { get; set; }

        public List<string> DbList { get; set; } = new();

        public string AntiForgeryToken { get; set; } = string.Empty;
    }
}
