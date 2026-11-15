// The service worker of the published site, as the .NET build publishes the original's:
// - service-worker-assets.js, the manifest: every published file with the SHA-256 of its content, in the format the
//   .NET build writes it (the worker reads it, and the start-up script of the Docker image and the post-publish
//   scripts of a release find the hashes in it as text);
// - service-worker.js: public/service-worker.published.js with the manifest version on its first line, so that the
//   worker itself changes whenever a published file does, and the browser installs it as an update;
// - public/service-worker.js, the do-nothing worker, stays what the development server serves.

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import type { Plugin } from 'vite';

const workerName = 'service-worker.js';
const publishedWorkerName = 'service-worker.published.js';
const manifestName = 'service-worker-assets.js';

interface ServiceWorkerAsset {
    hash: string;
    url: string;
}

function sha256(content: Buffer): Buffer {
    return crypto.createHash('sha256').update(content).digest();
}

/** The manifest's text, as the .NET build writes it (System.Text.Json, indented; the URLs in ordinal order). */
function formatManifest(assets: ServiceWorkerAsset[]): { version: string; text: string } {
    const sorted = [...assets].sort((a, b) => (a.url < b.url ? -1 : a.url > b.url ? 1 : 0));

    // the version: the start of the hash of all the files' hashes — any changed file changes it
    const version = sha256(Buffer.from(sorted.map(asset => asset.hash).join(''), 'utf8')).toString('base64').substring(0, 8);

    const text = `self.assetsManifest = ${JSON.stringify({ version, assets: sorted.map(({ hash, url }) => ({ hash, url })) }, null, 2)};\n`;
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

    return {
        name: 'oneshelf-service-worker',
        apply: 'build',

        configResolved(config) {
            outDir = path.resolve(config.root, config.build.outDir);
        },

        closeBundle() {
            const published = fs.readFileSync(path.join(outDir, publishedWorkerName), 'utf8');
            fs.rmSync(path.join(outDir, publishedWorkerName));

            const assets = listFiles(outDir)
                .filter(url => url !== workerName && url !== manifestName)
                .map(url => ({ url, hash: `sha256-${sha256(fs.readFileSync(path.join(outDir, url))).toString('base64')}` }));

            const { version, text } = formatManifest(assets);
            fs.writeFileSync(path.join(outDir, manifestName), text, 'utf8');
            fs.writeFileSync(path.join(outDir, workerName), `/* Manifest version: ${version} */\n${published}`, 'utf8');
        },
    };
}
