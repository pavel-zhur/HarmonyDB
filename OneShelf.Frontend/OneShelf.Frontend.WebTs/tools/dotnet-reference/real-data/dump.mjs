// Dumps the original site's offline library (IndexedDB OneShelfDatabase) from a running original site, for the
// comparison on real data. Read only: it logs in as the given test account in a fresh, temporary browser profile,
// waits for the site to download the library, reads the stores and closes the browser (the profile is deleted).
// The dump is other people's songs: write it outside the repository.
//
//   node tools/dotnet-reference/real-data/dump.mjs <playwright-core directory> <site url> <output .json>
//   e.g. node tools/dotnet-reference/real-data/dump.mjs <dir>/node_modules/playwright-core http://localhost:13003 <dir>/dump.json
//
// The account: the login page's link for entering without Telegram ("нажмите здесь").

import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';

const [playwrightDirectory, site, output] = process.argv.slice(2);
if (!output) throw new Error('Usage: dump.mjs <playwright-core directory> <site url> <output .json>');
const playwright = await import(pathToFileURL(join(playwrightDirectory, 'index.js')).href);
const { chromium } = playwright.default ?? playwright;

const profile = mkdtempSync(join(tmpdir(), 'oneshelf-dump-'));
const context = await chromium.launchPersistentContext(profile, { channel: 'chrome', headless: true });
try {
    const page = await context.newPage();
    await page.goto(`${site}/login`);
    await page.getByText('нажмите здесь').click({ timeout: 120000 });

    // Wait until the stores stop growing for a while.
    const counts = () => page.evaluate(() => new Promise(resolve => {
        const request = indexedDB.open('OneShelfDatabase');
        request.onerror = () => resolve(null);
        request.onsuccess = () => {
            const db = request.result;
            const names = [...db.objectStoreNames];
            if (names.length === 0) {
                db.close();
                resolve({});
                return;
            }

            const result = {};
            const transaction = db.transaction(names, 'readonly');
            for (const name of names) transaction.objectStore(name).count().onsuccess = e => { result[name] = e.target.result; };
            transaction.oncomplete = () => {
                db.close();
                resolve(result);
            };
        };
    }));

    let last = '';
    let stableSince = Date.now();
    const started = Date.now();
    while (Date.now() - started < 15 * 60 * 1000) {
        await page.waitForTimeout(3000);
        const current = JSON.stringify(await counts());
        if (current !== last) {
            console.log(current);
            last = current;
            stableSince = Date.now();
        } else if (Date.now() - stableSince > 30000 && current !== '{}' && current !== 'null') {
            break;
        }
    }

    const dump = await page.evaluate(() => new Promise((resolve, reject) => {
        const request = indexedDB.open('OneShelfDatabase');
        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
            const db = request.result;
            const names = [...db.objectStoreNames];
            const result = {};
            const transaction = db.transaction(names, 'readonly');
            for (const name of names) {
                const store = transaction.objectStore(name);
                const keys = store.getAllKeys();
                const values = store.getAll();
                transaction.addEventListener('complete', () => {
                    result[name] = keys.result.map((key, i) => ({ key, value: values.result[i] }));
                });
            }

            transaction.oncomplete = () => {
                db.close();
                setTimeout(() => resolve(result));
            };
        };
    }));
    writeFileSync(output, JSON.stringify(dump));
    console.log(`Written ${output}: ${Object.entries(dump).map(([name, items]) => `${name} ${items.length}`).join(', ')}.`);
} finally {
    await context.close();
    rmSync(profile, { recursive: true, force: true });
}
