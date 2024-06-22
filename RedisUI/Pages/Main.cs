using RedisUI.Contents;
using RedisUI.Helpers;
using RedisUI.Models;
using System.Collections.Generic;
using System.Text;

namespace RedisUI.Pages
{
    public static class Main
    {
        public static string Build(List<KeyModel> keys, long next)
        {
            var tbody = new StringBuilder();
            foreach (var key in keys)
            {
                var columns = $"<td><span class=\"badge text-bg-{key.Badge}\">{key.KeyType}</span></td><td>{key.Name}</td><td>{key.Value.Length().ToKilobytes()}</td>";

                tbody.Append($"<tr style=\"cursor: pointer;\" data-value='{key.Value}'>{columns}<td><a onclick=\"confirmDelete('{key.Name}')\" class=\"btn btn-sm btn-outline-danger\"><span>{Icons.Delete}</span></a></td></tr>");
            }

            var html = $@"
    {InsertModal.Build()}
    <div class=""row"">
        <div class=""col-6""><div id=""search"" class=""input-group mb-3""></div></div>
        <div class=""col-1"">
            <button type=""button"" class=""btn btn-outline-success"" data-bs-toggle=""modal"" data-bs-target=""#insertModal"" title=""Add or Edit Key"">
              {Icons.KeyLg}
            </button>
        </div>
        <div class=""col-5"">
            <ul class=""pagination"">
                <li class=""page-item"" id=""size10""><a class=""page-link"" href=""javascript:setSize(10);"">10</a></li>
                <li class=""page-item"" id=""size20""><a class=""page-link"" href=""javascript:setSize(20);"">20</a></li>
                <li class=""page-item"" id=""size50""><a class=""page-link"" href=""javascript:setSize(50);"">50</a></li>
                <li class=""page-item"" id=""size100""><a class=""page-link"" href=""javascript:setSize(100);"">100</a></li>
                <li class=""page-item"" id=""size500""><a class=""page-link"" href=""javascript:setSize(500);"">500</a></li>
                <li class=""page-item"" id=""size1000""><a class=""page-link"" href=""javascript:setSize(1000);"">1000</a></li>
            </ul>
        </div>
    </div>
    <div class=""row"">
        <div class=""col-6"">
            <div class=""table-responsive"">
                <table class=""table table-hover"" id=""redisTable"">
                    <thead class=""sticky-top"">
                        <tr class=""table-active"">
                            <th scope=""col"">Type</th>
                            <th scope=""col"">Key</th>
                            <th scope=""col"">Size(KB)</th>
                            <th scope=""col"" class=""col-md-1"">#</th> 
                        </tr>
                    </thead>
                    <tbody>
                        {tbody}
                    </tbody>
                </table>
            </div>
            <div class=""pagination"" id=""pagination"">
            </div>
        </div>

        <div class=""col-6"">
            <div class=""card border-info mb-3 sticky-top"">
                <div class=""card-header"">Value</div>
                <div class=""card-body"">
                    <code><p id=""valueContent"">Click on a key to get value...</p></code>
                </div>
            </div>
        </div>
    </div>

<script>
    
    document.addEventListener('DOMContentLoaded', function () {{
        let currentPage = 0;
        let currentDb = 0;
        let currentKey = null;
        let currentSize = 10;

        const table = document.getElementById('redisTable');

        var searchParams = new URLSearchParams(window.location.search);
		var paramPage = searchParams.get('page');
        var paramDb = searchParams.get('db');
        var paramKey = searchParams.get('key');
        var paramSize = searchParams.get('size');

        if (paramPage) {{
            currentPage = paramPage;
		}}

        if (paramDb) {{
            currentDb = paramDb;
		}}

        if (paramKey) {{
            currentKey = paramKey;
        }}

        if (paramSize) {{
            currentSize = paramSize;
        }}

        const paginationContainer = document.getElementById('pagination');
        paginationContainer.innerHTML = '';

        const nBtn = document.createElement('button');
        nBtn.innerText = {next} == 0 ? 'Back to top' : 'Next';
        nBtn.className = ""btn btn-outline-success"";
        nBtn.id = ""btnNext"";
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

			var newQueryString = ""page="" + page + ""&db="" + db + ""&size="" + currentSize;

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

    var sizeElement = document.getElementById(""size""+currentSize);
    sizeElement.classList.add(""active"");

    document.getElementById(""btnNext"").hidden = '{next}' == currentPage;

    }});

    let currentSize = 10;
    let currentKey = '';
    let currentDb = 0;
    let currentPage = 0;

    var searchParams = new URLSearchParams(window.location.search);
    var paramDb = searchParams.get('db');
    var paramKey = searchParams.get('key');
    var paramSize = searchParams.get('size');
	var paramPage = searchParams.get('page');

    if (paramDb) {{
        currentDb = paramDb;
	}}

    if (paramKey) {{
        currentKey = paramKey;
    }}

    if (paramSize) {{
        currentSize = paramSize;
    }}

    if (paramPage) {{
        currentPage = paramPage;
	}}

    var currentPath = window.location.href.replace(window.location.search, '');

	newQueryString = ""&db="" + currentDb + ""&size="" + currentSize + ""&key="" + currentKey + ""&page="" + currentPage;

	newUrl = currentPath + (currentPath.indexOf('?') !== -1 ? '&' : '?') + newQueryString;

    function confirmDelete(del){{
        if (confirm(""Are you sure to delete key '"" + del + ""' ?"") == true) 
        {{
            fetch(newUrl, {{
              method: 'POST',
              body: JSON.stringify({{
                DelKey: del,
              }}),
              headers: {{
                'Content-type': 'application/json; charset=UTF-8'
              }}
            }}).then(function(response) {{
                window.location = newUrl.replace('#', '');
            }});
        }}
    }};

    function saveKey(){{
        fetch(newUrl, {{
          method: 'POST',
          body: JSON.stringify({{
            InsertKey: document.getElementById(""insertKey"").value,
            InsertValue: document.getElementById(""insertValue"").value
          }}),
          headers: {{
            'Content-type': 'application/json; charset=UTF-8'
          }}
        }}).then(function(response) {{
            window.location = newUrl.replace('#', '');
        }});
    }}

    function checkRequired(){{
        var insertKey = document.getElementById(""insertKey"").value;
        var insertValue = document.getElementById(""insertValue"").value;

        if (insertKey && insertValue){{
            document.getElementById(""btnSave"").disabled = false;
        }}
        else{{
            document.getElementById(""btnSave"").disabled = true;
        }}
    }}

</script>
";
            return html;
        }
    }
}
