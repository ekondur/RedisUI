using System.Collections.Generic;

namespace RedisUI.Models
{
    public class LayoutModel
    {
        public string Section { get; set; }
        public string DbSize { get; set; }
        public int CurrentDb { get; set; }
        public List<string> DbList { get; set; }
    }
}
