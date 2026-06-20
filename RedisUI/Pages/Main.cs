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
        public static string Build(System.Collections.Generic.List<KeyModel> keys, long next, int nextPageOffset, RedisUISettings settings)
        {
            var encoder = HtmlEncoder.Default;
            var tbody = new StringBuilder();
            var stringPreviewCount = System.Math.Max(1, settings.StringPreviewBytes);
            var collectionPreviewCount = System.Math.Max(1, settings.ValuePreviewPageSize);

            for (var index = 0; index < keys.Count; index++)
            {
                var key = keys[index];
                var size = key.ValueSizeBytes.ToKilobytes();
                var ttlText = key.TTLSeconds.HasValue ? key.TTLSeconds.Value + "s" : "∞";
                var columns = $@"<td><span class=""badge text-bg-{encoder.Encode(key.Badge)}"">{encoder.Encode(key.KeyType)}</span></td><td>{encoder.Encode(key.Name)}</td><td>{size}</td><td class=""small text-muted"">{ttlText}</td>";

                tbody.Append($@"<tr class=""redis-row"" style=""cursor: pointer;"" data-index=""{index}"">{columns}<td><button type=""button"" class=""btn btn-sm btn-outline-danger delete-key"" data-index=""{index}""><span>{Icons.Delete}</span></button></td></tr>");
            }

            var keyPayload = JsonSerializer.Serialize(keys.Select(x => new
            {
                name = x.Name,
                keyType = x.KeyType,
                valueSizeBytes = x.ValueSizeBytes,
                ttlSeconds = x.TTLSeconds
            }));

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
                            <th scope=""col"">TTL</th>
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
                <div class=""card-header d-flex justify-content-between align-items-center"">
                    <span>Value</span>
                    <span class=""small text-muted"" id=""valueMeta"">Click on a key to inspect its value.</span>
                </div>
                <div class=""card-body"">
                    <div class=""d-flex gap-2 mb-2 align-items-center d-none"" id=""previewPager"">
                        <button type=""button"" class=""btn btn-sm btn-outline-secondary"" id=""prevValuePage"">Previous</button>
                        <button type=""button"" class=""btn btn-sm btn-outline-secondary"" id=""nextValuePage"">Next</button>
                        <span class=""small text-muted"" id=""previewPageMeta""></span>
                    </div>
                    <pre id=""valueContent"" class=""mb-0"">Click on a key to get value...</pre>
                </div>
                <div class=""card-footer d-flex align-items-center gap-2 flex-wrap d-none"" id=""expirySection"">
                    <span class=""small text-muted"">Expiry:</span>
                    <input type=""number"" class=""form-control form-control-sm"" id=""expiryInput"" placeholder=""seconds"" min=""1"" style=""width:110px"">
                    <button type=""button"" class=""btn btn-sm btn-outline-warning"" id=""btnSetExpiry"">Set</button>
                    <button type=""button"" class=""btn btn-sm btn-outline-secondary"" id=""btnPersist"">Persist</button>
                </div>
            </div>
        </div>
    </div>

<script>
    document.addEventListener('DOMContentLoaded', function () {{
        const keyData = {keyPayload};
        const stringPreviewCount = {stringPreviewCount};
        const collectionPreviewCount = {collectionPreviewCount};
        let currentPage = 0;
        let currentDb = 0;
        let currentKey = '';
        let currentSize = 10;
        let currentOffset = 0;
        let selectedIndex = null;
        let previewOffset = 0;
        let previewCursor = '';
        let previewHistory = [];
        let currentPreview = null;
        let previewRequestId = 0;
        let filterExpiring = false;

        const searchParams = new URLSearchParams(window.location.search);
        const paramPage = searchParams.get('page');
        const paramDb = searchParams.get('db');
        const paramKey = searchParams.get('key');
        const paramSize = searchParams.get('size');
        const paramOffset = searchParams.get('offset');
        const nextCursor = {next};
        const nextOffset = {nextPageOffset};
        const valueContent = document.getElementById('valueContent');
        const valueMeta = document.getElementById('valueMeta');
        const previewPager = document.getElementById('previewPager');
        const prevValuePageButton = document.getElementById('prevValuePage');
        const nextValuePageButton = document.getElementById('nextValuePage');
        const previewPageMeta = document.getElementById('previewPageMeta');

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

        if (paramOffset) {{
            currentOffset = Number(paramOffset);
        }}

        const paginationContainer = document.getElementById('pagination');
        const nextButton = document.createElement('button');
        nextButton.innerText = nextCursor === 0 && nextOffset === 0 ? 'Back to top' : 'Next';
        nextButton.className = 'btn btn-outline-success';
        nextButton.id = 'btnNext';
        nextButton.addEventListener('click', function () {{
            window.location = window.buildRedisUiUrl({{ page: nextCursor, offset: nextOffset, db: currentDb, key: currentKey, size: currentSize }});
        }});
        nextButton.hidden = nextCursor === currentPage && nextOffset === currentOffset;
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

        const filterButton = document.createElement('button');
        filterButton.innerText = 'Expiring';
        filterButton.className = 'btn btn-outline-warning btn-sm';
        filterButton.title = 'Show only keys with a TTL set';
        filterButton.addEventListener('click', function () {{
            filterExpiring = !filterExpiring;
            filterButton.classList.toggle('active', filterExpiring);
            applyExpiringFilter();
        }});

        searchContainer.replaceChildren(searchInput, searchButton, filterButton);

        const expirySection = document.getElementById('expirySection');
        const expiryInput = document.getElementById('expiryInput');

        document.getElementById('btnSetExpiry').addEventListener('click', function () {{
            if (selectedIndex === null) {{ return; }}
            const key = keyData[selectedIndex];
            const seconds = parseInt(expiryInput.value, 10);
            if (!seconds || seconds < 1) {{
                window.alert('Enter a TTL of at least 1 second.');
                return;
            }}
            submitMutation({{ SetExpiryKey: key.name, ExpireSeconds: seconds }});
        }});

        document.getElementById('btnPersist').addEventListener('click', function () {{
            if (selectedIndex === null) {{ return; }}
            const key = keyData[selectedIndex];
            if (!window.confirm(""Remove expiry from '"" + key.name + ""'?"")) {{ return; }}
            submitMutation({{ SetExpiryKey: key.name }});
        }});

        document.querySelectorAll('#redisTable tbody tr.redis-row').forEach(function (row) {{
            row.addEventListener('click', function () {{
                selectedIndex = Number(row.dataset.index);
                previewOffset = 0;
                previewCursor = '';
                previewHistory = [];
                currentPreview = null;
                loadSelectedValue();
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

        nextValuePageButton.addEventListener('click', function () {{
            if (!currentPreview || !currentPreview.hasMore) {{
                return;
            }}

            previewHistory.push({{ offset: previewOffset, cursor: previewCursor }});
            if (currentPreview.pageMode === 'cursor') {{
                previewCursor = currentPreview.nextCursor || '';
                previewOffset = currentPreview.nextOffset || previewOffset;
            }} else {{
                previewCursor = '';
                previewOffset = currentPreview.nextOffset || previewOffset;
            }}

            loadSelectedValue();
        }});

        prevValuePageButton.addEventListener('click', function () {{
            const previous = previewHistory.pop();
            if (!previous) {{
                return;
            }}

            previewOffset = previous.offset || 0;
            previewCursor = previous.cursor || '';
            loadSelectedValue();
        }});

        const navElement = document.getElementById('nav' + currentDb);
        if (navElement) {{
            navElement.classList.add('active');
        }}

        const sizeElement = document.getElementById('size' + currentSize);
        if (sizeElement) {{
            sizeElement.classList.add('active');
        }}

        const insertType = document.getElementById('insertType');
        const insertKey = document.getElementById('insertKey');
        const insertField = document.getElementById('insertField');
        const insertScore = document.getElementById('insertScore');
        const insertValue = document.getElementById('insertValue');
        const insertTTL = document.getElementById('insertTTL');
        const saveButton = document.getElementById('btnSave');
        const fieldGroup = document.getElementById('fieldGroup');
        const scoreGroup = document.getElementById('scoreGroup');

        const valuePlaceholders = {{
            string: 'Value',
            list: 'Element to append',
            set: 'Member to add',
            hash: 'Value',
            sortedset: 'Member',
            stream: 'Fields as JSON object, e.g. {{""field1"":""val1"",""field2"":""val2""}}'
        }};

        function applyTypeLayout() {{
            const type = insertType.value;
            fieldGroup.style.display = type === 'hash' ? '' : 'none';
            scoreGroup.style.display = type === 'sortedset' ? '' : 'none';
            insertValue.placeholder = valuePlaceholders[type] || 'Value';
            updateSaveState();
        }}

        const updateSaveState = function () {{
            const type = insertType.value;
            const fieldOk = type !== 'hash' || insertField.value.trim().length > 0;
            const scoreOk = type !== 'sortedset' || (insertScore.value.trim().length > 0 && !isNaN(Number(insertScore.value)));
            saveButton.disabled = !(insertKey.value.trim() && insertValue.value.trim() && fieldOk && scoreOk);
        }};

        insertType.addEventListener('change', applyTypeLayout);
        insertKey.addEventListener('input', updateSaveState);
        insertField.addEventListener('input', updateSaveState);
        insertScore.addEventListener('input', updateSaveState);
        insertValue.addEventListener('input', updateSaveState);
        saveButton.addEventListener('click', saveKey);
        updateSaveState();

        function loadSelectedValue() {{
            if (selectedIndex === null) {{
                return;
            }}

            const key = keyData[selectedIndex];
            if (!key) {{
                return;
            }}

            const requestId = ++previewRequestId;
            valueContent.textContent = 'Loading preview...';
            valueMeta.textContent = buildBaseMeta(key) + ' | loading';
            setVisible(previewPager, false);
            setVisible(expirySection, true);
            expiryInput.value = (key.ttlSeconds !== null && key.ttlSeconds !== undefined) ? key.ttlSeconds : '';

            fetch(buildValuePreviewUrl(key))
                .then(function (response) {{
                    if (requestId !== previewRequestId) {{
                        return;
                    }}

                    if (response.ok) {{
                        response.json().then(function (preview) {{
                            if (requestId === previewRequestId) {{
                                renderSelectedValue(preview);
                            }}
                        }});
                        return;
                    }}

                    response.text().then(function (message) {{
                        if (requestId === previewRequestId) {{
                            renderPreviewError(message || 'RedisUI preview request failed.');
                        }}
                    }});
                }})
                .catch(function () {{
                    if (requestId === previewRequestId) {{
                        renderPreviewError('RedisUI preview request failed.');
                    }}
                }});
        }}

        function renderSelectedValue(preview) {{
            currentPreview = preview;
            let content = '';
            let meta = preview.valueSizeBytes + ' bytes';

            if (preview.viewerFormat === 'binary') {{
                content = preview.base64Value || '';
                meta = meta + ' | binary-safe base64 preview';
            }} else if (preview.viewerFormat === 'json') {{
                content = prettyPrintJson(preview.value);
                meta = meta + ' | JSON preview';
            }} else {{
                content = preview.value || '';
            }}

            if (preview.ttlSeconds !== null && preview.ttlSeconds !== undefined) {{
                meta = meta + ' | TTL: ' + preview.ttlSeconds + 's';
            }}

            if (preview.totalItems > 0) {{
                const start = preview.nextOffset > preview.offset ? preview.offset + 1 : 0;
                meta = meta + ' | ' + preview.pagingUnit + ': ' + start + '-' + preview.nextOffset + ' of ' + preview.totalItems;
            }}

            valueContent.textContent = content || '(empty)';
            valueMeta.textContent = meta;
            setVisible(previewPager, previewHistory.length > 0 || preview.hasMore);
            prevValuePageButton.hidden = previewHistory.length === 0;
            nextValuePageButton.hidden = !preview.hasMore;
            previewPageMeta.textContent = preview.hasMore ? 'More data available' : 'End of value';

            setVisible(expirySection, true);
            expiryInput.value = (preview.ttlSeconds !== null && preview.ttlSeconds !== undefined) ? preview.ttlSeconds : '';
        }}

        function renderPreviewError(message) {{
            currentPreview = null;
            valueContent.textContent = message;
            valueMeta.textContent = 'Preview failed';
            setVisible(previewPager, false);
            setVisible(expirySection, false);
        }}

        function setVisible(element, visible) {{
            element.classList.toggle('d-none', !visible);
        }}

        function buildValuePreviewUrl(key) {{
            const query = new URLSearchParams();
            query.set('db', String(currentDb));
            query.set('key', key.name);
            query.set('offset', String(previewOffset));
            query.set('count', String(getPreviewCount(key)));

            if (previewCursor) {{
                query.set('cursor', previewCursor);
            }}

            return window.redisUi.basePath + '/value?' + query.toString();
        }}

        function getPreviewCount(key) {{
            return String(key.keyType || '').toLowerCase() === 'string'
                ? stringPreviewCount
                : collectionPreviewCount;
        }}

        function buildBaseMeta(key) {{
            let meta = key.valueSizeBytes + ' bytes';
            if (key.ttlSeconds !== null && key.ttlSeconds !== undefined) {{
                meta = meta + ' | TTL: ' + key.ttlSeconds + 's';
            }}

            return meta;
        }}

        function applyExpiringFilter() {{
            document.querySelectorAll('#redisTable tbody tr.redis-row').forEach(function (row) {{
                const key = keyData[Number(row.dataset.index)];
                const hasExpiry = key && key.ttlSeconds !== null && key.ttlSeconds !== undefined;
                row.style.display = (filterExpiring && !hasExpiry) ? 'none' : '';
            }});
        }}

        function prettyPrintJson(value) {{
            try {{
                return JSON.stringify(JSON.parse(value), null, 2);
            }} catch (_error) {{
                return value;
            }}
        }}

        function confirmDelete(delKey) {{
            if (!window.confirm(""Are you sure to delete key '"" + delKey + ""' ?"")) {{
                return;
            }}

            submitMutation({{ DelKey: delKey }});
        }}

        function saveKey() {{
            const type = insertType.value;
            const payload = {{
                InsertKey: insertKey.value,
                InsertType: type,
                InsertValue: insertValue.value
            }};
            if (type === 'hash') {{
                payload.InsertField = insertField.value;
            }}
            if (type === 'sortedset') {{
                payload.InsertScore = insertScore.value;
            }}
            const ttlVal = parseInt(insertTTL.value, 10);
            if (ttlVal > 0) {{
                payload.InsertTTLSeconds = ttlVal;
            }}
            submitMutation(payload);
        }}

        function submitMutation(payload) {{
            fetch(window.buildRedisUiUrl({{
                db: currentDb,
                size: currentSize,
                key: currentKey,
                page: currentPage,
                offset: currentOffset
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
                        page: currentPage,
                        offset: currentOffset
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
