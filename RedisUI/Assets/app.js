(() => {
    'use strict';
    const $ = id => document.getElementById(id);
    const config = JSON.parse($('redis-config').textContent);
    const parameters = new URLSearchParams(location.search);
    const node = (tag, text, className) => {
        const element = document.createElement(tag);
        if (text !== undefined) element.textContent = text;
        if (className) element.className = className;
        return element;
    };
    const url = (path = '', overrides = {}) => {
        const query = new URLSearchParams({ connection: config.connection, db: config.database, ...overrides });
        return config.basePath.replace(/\/$/, '') + path + '?' + query;
    };
    const notify = (message, error = false) => {
        $('notice').textContent = message;
        $('notice').classList.toggle('error', error);
        $('notice').hidden = false;
    };
    async function request(path, overrides, options) {
        const response = await fetch(url(path, overrides), options);
        if (!response.ok) {
            const error = new Error((await response.text()) || 'Request failed. Please try again.');
            error.status = response.status;
            throw error;
        }
        return response.status === 204 ? null : response.json();
    }
    $('keysLink').href = url();
    $('statisticsLink').href = url('/statistics');
    $('connectionSelect').addEventListener('change', event => {
        location.href = url(location.pathname.endsWith('/statistics') ? '/statistics' : '', { connection: event.target.value, db: 0 });
    });
    $('databaseGo').addEventListener('click', () => {
        if ($('databaseInput').reportValidity()) location.href = url('', { db: $('databaseInput').value || 0 });
    });

    if ($('keyWorkspace')) setupKeys();
    if ($('liveMetrics')) setupMetrics();

    function setupKeys() {
        let data = JSON.parse($('key-data').textContent);
        let page = { page: parameters.get('page') || '0', offset: parameters.get('offset') || '0',
            size: parameters.get('size') || String(Math.min(10, config.maxPageSize)), key: parameters.get('key') || '' };
        let pageHistory = [];
        let selected = null;
        let preview = null;
        let previewPage = { offset: 0, cursor: '' };
        let previewHistory = [];
        let listRequest = 0;
        let valueRequest = 0;
        let busy = false;
        let editor = null;
        let pendingDelete = null;
        const storageKey = 'redisui.searches:' + JSON.stringify([location.pathname, config.connection, config.database]);
        $('searchInput').value = page.key;
        for (const size of [...new Set([10, 20, 50, 100, 500, 1000, Number(page.size), config.maxPageSize])].filter(x => x > 0 && x <= config.maxPageSize).sort((a,b) => a-b)) {
            $('pageSize').append(new Option(String(size), String(size)));
        }
        $('pageSize').value = page.size;
        $('addKey').hidden = !config.canEdit;
        $('auditPanel').hidden = !config.canAudit;
        $('searchForm').addEventListener('submit', event => { event.preventDefault(); search($('searchInput').value); });
        $('pageSize').addEventListener('change', () => search($('searchInput').value));
        $('expiringOnly').addEventListener('change', renderKeys);
        $('refreshKeys').addEventListener('click', () => refresh());
        $('nextKeys').addEventListener('click', async () => {
            const previous = { ...page };
            if (await loadKeys({ ...page, page: data.nextCursor, offset: data.nextPageOffset })) { pageHistory.push(previous); renderKeys(); }
        });
        $('previousKeys').addEventListener('click', async () => {
            if (pageHistory.length && await loadKeys(pageHistory[pageHistory.length - 1])) { pageHistory.pop(); renderKeys(); }
        });
        $('redisTable').addEventListener('click', event => {
            const button = event.target.closest('button[data-index]');
            if (!button || busy) return;
            const key = data.keys[Number(button.dataset.index)];
            if (button.classList.contains('delete-key')) confirmDelete({ DelKey: key.name }, 'Delete key ' + key.name + '?');
            else selectKey(key);
        });
        $('nextValue').addEventListener('click', () => {
            if (!preview || !preview.hasMore) return;
            previewHistory.push({ ...previewPage });
            previewPage = { offset: preview.nextOffset, cursor: preview.nextCursor || '' };
            loadPreview();
        });
        $('previousValue').addEventListener('click', () => {
            if (!previewHistory.length) return;
            previewPage = previewHistory.pop(); loadPreview();
        });
        $('addKey').addEventListener('click', () => openEditor());
        $('appendElement').addEventListener('click', () => openEditor({ key: selected.name, type: selected.keyType.toLowerCase() }));
        $('editString').addEventListener('click', () => {
            if (preview) openEditor({ key: selected.name, type: 'string', original: preview.value });
        });
        $('expiryForm').addEventListener('submit', event => {
            event.preventDefault();
            if (selected) mutate({ SetExpiryKey: selected.name, ExpireSeconds: Number($('expiryInput').value) });
        });
        $('persistKey').addEventListener('click', () => { if (selected) mutate({ SetExpiryKey: selected.name, ExpireSeconds: null }); });
        $('refreshAudit').addEventListener('click', loadAudit);
        $('auditPanel').addEventListener('toggle', () => { if ($('auditPanel').open) loadAudit(); });
        $('saveSearch').addEventListener('click', () => {
            const pattern = $('searchInput').value;
            if (!pattern) { notify('Enter a key pattern first.', true); return; }
            saveSearches([...new Set([...readSearches(), pattern])].slice(-50));
        });
        $('savedSearches').addEventListener('change', event => { if (event.target.value) search(event.target.value); });
        $('removeSearch').addEventListener('click', () => saveSearches(readSearches().filter(x => x !== $('savedSearches').value)));
        $('closeEditor').addEventListener('click', () => { if (!busy) $('editorDialog').close(); });
        $('editorDialog').addEventListener('cancel', event => { if (busy) event.preventDefault(); });
        $('cancelDelete').addEventListener('click', () => $('deleteDialog').close());
        $('confirmDelete').addEventListener('click', async () => { if (pendingDelete && await mutate(pendingDelete)) $('deleteDialog').close(); });
        $('insertType').addEventListener('change', editorLayout);
        $('expiryMode').addEventListener('change', editorLayout);
        $('insertValue').addEventListener('input', validateEditor);
        $('validateJson').addEventListener('change', validateEditor);
        $('formatJson').addEventListener('click', () => {
            try { $('insertValue').value = JSON.stringify(JSON.parse($('insertValue').value), null, 2); $('validateJson').checked = true; validateEditor(); }
            catch { $('jsonStatus').textContent = 'Invalid JSON: correct the value before formatting.'; }
        });
        $('editorForm').addEventListener('submit', async event => {
            event.preventDefault();
            if (!validateEditor()) return;
            const payload = editor.element ? {
                ElementKey: editor.key, ElementType: editor.type, ElementAction: 'update', ElementSelector: editor.selector,
                ElementValue: $('insertValue').value, ExpectedValue: editor.original, ElementScore: Number($('insertScore').value || 0)
            } : {
                InsertKey: $('insertKey').value, InsertType: $('insertType').value, InsertField: $('insertField').value,
                InsertValue: $('insertValue').value, InsertScore: $('insertScore').value,
                ExpiryMode: $('expiryMode').value, ValidateJson: $('validateJson').checked,
                ...($('expiryMode').value === 'set' ? { InsertTTLSeconds: Number($('insertTTL').value) } : {}),
                ...(editor.original !== undefined && editor.type === 'string' ? { ExpectedValue: editor.original } : {})
            };
            if (await mutate(payload)) $('editorDialog').close();
        });
        renderSearches(); renderKeys();

        async function search(pattern) {
            if (await loadKeys({ page: '0', offset: 0, size: $('pageSize').value, key: pattern })) {
                pageHistory = []; $('searchInput').value = pattern; clearPreview(); renderKeys();
            }
        }
        async function loadKeys(next = page) {
            const id = ++listRequest;
            $('pageStatus').textContent = 'Loading…';
            try {
                const result = await request('/keys', next);
                if (id !== listRequest) return false;
                data = result; page = { ...next };
                if (result.databaseSize != null) $('databaseSize').textContent = result.databaseSize;
                history.replaceState(null, '', url('', page));
                renderKeys(); return true;
            } catch (error) {
                if (id === listRequest) {
                    if (error.status === 410 && Number(next.offset) > 0) {
                        pageHistory = []; clearPreview();
                        if (await loadKeys({ ...next, page: '0', offset: 0 }))
                            notify('The previous key page expired. Search restarted from the first page.');
                    } else { notify(error.message, true); $('pageStatus').textContent = 'Could not load keys'; }
                }
                return false;
            }
        }
        async function refresh() {
            if (await loadKeys() && selected) await loadPreview();
        }
        function renderKeys() {
            const body = $('redisTable').querySelector('tbody'); body.replaceChildren();
            let visible = 0;
            data.keys.forEach((key, index) => {
                if ($('expiringOnly').checked && key.ttlSeconds == null) return;
                visible++;
                const row = node('tr', undefined, 'redis-row' + (selected?.name === key.name ? ' selected' : ''));
                row.append(node('td', key.keyType));
                const keyCell = node('td'); const select = node('button', key.name, 'key-select'); select.dataset.index = index; keyCell.append(select); row.append(keyCell);
                row.append(node('td', key.valueSizeBytes), node('td', key.ttlSeconds == null ? '∞' : key.ttlSeconds + 's'));
                const actions = node('td');
                if (config.canDelete) { const remove = node('button', 'Delete', 'delete-key danger'); remove.dataset.index = index; remove.disabled = busy; actions.append(remove); }
                row.append(actions); body.append(row);
            });
            if (!visible) { const row = node('tr'); const cell = node('td', 'No matching keys on this page. Continue scanning if Next is available.'); cell.colSpan = 5; row.append(cell); body.append(row); }
            $('pageStatus').textContent = visible + ' shown · ' + data.keys.length + ' loaded';
            $('previousKeys').disabled = busy || !pageHistory.length;
            $('nextKeys').disabled = busy || (String(data.nextCursor) === '0' && !data.nextPageOffset);
            renderTree();
        }
        function renderTree() {
            const root = new Map();
            for (const key of data.keys) {
                let branch = root; let prefix = '';
                const parts = key.name.split(':').slice(0, -1).slice(0, 16);
                for (const part of parts) {
                    prefix += part + ':';
                    if (!branch.has(part)) branch.set(part, { prefix, count: 0, children: new Map() });
                    const item = branch.get(part); item.count++; branch = item.children;
                }
            }
            function append(parent, branch) {
                for (const [name, item] of branch) {
                    const details = node('details'); const summary = node('summary', name + ' (' + item.count + ')'); details.append(summary);
                    const button = node('button', 'Search ' + item.prefix + '*');
                    button.addEventListener('click', () => search(item.prefix.replace(/[\\*?\[\]]/g, '\\$&') + '*'));
                    details.append(button); append(details, item.children); parent.append(details);
                }
            }
            $('keyTree').replaceChildren(); append($('keyTree'), root);
            if (!root.size) $('keyTree').textContent = 'No grouped keys on this page.';
        }
        function clearPreview() {
            valueRequest++; selected = null; preview = null; previewHistory = [];
            $('valueTitle').textContent = 'Value inspector'; $('valueMeta').textContent = 'Select a key.'; $('valueContent').textContent = '';
            $('elementRows').replaceChildren();
            for (const id of ['editString', 'appendElement', 'previousValue', 'nextValue', 'expiryForm']) $(id).hidden = true;
        }
        function selectKey(key) { clearPreview(); selected = key; previewPage = { offset: 0, cursor: '' }; renderKeys(); loadPreview(); }
        async function loadPreview() {
            if (!selected) return;
            const id = ++valueRequest; preview = null;
            $('valueTitle').textContent = selected.name; $('valueContent').textContent = 'Loading…'; $('elementRows').replaceChildren();
            for (const control of ['editString', 'appendElement', 'expiryForm', 'previousValue', 'nextValue']) $(control).hidden = true;
            try {
                const result = await request('/value', { key: selected.name, ...previewPage,
                    count: selected.keyType.toLowerCase() === 'string' ? config.stringPreviewCount : config.collectionPreviewCount });
                if (id !== valueRequest) return;
                preview = result; renderPreview();
            } catch (error) { if (id === valueRequest) { $('valueContent').textContent = error.message; $('valueMeta').textContent = 'Preview unavailable'; } }
        }
        function renderPreview() {
            const p = preview; const type = p.keyType.toLowerCase();
            selected.keyType = p.keyType;
            let content = p.viewerFormat === 'binary' ? p.base64Value : p.value;
            if (p.viewerFormat === 'json') { try { content = JSON.stringify(JSON.parse(p.value), null, 2); } catch { /* retain text */ } }
            $('valueContent').textContent = content || '(empty)';
            $('valueMeta').textContent = p.valueSizeBytes + ' bytes · TTL: ' + (p.ttlSeconds == null ? 'persistent' : p.ttlSeconds + 's') +
                ' · ' + p.offset + '–' + p.nextOffset + ' / ' + p.totalItems + ' ' + p.pagingUnit + (p.viewerFormat === 'binary' ? ' · Base64 (read only)' : '');
            $('previousValue').hidden = !previewHistory.length; $('nextValue').hidden = !p.hasMore;
            const completeText = type === 'string' && p.offset === 0 && !p.hasMore && p.viewerFormat !== 'binary';
            $('editString').hidden = !config.canEdit || !config.canEditElements || !completeText;
            if (type === 'string' && !completeText) $('valueMeta').textContent += ' · Partial or binary preview cannot be edited';
            $('appendElement').hidden = !config.canEdit || type === 'string';
            $('expiryForm').hidden = !config.canEdit;
            $('expiryInput').value = p.ttlSeconds ?? '';
            if (type !== 'string' && config.canEditElements) renderElements(type, p);
        }
        function renderElements(type, p) {
            let values; try { values = JSON.parse(p.value); } catch { return; }
            if (!Array.isArray(values)) return;
            const table = node('table'); const body = node('tbody'); table.append(body);
            values.forEach((item, index) => {
                let selector, value, score = 0;
                if (type === 'hash') { selector = item.field; value = item.value; }
                else if (type === 'sortedset') { selector = item.member; value = item.member; score = item.score; }
                else if (type === 'stream') { selector = item.id; value = JSON.stringify(item.fields); }
                else { selector = type === 'list' ? String(p.offset + index) : item; value = item; }
                // Binary elements stay view-only; passing their display objects would corrupt data.
                if (typeof selector !== 'string' || typeof value !== 'string') return;
                const row = node('tr'); row.append(node('td', type === 'list' ? 'Index ' + selector : selector));
                const actions = node('td', undefined, 'element-actions');
                if (config.canEdit && type !== 'stream') {
                    const edit = node('button', 'Edit'); edit.disabled = busy;
                    edit.addEventListener('click', () => openEditor({ element: true, key: selected.name, type, selector, original: value, score })); actions.append(edit);
                }
                if (config.canDelete) {
                    const remove = node('button', 'Delete', 'danger'); remove.disabled = busy;
                    remove.addEventListener('click', () => confirmDelete({ ElementKey: selected.name, ElementType: type, ElementAction: 'delete', ElementSelector: selector, ExpectedValue: value }, 'Delete element ' + selector + ' from ' + selected.name + '?'));
                    actions.append(remove);
                }
                row.append(actions); body.append(row);
            });
            $('elementRows').replaceChildren(table);
        }
        function openEditor(options = {}) {
            editor = { type: 'string', ...options };
            $('editorForm').reset(); $('editorError').textContent = '';
            $('editorTitle').textContent = editor.element ? 'Edit collection element' : editor.original !== undefined ? 'Edit value' : 'Add key / element';
            $('insertKey').value = editor.key || ''; $('insertKey').readOnly = !!editor.key;
            $('insertType').value = editor.type; $('insertType').disabled = !!editor.key;
            $('insertValue').value = editor.original ?? ''; $('insertScore').value = editor.score ?? '';
            $('elementSelector').value = editor.selector ?? '';
            $('beforeValue').textContent = editor.original ?? '';
            $('diffPanel').hidden = editor.original === undefined;
            $('diffPanel').open = editor.original !== undefined;
            editorLayout(); $('editorDialog').showModal();
        }
        function editorLayout() {
            const type = $('insertType').value;
            $('fieldGroup').hidden = type !== 'hash' || editor.element;
            $('insertField').required = !$('fieldGroup').hidden;
            $('scoreGroup').hidden = type !== 'sortedset'; $('insertScore').required = type === 'sortedset';
            $('selectorGroup').hidden = !editor.element;
            $('expiryModeGroup').hidden = !!editor.element;
            $('ttlGroup').hidden = !!editor.element || $('expiryMode').value !== 'set';
            $('insertTTL').required = !$('ttlGroup').hidden;
            if (type === 'stream') $('validateJson').checked = true;
            validateEditor();
        }
        function validateEditor() {
            let valid = true;
            if ($('validateJson').checked) {
                try { JSON.parse($('insertValue').value); $('jsonStatus').textContent = 'Valid JSON'; }
                catch { valid = false; $('jsonStatus').textContent = 'Invalid JSON. Save is disabled until corrected.'; }
            } else $('jsonStatus').textContent = 'Plain text mode';
            $('afterValue').textContent = $('insertValue').value;
            const changed = editor?.original !== $('insertValue').value;
            $('afterValue').classList.toggle('changed', changed);
            $('diffStatus').textContent = changed ? 'Value differs from the original.' : 'No value changes.';
            $('saveKey').disabled = busy || !valid;
            return valid;
        }
        function confirmDelete(payload, message) {
            pendingDelete = payload; $('deleteMessage').textContent = message; $('deleteDialog').showModal();
        }
        async function mutate(payload) {
            if (busy) return false;
            busy = true;
            const controls = [...document.querySelectorAll('button, input, select, textarea')];
            const previous = controls.map(control => control.disabled);
            controls.forEach(control => { control.disabled = true; });
            $('editorError').textContent = '';
            let written = false;
            try {
                await request('', {}, { method: 'POST', headers: { 'Content-Type': 'application/json', [config.csrfHeaderName]: config.csrfToken }, body: JSON.stringify(payload) });
                written = true; notify('Operation completed.');
                previewPage = { offset: 0, cursor: '' }; previewHistory = [];
                if (payload.DelKey === selected?.name) clearPreview();
                await refresh();
                if ($('auditPanel').open) await loadAudit();
            } catch (error) { notify(error.message, true); $('editorError').textContent = error.message; }
            finally { busy = false; controls.forEach((control, index) => { control.disabled = previous[index]; }); renderKeys(); if (preview) renderPreview(); if (editor) validateEditor(); }
            return written;
        }
        async function loadAudit() {
            if (!config.canAudit) return;
            try {
                const entries = await request('/audit'); $('auditRows').replaceChildren();
                for (const entry of entries) {
                    const row = node('tr');
                    for (const value of [new Date(entry.timestamp).toLocaleString(), entry.user, entry.operation, entry.key, entry.succeeded ? 'Succeeded' : 'Failed']) row.append(node('td', value));
                    $('auditRows').append(row);
                }
                if (!entries.length) { const row = node('tr'); const cell = node('td', 'No operations recorded.'); cell.colSpan = 5; row.append(cell); $('auditRows').append(row); }
            } catch (error) { notify(error.message, true); }
        }
        function readSearches() {
            try { const value = JSON.parse(localStorage.getItem(storageKey) || '[]'); return Array.isArray(value) ? value.filter(x => typeof x === 'string').slice(-50) : []; }
            catch { return []; }
        }
        function saveSearches(value) {
            try { localStorage.setItem(storageKey, JSON.stringify(value)); renderSearches(); }
            catch { notify('Browser storage is unavailable. Search could not be saved.', true); }
        }
        function renderSearches() {
            $('savedSearches').replaceChildren(new Option('Choose a search', ''));
            for (const value of readSearches()) $('savedSearches').append(new Option(value, value));
        }
    }

    function setupMetrics() {
        let samples = []; let timer; let previous = null;
        const charts = [
            ['memory', 'Memory (MiB)', sample => sample.memoryBytes == null ? null : sample.memoryBytes / 1048576],
            ['ops', 'Operations / second', sample => sample.operationsPerSecond],
            ['hit', 'Cache hit rate (%)', sample => sample.hitRate],
            ['evictions', 'Evictions / second', sample => sample.evictionRate]
        ];
        const cards = node('div', undefined, 'metrics');
        for (const [id, title] of charts) {
            const card = node('div', undefined, 'panel'); card.append(node('h3', title));
            const value = node('div', '—', 'metric-value'); value.id = 'metric-' + id; card.append(value);
            const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.id = 'chart-' + id; svg.setAttribute('viewBox', '0 0 300 85'); svg.setAttribute('class', 'metric-chart'); svg.setAttribute('role', 'img'); svg.setAttribute('aria-label', title + ' recent trend');
            card.append(svg); cards.append(card);
        }
        $('liveMetrics').append(cards);
        $('metricsPause').addEventListener('change', () => { clearTimeout(timer); if (!$('metricsPause').checked) poll(); });
        document.addEventListener('visibilitychange', () => { clearTimeout(timer); if (!document.hidden && !$('metricsPause').checked) poll(); });
        let inFlight = false;
        async function poll() {
            if (inFlight || document.hidden || $('metricsPause').checked) return;
            inFlight = true;
            try {
                const sample = await request('/metrics');
                const seconds = previous ? (new Date(sample.timestamp) - new Date(previous.timestamp)) / 1000 : 0;
                const hits = previous && sample.hits != null && previous.hits != null ? sample.hits - previous.hits : -1;
                const misses = previous && sample.misses != null && previous.misses != null ? sample.misses - previous.misses : -1;
                sample.hitRate = hits >= 0 && misses >= 0 && hits + misses > 0 ? hits * 100 / (hits + misses) : null;
                sample.evictionRate = seconds > 0 && sample.evictedKeys != null && previous.evictedKeys != null && sample.evictedKeys >= previous.evictedKeys ? (sample.evictedKeys - previous.evictedKeys) / seconds : null;
                previous = sample; samples.push(sample); samples = samples.slice(-60);
                for (const [id, , get] of charts) {
                    const value = get(sample); $('metric-' + id).textContent = value == null ? '—' : value.toLocaleString(undefined, { maximumFractionDigits: 1 });
                    const values = samples.map(get); const max = Math.max(1, ...values.filter(x => x != null));
                    const svg = $('chart-' + id); svg.replaceChildren();
                    // Separate segments at missing samples rather than drawing invented zeroes.
                    let points = [];
                    const flush = () => {
                        if (!points.length) return;
                        const line = document.createElementNS('http://www.w3.org/2000/svg', 'polyline');
                        line.setAttribute('points', points.join(' ')); line.setAttribute('fill', 'none'); line.setAttribute('stroke', '#176f5b'); line.setAttribute('stroke-width', '2'); svg.append(line); points = [];
                    };
                    values.forEach((v, i) => { if (v == null) flush(); else points.push((i * 300 / Math.max(1, values.length - 1)) + ',' + (80 - v / max * 75)); }); flush();
                }
                $('metricsStatus').textContent = 'Updated ' + new Date(sample.timestamp).toLocaleTimeString() + ' · last ' + samples.length + ' samples · 5 second interval';
            } catch (error) { $('metricsStatus').textContent = 'Live update failed: ' + error.message; previous = null; }
            finally { inFlight = false; if (!document.hidden && !$('metricsPause').checked) timer = setTimeout(poll, 5000); }
        }
        poll();
    }
})();
