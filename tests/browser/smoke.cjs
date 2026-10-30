const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
    const fixture = process.env.REDISUI_BROWSER_FIXTURE || path.resolve('browser-fixture.html');
    const html = fs.readFileSync(fixture, 'utf8');
    const statistics = fs.readFileSync(fixture + '.statistics.html', 'utf8');
    const browser = await chromium.launch({ headless: true, ...(process.env.REDISUI_BROWSER_EXECUTABLE ? { executablePath: process.env.REDISUI_BROWSER_EXECUTABLE } : {}) });
    let failures = [];
    try {
        const context = await browser.newContext({ viewport: { width: 1500, height: 950 } });
        const page = await context.newPage();
        page.on('pageerror', error => failures.push(error.message));
        const mutations = [];
        let documentRequests = 0;
        let allowEdit = true;
        let requests = [];
        let values = { 'users:1': '{"name":"Ada"}', 'users:profile:2': 'profile', 'large:string': 'preview', 'binary:value': '' };
        let hash = 'before';
        let metricsCount = 0;
        let rejectNextWrite = false;
        let expireNextPage = false;
        await context.route('**/*', async route => {
            const request = route.request();
            const url = new URL(request.url());
            if (url.hostname !== 'redisui.test') return route.fulfill({ status: 200, body: '' });
            requests.push(url);
            if (request.isNavigationRequest()) {
                documentRequests++;
                let body = url.pathname.endsWith('/statistics') ? statistics : html;
                if (!allowEdit) body = body.replace('"canEdit":true', '"canEdit":false').replace('"canDelete":true', '"canDelete":false');
                return route.fulfill({ contentType: 'text/html', body });
            }
            if (request.method() === 'POST') {
                const payload = request.postDataJSON(); mutations.push(payload);
                assert.ok(request.headers()['x-redisui-csrf']);
                if (rejectNextWrite) { rejectNextWrite = false; return route.fulfill({ status: 409, body: 'The value changed. Refresh before editing again.' }); }
                if (payload.InsertKey) values[payload.InsertKey] = payload.InsertValue;
                if (payload.DelKey) delete values[payload.DelKey];
                if (payload.ElementKey) hash = payload.ElementValue;
                return route.fulfill({ status: 204 });
            }
            let result;
            switch (url.pathname) {
                case '/redis/keys': {
                    if (expireNextPage && url.searchParams.get('offset') === '123') {
                        expireNextPage = false;
                        return route.fulfill({ status: 410, body: 'This key page has expired.' });
                    }
                    let keys = Object.keys(values).map(name => ({ name, keyType: 'String', valueSizeBytes: values[name].length, ttlSeconds: name === 'users:1' ? 60 : null }));
                    keys.push({ name: 'users:hash', keyType: 'Hash', valueSizeBytes: 50, ttlSeconds: null });
                    if (url.searchParams.get('key') === 'users:*') keys = keys.filter(x => x.name.startsWith('users:'));
                    result = { keys, nextCursor: '0', nextPageOffset: 0, databaseSize: keys.length }; break;
                }
                case '/redis/value': {
                    const name = url.searchParams.get('key');
                    result = { name, keyType: name === 'users:hash' ? 'Hash' : 'String', value: name === 'users:hash' ? JSON.stringify([{ field: 'field', value: hash }]) : values[name],
                        viewerFormat: name === 'binary:value' ? 'binary' : name === 'users:hash' ? 'json' : 'text', base64Value: 'AAEC', valueSizeBytes: 20, ttlSeconds: 60,
                        offset: 0, nextOffset: 20, totalItems: 20, pagingUnit: 'bytes', hasMore: name === 'large:string', nextCursor: '', pageMode: 'offset' };
                    break;
                }
                case '/redis/audit': result = [{ timestamp: new Date().toISOString(), user: 'tester', operation: 'Write string', key: 'users:1', succeeded: true }]; break;
                case '/redis/metrics': metricsCount++; result = { timestamp: new Date(Date.UTC(2026, 9, 30) + metricsCount * 5000).toISOString(), memoryBytes: 1048576, operationsPerSecond: 5, hits: 10 * metricsCount, misses: metricsCount, evictedKeys: 0 }; break;
                default: throw new Error('Unexpected request ' + url);
            }
            await route.fulfill({ contentType: 'application/json', body: JSON.stringify(result) });
        });
        await page.goto('http://redisui.test/redis');
        await page.locator('#refreshKeys').click();
        await page.getByRole('button', { name: 'users:1', exact: true }).click();
        await page.locator('#editString').waitFor({ state: 'visible' });
        await page.locator('#editString').click();
        assert.equal(await page.locator('#expiryMode').inputValue(), 'keep');
        await page.locator('#validateJson').check();
        await page.locator('#insertValue').fill('{broken');
        assert.equal(await page.locator('#saveKey').isDisabled(), true);
        await page.locator('#insertValue').fill('{"name":"Grace"}');
        await page.locator('#formatJson').click();
        assert.match(await page.locator('#beforeValue').textContent(), /Ada/);
        assert.match(await page.locator('#afterValue').textContent(), /Grace/);
        await page.locator('#saveKey').click();
        await page.locator('#editorDialog').waitFor({ state: 'hidden' });
        assert.equal(mutations[0].ExpiryMode, 'keep');
        assert.equal(mutations[0].ExpectedValue, '{"name":"Ada"}');
        assert.equal(documentRequests, 1, 'Mutation must not reload the document');
        await page.locator('#editString').click();
        await page.locator('#expiryMode').selectOption('set');
        await page.locator('#insertTTL').fill('120');
        await page.locator('#saveKey').click();
        await page.locator('#editorDialog').waitFor({ state: 'hidden' });
        assert.equal(mutations.at(-1).InsertTTLSeconds, 120);
        rejectNextWrite = true;
        await page.locator('#editString').click();
        await page.locator('#insertValue').fill('stale edit');
        await page.locator('#saveKey').click();
        await page.waitForFunction(() => document.getElementById('editorError').textContent.includes('value changed'));
        assert.equal(await page.locator('#editorDialog').isVisible(), true);
        assert.equal(await page.locator('#saveKey').isDisabled(), false);
        await page.locator('#closeEditor').click();
        await page.getByRole('button', { name: 'users:hash', exact: true }).click();
        await page.locator('#elementRows').getByRole('button', { name: 'Edit', exact: true }).click();
        await page.locator('#insertValue').fill('after');
        await page.locator('#saveKey').click();
        await page.locator('#editorDialog').waitFor({ state: 'hidden' });
        assert.equal(mutations.at(-1).ElementType, 'hash');
        assert.equal(mutations.at(-1).ElementSelector, 'field');
        await page.getByRole('button', { name: 'large:string', exact: true }).click();
        await page.locator('#nextValue').waitFor({ state: 'visible' });
        assert.equal(await page.locator('#editString').isVisible(), false);
        await page.getByRole('button', { name: 'binary:value', exact: true }).click();
        await page.waitForFunction(() => document.getElementById('valueContent').textContent === 'AAEC');
        assert.equal(await page.locator('#editString').isVisible(), false);
        await page.locator('#expiringOnly').check();
        assert.equal(await page.locator('#redisTable tbody .key-select').count(), 1);
        await page.locator('#expiringOnly').uncheck();
        await page.locator('#searchInput').fill('users:*');
        await page.locator('#saveSearch').click();
        assert.equal(await page.locator('#savedSearches option').count(), 2);
        await page.locator('#savedSearches').selectOption('users:*');
        await page.waitForFunction(() => new URLSearchParams(location.search).get('key') === 'users:*');
        await page.locator('#keyTree summary').first().click();
        await page.getByRole('button', { name: 'Search users:*', exact: true }).click();
        await page.locator('#auditPanel summary').click();
        await page.waitForFunction(() => document.getElementById('auditRows').textContent.includes('tester'));
        expireNextPage = true;
        await page.goto('http://redisui.test/redis?page=42&offset=123&key=users%3A*');
        await page.locator('#refreshKeys').click();
        await page.waitForFunction(() => document.getElementById('notice').textContent.includes('Search restarted'));
        assert.equal(new URL(page.url()).searchParams.get('page'), '0');
        assert.equal(new URL(page.url()).searchParams.get('offset'), '0');
        assert.equal(new URL(page.url()).searchParams.get('key'), 'users:*');
        assert.equal(await page.locator('#previousKeys').isDisabled(), true);
        await page.screenshot({ path: path.resolve('browser-desktop.png'), fullPage: true });
        await page.setViewportSize({ width: 390, height: 844 });
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true, 'Mobile layout must fit viewport');
        await page.screenshot({ path: path.resolve('browser-mobile.png'), fullPage: true });
        allowEdit = false;
        await page.goto('http://redisui.test/redis');
        assert.equal(await page.locator('#addKey').isVisible(), false);
        assert.equal(await page.locator('.delete-key').count(), 0);
        assert.equal(await page.locator('#savedSearches option').count(), 2, 'Saved searches should survive navigation');
        allowEdit = true;
        await page.clock.install();
        await page.goto('http://redisui.test/redis/statistics');
        await page.waitForFunction(() => document.getElementById('metric-ops').textContent === '5');
        assert.equal(await page.locator('.metric-chart').count(), 4);
        await page.locator('#metricsPause').check();
        await page.clock.fastForward(6000);
        assert.equal(metricsCount, 1);
        await page.locator('#metricsPause').uncheck();
        await page.waitForFunction(() => document.getElementById('metric-hit').textContent === '90.9');
        assert.equal(await page.locator('#chart-memory polyline').count(), 1);
        await page.locator('#metricsPause').check();
        await page.screenshot({ path: path.resolve('browser-statistics.png'), fullPage: true });
        assert.deepEqual(failures, []);
        assert.ok(requests.filter(x => /value|keys|audit|metrics/.test(x.pathname)).every(x => x.searchParams.get('connection') === 'default' && x.searchParams.get('db') === '0'));
        console.log('Browser checks passed: JSON editing, TTL modes, collection edits, bounded preview, saved searches, tree, audit, read-only UI, metrics and mobile layout.');
        await context.close();
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
