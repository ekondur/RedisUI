using RedisUI.Models;
using System.Collections.Generic;
using System.Text;

namespace RedisUI.Pages
{
    public static class MainPage
    {
        public static string Build(List<KeyModel> keys, long next)
        {
            var tbody = new StringBuilder();
            foreach (var key in keys)
            {
                tbody.Append($"<tr data-value='{key.Value.ToString()}'><td><span class=\"badge text-bg-secondary\">{key.KeyType}</span></td><td>{key.KeyName}</td></tr>");
            }

            var html = $@"
    <div class=""row"">
        <div id=""search"" class=""input-group mb-3""></div> 
    </div>
    <div class=""row"">
        <div class=""col-6"">
            <table class=""table table-hover"" id=""redisTable"" class=""table"">
                <thead>
                    <tr>
                        <th>Type</th>
                        <th>Key</th>
                    </tr>
                </thead>
                <tbody>
                    {tbody}
                </tbody>
            </table>
            <div class=""pagination"" id=""pagination"">
            </div>
        </div>

        <div class=""col-6"">
            <div class=""card border-info mb-3"">
                <div class=""card-header"">Value</div>
                <div class=""card-body"">
                    <code><p id=""valueContent"">Click on a key to get value...</p></code>
                </div>
            </div>
        </div>
    <div/>

<script>
    
    document.addEventListener('DOMContentLoaded', function () {{
        let currentPage = 0;
        let currentDb = 0;
        let currentKey = null;

        const table = document.getElementById('redisTable');

        var searchParams = new URLSearchParams(window.location.search);
		var paramPage = searchParams.get('page');
        var paramDb = searchParams.get('db');
        var paramKey = searchParams.get('key');

        if (paramPage) {{
            currentPage = paramPage;
		}}

        if (paramDb) {{
            currentDb = paramDb;
		}}

        if (paramKey) {{
            currentKey = paramKey;
        }}

        const paginationContainer = document.getElementById('pagination');
        paginationContainer.innerHTML = '';

        const nBtn = document.createElement('button');
        nBtn.innerText = {next} == 0 ? 'Back to top' : 'Next';
        nBtn.className = ""btn btn-outline-success"";
        nBtn.addEventListener('click', function () {{
            showPage({next}, currentDb, currentKey);
         }});
        paginationContainer.appendChild(nBtn);

        const searchContainer = document.getElementById('search');
        searchContainer.innerHTML = '';

        const sInput = document.createElement('input');
        sInput.type = ""text"";
        sInput.name = ""searchInput"";
        sInput.className = ""form-control"";
        sInput.placeholder = ""key or pattern..."";
        if (currentKey) {{
            sInput.value = currentKey;
        }}

        const sBtn = document.createElement('button');
        sBtn.innerText = 'Search';
        sBtn.className = 'btn btn-outline-success btn-sm';
        sBtn.addEventListener('click', function () {{
            var searchText = sInput.value;
            if (searchText) {{
                showPage(0, currentDb, searchText);
            }} else {{
                showPage(0, currentDb);
            }}
         }});

        sInput.addEventListener(""keypress"", function(event) {{
          if (event.key === ""Enter"") {{
            event.preventDefault();
            sBtn.click();
          }}
        }});

        searchContainer.appendChild(sInput);
        searchContainer.appendChild(sBtn);

        function showPage(page, db, key) {{
            var currentPath = window.location.href.replace(window.location.search, '');

			var newQueryString = ""page="" + page + ""&db="" + db;

            if (key) {{
                newQueryString = newQueryString + ""&key="" + key;
            }}

			// Set the modified URL
			var newUrl = currentPath + (currentPath.indexOf('?') !== -1 ? '&' : '?') + newQueryString;
			
            // Change the current page URL
            window.location = newUrl.replace('#', '');
        }}

        const tableRows = document.querySelectorAll(""#redisTable tbody tr"");
        tableRows.forEach(row => {{
        row.addEventListener(""click"", function() {{
            const value = row.getAttribute(""data-value"");
            valueContent.textContent = JSON.stringify(value, null, 4);
        }});

      }});

    var navElement = document.getElementById(""nav""+currentDb);
    navElement.classList.add(""active"");

    }});


</script>
";
            return html;
        }
    }
}
