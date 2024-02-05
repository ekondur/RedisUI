using RedisUI.Models;
using System.Collections.Generic;
using System.Text;

namespace RedisUI.Pages
{
    public static class MainPage
    {
        public static string Build(List<KeyModel> keys, long next, string dbSize)
        {
            var tbody = new StringBuilder();
            foreach (var key in keys)
            {
                tbody.Append($"<tr data-value='{key.Value.ToString()}'><td>{key.KeyType}</td><td>{key.KeyName}</td></tr>");
            }

            var html = $@"
    <style>
        table {{
            border-collapse: collapse;
            width: 100%;
            margin-top: 20px;
        }}

        table, th, td {{
            border: 1px solid #ddd;
        }}

        th, td {{
            padding: 8px;
            text-align: left;
        }}

        th {{
            background-color: #f2f2f2;
        }}

        .pagination {{display: flex;
            justify-content: center;
            margin-top: 20px;
        }}

        .pagination button {{
            margin: 0 5px;
            padding: 8px 12px;
            text-decoration: none;
            cursor: pointer;
            }}

        .pagination button.active {{
            background-color: #4CAF50;
            color: white;
        }}

        .container-left{{
            width: 50%;
            float: left;
        }}

        .container-right{{
            width: 49%;
            float: left;
            margin-left: 1%;
            word-wrap: break-word;
        }}

        .table tr {{
            cursor: pointer;
        }}

        .table tbody tr:hover td, .table tbody tr:hover th {{
            background-color: #f2f2f2;
        }}

        .dbsize{{
            float: right;
        }}
</style>

<div id=""search""></div> 

<div class=""container"">
<div class=""container-left"">
<table id=""redisTable"" class=""table"">
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

<div class=""container-right"">
    <h3>Value:</h3>
    <code><p id=""valueContent"">Click on a key to get value...</p></code>
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
        nBtn.addEventListener('click', function () {{
            showPage({next}, currentDb, currentKey);
         }});
        paginationContainer.appendChild(nBtn);

        const searchContainer = document.getElementById('search');
        searchContainer.innerHTML = '';

        const sInput = document.createElement('input');
        sInput.type = ""text"";
        sInput.name = ""searchInput"";
        sInput.placeholder = ""key or pattern..."";
        if (currentKey) {{
            sInput.value = currentKey;
        }}

        const sBtn = document.createElement('button');
        sBtn.innerText = 'Search';
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

        const dbsize = document.createElement('span');
        dbsize.innerText = ""Total Keys: {dbSize}"";
        dbsize.className = ""dbsize"";

        searchContainer.appendChild(sInput);
        searchContainer.appendChild(sBtn);
        searchContainer.appendChild(dbsize);

        function showPage(page, db, key) {{
            var currentPath = window.location.href.replace(window.location.search, '');

			var newQueryString = ""page="" + page + ""&db="" + db;

            if (key) {{
                newQueryString = newQueryString + ""&key="" + key;
            }}

			// Set the modified URL
			var newUrl = currentPath + (currentPath.indexOf('?') !== -1 ? '&' : '?') + newQueryString;
			
            // Change the current page URL
            window.location = newUrl;
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
