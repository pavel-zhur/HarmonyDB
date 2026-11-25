// The app's service worker, as the old frontend publishes its own (OneShelf.Frontend.WebTs/build/serviceWorker.ts,
// copied): after the build,
// - service-worker-assets.js, the manifest: every published file of the app with the SHA-256 of its content;
// - service-worker.js: build/service-worker.js with the manifest's version on its first line, so that the worker
//   itself changes whenever a published file does, and the browser installs it as an update.
// The development server has no worker: the app registers it only in a build.

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import type { Plugin } from 'vite';

const workerName = 'service-worker.js';
const manifestName = 'service-worker-assets.js';

/** Pages of the build that are not the app (the public page): not precached by the app's worker. */
const notApp = [/^public\.html$/];

/**
 * Files of the app not worth fetching for everyone at install, for what only some open (the fingerings' pieces,
 * build/fingerings.ts): in the manifest's `onDemand`, each kept by the worker the first time it is asked for.
 */
const onDemand = [/^assets\/fingerings\//];

interface ServiceWorkerAsset {
    hash: string;
    url: string;
}

function sha256(content: Buffer): Buffer {
    return crypto.createHash('sha256').update(content).digest();
}

function formatManifest(all: ServiceWorkerAsset[]): { version: string; text: string } {
    const sorted = [...all].sort((a, b) => (a.url < b.url ? -1 : a.url > b.url ? 1 : 0));

    // the version: the start of the hash of all the files' hashes — any changed file changes it
    const version = sha256(Buffer.from(sorted.map(asset => asset.hash).join(''), 'utf8')).toString('base64').substring(0, 8);

    const assets = sorted.filter(asset => !onDemand.some(pattern => pattern.test(asset.url)));
    const later = sorted.filter(asset => onDemand.some(pattern => pattern.test(asset.url)));
    const text = `self.assetsManifest = ${JSON.stringify({ version, assets, onDemand: later }, null, 2)};\n`;
    return { version, text };
}

function listFiles(directory: string, relative = ''): string[] {
    return fs.readdirSync(path.join(directory, relative), { withFileTypes: true }).flatMap(entry => {
        const name = relative === '' ? entry.name : `${relative}/${entry.name}`;
        return entry.isDirectory() ? listFiles(directory, name) : [name];
    });
}

export function serviceWorker(): Plugin {
    let outDir = '';
    let root = '';

    return {
        name: 'shelf-service-worker',
        apply: 'build',

        configResolved(config) {
            root = config.root;
            outDir = path.resolve(config.root, config.build.outDir);
        },

        closeBundle() {
            const worker = fs.readFileSync(path.join(root, 'build', workerName), 'utf8');

            const assets = listFiles(outDir)
                .filter(url => url !== workerName && url !== manifestName && !notApp.some(pattern => pattern.test(url)))
                .map(url => ({ url, hash: `sha256-${sha256(fs.readFileSync(path.join(outDir, url))).toString('base64')}` }));

            const { version, text } = formatManifest(assets);
            fs.writeFileSync(path.join(outDir, manifestName), text, 'utf8');
            fs.writeFileSync(path.join(outDir, workerName), `/* Manifest version: ${version} */\n${worker}`, 'utf8');
        },
    };
}
