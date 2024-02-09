using RedisUI.Models;
using System.Collections.Generic;
using System.Text;

namespace RedisUI.Pages
{
    public static class Statistics
    {
        public static string Build(List<Keyspace> keyspaces)
        {
            var tbody = new StringBuilder();
            foreach (var keyspace in keyspaces)
            {
                tbody.Append($"<tr><td>{keyspace.Db}</td><td>{keyspace.Keys}</td><td>{keyspace.Expires}</td><td>{keyspace.Avg_Ttl}</td></tr>");
            }
            return @$"
<div class=""row"">
    <div class=""col-6"">
        <table class=""table table-hover"">
          <thead>
            <tr class=""table-active"">
              <th colspan=""4"">
                <span>
                    <svg xmlns=""http://www.w3.org/2000/svg"" width=""24"" height=""24"" fill=""currentColor"" class=""bi bi-key"" viewBox=""0 0 16 16"">
                      <path d=""M0 8a4 4 0 0 1 7.465-2H14a.5.5 0 0 1 .354.146l1.5 1.5a.5.5 0 0 1 0 .708l-1.5 1.5a.5.5 0 0 1-.708 0L13 9.207l-.646.647a.5.5 0 0 1-.708 0L11 9.207l-.646.647a.5.5 0 0 1-.708 0L9 9.207l-.646.647A.5.5 0 0 1 8 10h-.535A4 4 0 0 1 0 8m4-3a3 3 0 1 0 2.712 4.285A.5.5 0 0 1 7.163 9h.63l.853-.854a.5.5 0 0 1 .708 0l.646.647.646-.647a.5.5 0 0 1 .708 0l.646.647.646-.647a.5.5 0 0 1 .708 0l.646.647.793-.793-1-1h-6.63a.5.5 0 0 1-.451-.285A3 3 0 0 0 4 5""/>
                      <path d=""M4 8a1 1 0 1 1-2 0 1 1 0 0 1 2 0""/>
                </svg>
                </span>Key Statistics
            </th>
            </tr>
            <tr>
              <th scope=""col"">DB</th>
              <th scope=""col"">Keys</th>
              <th scope=""col"">Expires</th>
              <th scope=""col"">Avg_Ttl</th>
            </tr>
          </thead>
          <tbody>
            {tbody}       
          </tbody>
        </table>
    </div>
</div>"
;
        }
    }
}
