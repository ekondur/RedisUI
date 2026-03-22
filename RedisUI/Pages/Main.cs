using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using RedisUI.Contents;
using RedisUI.Helpers;
using RedisUI.Models;

namespace RedisUI.Pages
{
    public static class Main
    {
        public static string Build(System.Collections.Generic.List<KeyModel> keys, long next)
        {
            var encoder = HtmlEncoder.Default;
            var tbody = new StringBuilder();

            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                var size = ((long)Encoding.UTF8.GetByteCount(key.Value ?? string.Empty)).ToKilobytes();
                var columns = $@"<td><span class=""badge text-bg-{encoder.Encode(key.Badge)}"">{encoder.Encode(key.KeyType)}</span></td><td>{encoder.Encode(key.Name)}</td><td>{size}</td>";

                tbody.Append($@"<tr class=""redis-row"" style=""cursor: pointer;"" data-index=""{index}"">{columns}<td><button type=""button"" class=""btn btn-sm btn-outline-danger delete-key"" data-index=""{index}""><span>{Icons.Delete}</span></button></td></tr>");
            }

            var keyPayload = JsonSerializer.Serialize(keys.Select(x => new { name = x.Name, value = x.Value ?? string.Empty }));

            return $@"
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
                <li class=""page-item"" id=""size10""><button type=""button"" class=""page-link"">10</button></li>
                <li class=""page-item"" id=""size20""><button type=""button"" class=""page-link"">20</button></li>
                <li class=""page-item"" id=""size50""><button type=""button"" class=""page-link"">50</button></li>
                <li class=""page-item"" id=""size100""><button type=""button"" class=""page-link"">100</button></li>
                <li class=""page-item"" id=""size500""><button type=""button"" class=""page-link"">500</button></li>
                <li class=""page-item"" id=""size1000""><button type=""button"" class=""page-link"">1000</button></li>
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
                    <code><pre id=""valueContent"" class=""mb-0"">Click on a key to get value...</pre></code>
                </div>
            </div>
        </div>
    </div>

<script>
    document.addEventListener('DOMContentLoaded', function () {{
        const keyData = {keyPayload};
        let currentPage = 0;
        let currentDb = 0;
        let currentKey = '';
        let currentSize = 10;

        const searchParams = new URLSearchParams(window.location.search);
        const paramPage = searchParams.get('page');
        const paramDb = searchParams.get('db');
        const paramKey = searchParams.get('key');
        const paramSize = searchParams.get('size');
        const nextCursor = {next};

        if (paramPage) {{
            currentPage = Number(paramPage);
        }}

        if (paramDb) {{
            currentDb = Number(paramDb);
        }}

        if (paramKey) {{
            currentKey = paramKey;
        }}

        if (paramSize) {{
            currentSize = Number(paramSize);
        }}

        const paginationContainer = document.getElementById('pagination');
        const nextButton = document.createElement('button');
        nextButton.innerText = nextCursor === 0 ? 'Back to top' : 'Next';
        nextButton.className = 'btn btn-outline-success';
        nextButton.id = 'btnNext';
        nextButton.addEventListener('click', function () {{
            window.location = window.buildRedisUiUrl({{ page: nextCursor, db: currentDb, key: currentKey, size: currentSize }});
        }});
        nextButton.hidden = nextCursor === currentPage;
        paginationContainer.replaceChildren(nextButton);

        const searchContainer = document.getElementById('search');
        const searchInput = document.createElement('input');
        searchInput.type = 'text';
        searchInput.name = 'searchInput';
        searchInput.className = 'form-control';
        searchInput.placeholder = 'key or pattern...';
        searchInput.value = currentKey;

        const searchButton = document.createElement('button');
        searchButton.innerText = 'Search';
        searchButton.className = 'btn btn-outline-success btn-sm';
        searchButton.addEventListener('click', function () {{
            window.location = window.buildRedisUiUrl({{
                page: 0,
                db: currentDb,
                size: currentSize,
                key: searchInput.value.trim()
            }});
        }});

        searchInput.addEventListener('keypress', function (event) {{
            if (event.key === 'Enter') {{
                event.preventDefault();
                searchButton.click();
            }}
        }});

        searchContainer.replaceChildren(searchInput, searchButton);

        document.querySelectorAll('#redisTable tbody tr.redis-row').forEach(function (row) {{
            row.addEventListener('click', function () {{
                const index = Number(row.dataset.index);
                const key = keyData[index];
                document.getElementById('valueContent').textContent = key ? key.value : '';
            }});
        }});

        document.querySelectorAll('.delete-key').forEach(function (button) {{
            button.addEventListener('click', function (event) {{
                event.stopPropagation();
                const index = Number(button.dataset.index);
                const key = keyData[index];

                if (!key) {{
                    return;
                }}

                confirmDelete(key.name);
            }});
        }});

        document.querySelectorAll('[id^=""size""] .page-link').forEach(function (button) {{
            button.addEventListener('click', function () {{
                const pageItem = button.parentElement;
                if (!pageItem) {{
                    return;
                }}

                const nextSize = Number(pageItem.id.replace('size', ''));
                setSize(nextSize);
            }});
        }});

        const navElement = document.getElementById('nav' + currentDb);
        if (navElement) {{
            navElement.classList.add('active');
        }}

        const sizeElement = document.getElementById('size' + currentSize);
        if (sizeElement) {{
            sizeElement.classList.add('active');
        }}

        const insertKey = document.getElementById('insertKey');
        const insertValue = document.getElementById('insertValue');
        const saveButton = document.getElementById('btnSave');

        const updateSaveState = function () {{
            saveButton.disabled = !(insertKey.value && insertValue.value);
        }};

        insertKey.addEventListener('input', updateSaveState);
        insertValue.addEventListener('input', updateSaveState);
        saveButton.addEventListener('click', saveKey);
        updateSaveState();

        function confirmDelete(delKey) {{
            if (!window.confirm(""Are you sure to delete key '"" + delKey + ""' ?"")) {{
                return;
            }}

            submitMutation({{ DelKey: delKey }});
        }}

        function saveKey() {{
            submitMutation({{
                InsertKey: insertKey.value,
                InsertValue: insertValue.value
            }});
        }}

        function submitMutation(payload) {{
            fetch(window.buildRedisUiUrl({{
                db: currentDb,
                size: currentSize,
                key: currentKey,
                page: currentPage
            }}), {{
                method: 'POST',
                body: JSON.stringify(payload),
                headers: {{
                    'Content-Type': 'application/json; charset=UTF-8',
                    [window.redisUi.csrfHeaderName]: window.redisUi.csrfToken
                }}
            }}).then(function (response) {{
                if (response.ok) {{
                    window.location = window.buildRedisUiUrl({{
                        db: currentDb,
                        size: currentSize,
                        key: currentKey,
                        page: currentPage
                    }});
                    return;
                }}

                response.text().then(function (message) {{
                    window.alert(message || 'RedisUI request failed.');
                }});
            }});
        }}
    }});
</script>
";
        }
    }
}
